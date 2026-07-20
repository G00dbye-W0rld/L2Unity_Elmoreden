package com.shnok.javaserver.gameserver.model.actor.ai.type;

import com.shnok.javaserver.commons.logging.CLogger;
import com.shnok.javaserver.gameserver.data.SkillTable;
import com.shnok.javaserver.gameserver.enums.AiEventType;
import com.shnok.javaserver.gameserver.enums.IntentionType;
import com.shnok.javaserver.gameserver.enums.ZoneId;
import com.shnok.javaserver.gameserver.enums.items.ItemLocation;
import com.shnok.javaserver.gameserver.model.World;
import com.shnok.javaserver.gameserver.model.WorldObject;
import com.shnok.javaserver.gameserver.model.actor.Boat;
import com.shnok.javaserver.gameserver.model.actor.Creature;
import com.shnok.javaserver.gameserver.model.actor.Playable;
import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.model.actor.ai.Intention;
import com.shnok.javaserver.gameserver.model.item.instance.ItemInstance;
import com.shnok.javaserver.gameserver.model.location.Location;
import com.shnok.javaserver.gameserver.network.SystemMessageId;
import com.shnok.javaserver.gameserver.network.serverpackets.combat.ActionAllowed;
import com.shnok.javaserver.gameserver.network.serverpackets.movement.MoveToPawn;
import com.shnok.javaserver.gameserver.skills.L2Skill;

public abstract class PlayableAI<T extends Playable> extends CreatureAI<T>
{
	private static final CLogger LOGGER = new CLogger(PlayableAI.class.getName());

	protected Intention _previousIntention = new Intention();
	
	protected PlayableAI(T actor)
	{
		super(actor);
	}
	
	@Override
	protected synchronized void prepareIntention()
	{
		_actor.getMove().cancelFollowTask();
		
		// Refresh previous intention with current intention.
		_previousIntention.updateUsing(_currentIntention);
		
		// Reset next intention.
		_nextIntention.updateAsIdle();
	}
	
	@Override
	protected void onEvtFinishedCasting()
	{
		if (_nextIntention.isBlank())
		{
			if (_currentIntention.getType() == IntentionType.CAST)
			{
				final L2Skill skill = _currentIntention.getSkill();
				final Creature target = _currentIntention.getFinalTarget();
				
				if (skill.nextActionIsAttack() && target.isAttackableWithoutForceBy(_actor))
					doAttackIntention(target, _currentIntention.isCtrlPressed(), _currentIntention.isShiftPressed(), true);
				else
					doIdleIntention();
			}
			else
				// TODO This occurs with skills that change the AI of the caster in callSkill->useSkill (eg. SoE)
				doIdleIntention();
		}
		else
			doIntention(_nextIntention);
	}
	
	@Override
	protected void onEvtFinishedAttack()
	{
		if (_nextIntention.isBlank())
		{
			if (_actor.canKeepAttacking(_currentIntention.getFinalTarget()))
				notifyEvent(AiEventType.THINK, null, null);
			else
				doIdleIntention();
		}
		else
			doIntention(_nextIntention);
	}
	
	@Override
	protected void thinkCast()
	{
		if (_actor.denyAiAction() || _actor.getAllSkillsDisabled() || _actor.getCast().isCastingNow())
		{
			doIdleIntention();
			return;
		}
		
		final Creature target = _currentIntention.getFinalTarget();
		final L2Skill skill = _currentIntention.getSkill();
		
		if (isTargetLost(target, skill))
		{
			doIdleIntention();
			return;
		}
		
		if (!_actor.getCast().canAttemptCast(target, skill))
			return;
		
		final boolean isShiftPressed = _currentIntention.isShiftPressed();
		if (_actor.getMove().maybeStartOffensiveFollow(target, skill.getCastRange()))
		{
			if (isShiftPressed)
				doIdleIntention();
			
			return;
		}
		
		// Stop the movement, no matter the cast output. Edit the heading in the same time.
		if (skill.getHitTime() > 50)
		{
			_actor.getMove().stop();
			
			if (target != _actor)
				_actor.getPosition().setHeadingTo(target);
		}
		
		// In case the cast is unsuccessful, we trigger MoveToPawn to handle the rotation.
		if (!_actor.getCast().canCast(target, skill, _currentIntention.isCtrlPressed(), _currentIntention.getItemObjectId()))
		{
			if (target != _actor)
				_actor.broadcastPacket(new MoveToPawn(_actor, target, (int) _actor.distance3D(target)));
			
			doIdleIntention();
			return;
		}
		
		_actor.getCast().doCast(skill, target, null);
	}
	
	@Override
	protected void thinkFlee()
	{
		if (_actor.isMovementDisabled())
		{
			doIdleIntention();
			return;
		}
		
		final Creature target = _currentIntention.getFinalTarget();
		if (_actor == target)
		{
			doIdleIntention();
			return;
		}
		
		final int distance = _currentIntention.getItemObjectId();
		if (distance < 10)
		{
			doIdleIntention();
			return;
		}
		
		_actor.fleeFrom(target, distance);
	}
	
	@Override
	protected void thinkFollow()
	{
		clientActionFailed();
		
		if (_actor.denyAiAction() || _actor.isMovementDisabled())
		{
			doIdleIntention();
			return;
		}
		
		final Creature target = _currentIntention.getFinalTarget();
		if (_actor == target)
		{
			doIdleIntention();
			return;
		}
		
		final boolean isShiftPressed = _currentIntention.isShiftPressed();
		if (isShiftPressed)
		{
			doIdleIntention();
			return;
		}
		
		_actor.getMove().maybeStartFriendlyFollow(target, 70);
	}
	
	@Override
	protected void thinkMoveTo()
	{
		if (_actor.denyAiAction() || _actor.isMovementDisabled())
		{
			doIdleIntention();
			clientActionFailed();
			return;
		}
		
		_actor.getMove().maybeMoveToLocation(_currentIntention.getLoc(), 0, true, false);
	}
	
	@Override
	protected ItemInstance thinkPickUp()
	{
		clientActionFailed();

		LOGGER.info("[thinkPickUp] DEBUG start, itemObjectId=" + _currentIntention.getItemObjectId());

		if (_actor.denyAiAction() || _actor.isSitting())
		{
			LOGGER.info("[thinkPickUp] DEBUG rejected: denyAiAction=" + _actor.denyAiAction() + " isSitting=" + _actor.isSitting());
			doIdleIntention();
			return null;
		}

		final WorldObject target = World.getInstance().getObject(_currentIntention.getItemObjectId());
		if (target == null)
		{
			LOGGER.info("[thinkPickUp] DEBUG rejected: target not found in World for itemObjectId=" + _currentIntention.getItemObjectId());
			doIdleIntention();
			return null;
		}
		if (!(target instanceof ItemInstance item))
		{
			LOGGER.info("[thinkPickUp] DEBUG rejected: target is not an ItemInstance, class=" + target.getClass().getSimpleName());
			doIdleIntention();
			return null;
		}
		if (isTargetLost(target))
		{
			LOGGER.info("[thinkPickUp] DEBUG rejected: isTargetLost=true (knows=" + _actor.knows(target) + ", worldLookup=" + (World.getInstance().getObject(target.getObjectId()) != null) + ")");
			doIdleIntention();
			return null;
		}

		if (item.getLocation() != ItemLocation.VOID)
		{
			LOGGER.info("[thinkPickUp] DEBUG rejected: item.getLocation()=" + item.getLocation() + " (expected VOID)");
			doIdleIntention();
			return null;
		}

		final boolean isShiftPressed = _currentIntention.isShiftPressed();
		LOGGER.info("[thinkPickUp] DEBUG actorPos=" + _actor.getPosition() + " itemPos=" + target.getPosition());

		// Ground items are picked up on a flat plane: gate on horizontal (2D)
		// distance only. maybeMoveToLocation()'s "position reached?" check is
		// full 3D (isIn3DRadius), and on this map the geodata height under a
		// dropped item can sit ~0.5m away from the player's own tracked Z even
		// when standing right on top of it, which ate almost the entire 36-unit
		// budget and made pickup succeed only by horizontal-jitter luck. If
		// we're already close enough horizontally there's nothing to walk
		// towards, so skip the 3D-gated move check entirely.
		if (!_actor.isIn2DRadius(target, 36) && _actor.getMove().maybeMoveToLocation(target.getPosition(), 36, false, isShiftPressed))
		{
			LOGGER.info("[thinkPickUp] DEBUG rejected: maybeMoveToLocation returned true (still moving toward item)");
			if (isShiftPressed)
				doIdleIntention();

			return null;
		}

		LOGGER.info("[thinkPickUp] DEBUG SUCCESS - picking up item " + item.getObjectId());
		doIdleIntention();

		return item;
	}
	
	public synchronized void tryToAttack(Creature target, boolean isCtrlPressed, boolean isShiftPressed)
	{
		if (_actor.denyAiAction())
		{
			clientActionFailed();
			return;
		}
		
		// These situations are waited out regardless. Any Intention that is added is scheduled as nextIntention.
		if (_actor.getAttack().isAttackingNow() || _actor.getCast().isCastingNow() || _actor.isSittingNow() || _actor.isStandingNow() || canScheduleAfter(_currentIntention.getType(), IntentionType.ATTACK))
		{
			getNextIntention().updateAsAttack(target, isCtrlPressed, isShiftPressed, true);
			clientActionFailed();
			return;
		}
		
		if (target instanceof Playable)
		{
			final Player targetPlayer = target.getActingPlayer();
			final Player actorPlayer = _actor.getActingPlayer();
			
			if (!target.isInsideZone(ZoneId.PVP))
			{
				if (targetPlayer.getProtectionBlessing() && (actorPlayer.getStatus().getLevel() - targetPlayer.getStatus().getLevel()) >= 10 && actorPlayer.getKarma() > 0)
				{
					actorPlayer.sendPacket(SystemMessageId.TARGET_IS_INCORRECT);
					clientActionFailed();
					return;
				}
				
				if (actorPlayer.getProtectionBlessing() && (targetPlayer.getStatus().getLevel() - actorPlayer.getStatus().getLevel()) >= 10 && targetPlayer.getKarma() > 0)
				{
					actorPlayer.sendPacket(SystemMessageId.TARGET_IS_INCORRECT);
					clientActionFailed();
					return;
				}
			}
			
			if (targetPlayer.isCursedWeaponEquipped() && actorPlayer.getStatus().getLevel() <= 20)
			{
				actorPlayer.sendPacket(SystemMessageId.TARGET_IS_INCORRECT);
				clientActionFailed();
				return;
			}
			
			if (actorPlayer.isCursedWeaponEquipped() && targetPlayer.getStatus().getLevel() <= 20)
			{
				actorPlayer.sendPacket(SystemMessageId.TARGET_IS_INCORRECT);
				clientActionFailed();
				return;
			}
		}
		
		doAttackIntention(target, isCtrlPressed, isShiftPressed, true);
	}
	
	public synchronized void tryToAttack(Creature target)
	{
		tryToAttack(target, false, false);
	}
	
	public synchronized void tryToCast(Creature target, L2Skill skill, boolean isCtrlPressed, boolean isShiftPressed, int itemObjectId)
	{
		if (_actor.denyAiAction())
		{
			clientActionFailed();
			return;
		}
		
		final Creature finalTarget = skill.getFinalTarget(_actor, target);
		if (finalTarget == null || !_actor.getCast().canAttemptCast(finalTarget, skill))
		{
			clientActionFailed();
			return;
		}
		
		// These situations are waited out regardless. Any Intention that is added is scheduled as nextIntention.
		if (_actor.getAttack().isAttackingNow() || _actor.getCast().isCastingNow() || _actor.isSittingNow() || _actor.isStandingNow() || canScheduleAfter(_currentIntention.getType(), IntentionType.CAST))
		{
			getNextIntention().updateAsCast(_actor, target, skill, isCtrlPressed, isShiftPressed, itemObjectId, true);
			clientActionFailed();
			return;
		}
		
		doCastIntention(target, skill, isCtrlPressed, isShiftPressed, itemObjectId, true);
	}
	
	public synchronized void tryToCast(Creature target, L2Skill skill)
	{
		tryToCast(target, skill, false, false, 0);
	}
	
	public synchronized void tryToCast(Creature target, int id, int level)
	{
		final L2Skill skill = SkillTable.getInstance().getInfo(id, level);
		if (skill != null)
			tryToCast(target, skill, false, false, 0);
	}
	
	public synchronized void tryToFollow(Creature target, boolean isShiftPressed)
	{
		if (_actor == target || _actor.denyAiAction())
		{
			clientActionFailed();
			return;
		}
		
		// These situations are waited out regardless. Any Intention that is added is scheduled as nextIntention.
		if (_actor.getAttack().isAttackingNow() || _actor.getCast().isCastingNow() || _actor.isSittingNow() || _actor.isStandingNow() || canScheduleAfter(_currentIntention.getType(), IntentionType.FOLLOW))
		{
			getNextIntention().updateAsFollow(target, isShiftPressed);
			clientActionFailed();
			return;
		}
		
		doFollowIntention(target, isShiftPressed);
	}
	
	public synchronized void tryToIdle()
	{
		if (_actor.denyAiAction())
		{
			clientActionFailed();
			return;
		}
		
		// These situations are waited out regardless. Any Intention that is added is scheduled as nextIntention.
		if (_actor.getAttack().isAttackingNow() || _actor.getCast().isCastingNow() || _actor.isSittingNow() || _actor.isStandingNow() || canScheduleAfter(_currentIntention.getType(), IntentionType.IDLE))
		{
			getNextIntention().updateAsIdle();
			clientActionFailed();
			return;
		}
		
		doIdleIntention();
	}
	
	public synchronized void tryToInteract(WorldObject target, boolean isCtrlPressed, boolean isShiftPressed)
	{
		if (_actor.denyAiAction())
		{
			clientActionFailed();
			return;
		}
		
		// These situations are waited out regardless. Any Intention that is added is scheduled as nextIntention.
		if (_actor.getAttack().isAttackingNow() || _actor.getCast().isCastingNow() || _actor.isSittingNow() || _actor.isStandingNow() || canScheduleAfter(_currentIntention.getType(), IntentionType.INTERACT))
		{
			getNextIntention().updateAsInteract(target, isCtrlPressed, isShiftPressed);
			clientActionFailed();
			return;
		}
		
		doInteractIntention(target, isCtrlPressed, isShiftPressed);
	}

	public synchronized void tryToMoveTo(Location dir, Boat boat) {
		tryToMoveTo(dir, boat, false);
	}

	public synchronized void tryToMoveTo(Location dir, Boat boat, boolean requireResponse)
	{
		if (_actor.denyAiAction())
		{
			if(requireResponse) clientActionFailed();
			return;
		}
		
		// These situations are waited out regardless. Any Intention that is added is scheduled as nextIntention.
		if (_actor.getAttack().isAttackingNow() || _actor.getCast().isCastingNow() || _actor.isSittingNow() || _actor.isStandingNow() || canScheduleAfter(_currentIntention.getType(), IntentionType.MOVE_TO))
		{
			getNextIntention().updateAsMoveTo(dir, boat);
			if(requireResponse) clientActionFailed();
			return;
		}

		if(requireResponse) {
			_actor.sendPacket(ActionAllowed.STATIC_PACKET);
		}
		
		doMoveToIntention(dir, boat);
	}

	public synchronized void tryToPickUp(int itemObjectId, boolean isShiftPressed)
	{
		LOGGER.info("[tryToPickUp] DEBUG start, itemObjectId=" + itemObjectId + " currentIntentionType=" + _currentIntention.getType());

		if (_actor.denyAiAction())
		{
			LOGGER.info("[tryToPickUp] DEBUG rejected: denyAiAction=true");
			clientActionFailed();
			return;
		}

		// These situations are waited out regardless. Any Intention that is added is scheduled as nextIntention.
		if (_actor.getAttack().isAttackingNow() || _actor.getCast().isCastingNow() || _actor.isSittingNow() || _actor.isStandingNow() || canScheduleAfter(_currentIntention.getType(), IntentionType.PICK_UP))
		{
			LOGGER.info("[tryToPickUp] DEBUG queued as nextIntention instead of processed now: isAttackingNow=" + _actor.getAttack().isAttackingNow()
				+ " isCastingNow=" + _actor.getCast().isCastingNow() + " isSittingNow=" + _actor.isSittingNow()
				+ " isStandingNow=" + _actor.isStandingNow() + " canScheduleAfter=" + canScheduleAfter(_currentIntention.getType(), IntentionType.PICK_UP));
			getNextIntention().updateAsPickUp(itemObjectId, isShiftPressed);
			clientActionFailed();
			return;
		}

		doPickUpIntention(itemObjectId, isShiftPressed);
	}
	
	public synchronized void tryToSit(WorldObject target)
	{
		if (_actor.denyAiAction())
		{
			clientActionFailed();
			return;
		}
		
		// These situations are waited out regardless. Any Intention that is added is scheduled as nextIntention.
		if (_actor.getAttack().isAttackingNow() || _actor.getCast().isCastingNow() || _actor.isSittingNow() || _actor.isStandingNow() || canScheduleAfter(_currentIntention.getType(), IntentionType.SIT))
		{
			getNextIntention().updateAsSit(target);
			return;
		}
	
		doSitIntention(target);
	}
	
	public synchronized void tryToStand()
	{
		if (_actor.denyAiAction())
		{
			clientActionFailed();
			return;
		}
		
		// These situations are waited out regardless. Any Intention that is added is scheduled as nextIntention.
		if (_actor.getAttack().isAttackingNow() || _actor.getCast().isCastingNow() || _actor.isSittingNow() || _actor.isStandingNow() || canScheduleAfter(_currentIntention.getType(), IntentionType.STAND))
		{
			getNextIntention().updateAsStand();
			return;
		}
		
		doStandIntention();
	}
	
	public synchronized void tryToUseItem(int itemObjectId)
	{
		if (_actor.denyAiAction())
		{
			clientActionFailed();
			return;
		}
		
		// These situations are waited out regardless. Any Intention that is added is scheduled as nextIntention.
		if (_actor.getAttack().isAttackingNow() || _actor.getCast().isCastingNow() || _actor.isSittingNow() || _actor.isStandingNow() || canScheduleAfter(_currentIntention.getType(), IntentionType.USE_ITEM))
		{
			getNextIntention().updateAsUseItem(itemObjectId);
			return;
		}
		
		doUseItemIntention(itemObjectId);
	}
}
