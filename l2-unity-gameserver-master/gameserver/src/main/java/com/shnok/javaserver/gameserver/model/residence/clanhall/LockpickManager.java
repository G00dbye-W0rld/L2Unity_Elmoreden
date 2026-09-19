package com.shnok.javaserver.gameserver.model.residence.clanhall;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

import com.shnok.javaserver.Config;
import com.shnok.javaserver.commons.pool.ThreadPool;
import com.shnok.javaserver.commons.random.Rnd;

import com.shnok.javaserver.gameserver.data.sql.ClanTable;
import com.shnok.javaserver.gameserver.model.World;
import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.model.actor.instance.Door;
import com.shnok.javaserver.gameserver.model.pledge.Clan;
import com.shnok.javaserver.gameserver.network.serverpackets.ExLockpick;

/**
 * Crochetage des portes de salle de clan (ajout du projet). Le serveur tire tous
 * les des : la fenetre du client ne fait qu'animer le resultat recu.
 */
public class LockpickManager
{
	public static final int STATE_OPEN = 0;
	public static final int STATE_ROLLED = 1;
	public static final int STATE_SUCCESS = 2;
	public static final int STATE_CLOSED = 3;

	public static final int RESULT_NONE = 0;
	public static final int RESULT_PIN = 1;
	public static final int RESULT_FAIL = 2;
	public static final int RESULT_ALARM = 3;

	private static final int MAX_DISTANCE = 200;
	private static final long ROLL_DELAY = 2000;

	private static class Session
	{
		int doorObjectId;
		int pins;
		long lastRoll;
	}

	private final Map<Integer, Session> _sessions = new ConcurrentHashMap<>();
	private final Map<Integer, Long> _cooldowns = new ConcurrentHashMap<>();

	/**
	 * Premier contact avec une porte de salle de clan fermee, hors du clan proprietaire.
	 * @return un message de refus, ou null si la fenetre de crochetage s'ouvre.
	 */
	public String start(Player player, Door door)
	{
		final String refusal = check(player, door);
		if (refusal != null)
			return refusal;

		final Session session = new Session();
		session.doorObjectId = door.getObjectId();
		_sessions.put(player.getObjectId(), session);
		send(player, door, session, STATE_OPEN, 0, RESULT_NONE);
		return null;
	}

	public void roll(Player player, int doorObjectId)
	{
		final Session session = _sessions.get(player.getObjectId());
		if (session == null || session.doorObjectId != doorObjectId || !(World.getInstance().getObject(doorObjectId) instanceof Door door))
			return;

		final String refusal = check(player, door);
		if (refusal != null)
		{
			abandon(player);
			player.sendMessage(refusal);
			return;
		}

		final long now = System.currentTimeMillis();
		if (now - session.lastRoll < ROLL_DELAY)
			return;
		session.lastRoll = now;

		final ClanHall ch = (ClanHall) door.getResidence();
		final int die = Rnd.get(1, 20);
		final int result;

		if (die == 1)
			result = RESULT_ALARM;
		else if (die == 20 || die + bonus(player) >= difficulty(ch))
			result = RESULT_PIN;
		else
			result = RESULT_FAIL;

		// Un echec brise un crochet ; les goupilles deja ouvertes le restent.
		if (result != RESULT_PIN)
			player.destroyItemByItemId(Config.LOCKPICK_ITEM_ID, 1, true);

		if (result == RESULT_ALARM)
			alertOwners(ch, player.getName() + " tente de forcer la porte de " + ch.getName() + " !");

		if (result == RESULT_PIN && ++session.pins >= Config.LOCKPICK_PINS)
		{
			_sessions.remove(player.getObjectId());
			forceOpen(player, door, ch);
			send(player, door, session, STATE_SUCCESS, die, result);
			return;
		}

		send(player, door, session, STATE_ROLLED, die, result);
	}

	public void abandon(Player player)
	{
		_sessions.remove(player.getObjectId());
	}

	private void forceOpen(Player player, Door door, ClanHall ch)
	{
		door.openMe();
		ThreadPool.schedule(door::closeMe, Config.LOCKPICK_DOOR_OPEN_SECONDS * 1000L);
		_cooldowns.put(door.getObjectId(), System.currentTimeMillis() + Config.LOCKPICK_DOOR_COOLDOWN_MINUTES * 60000L);

		player.setKarma(player.getKarma() + Config.LOCKPICK_KARMA);
		alertOwners(ch, "La porte de " + ch.getName() + " a été forcée par " + player.getName() + " !");
	}

	private static String check(Player player, Door door)
	{
		if (!Config.LOCKPICK_ENABLED || !(door.getResidence() instanceof ClanHall ch))
			return "Vous n'êtes pas autorisé à faire cela.";

		if (door.isOpened())
			return "La porte est déjà ouverte.";

		final Clan owner = ClanTable.getInstance().getClan(ch.getOwnerId());
		if (owner == null)
			return "Cette salle n'a pas de propriétaire : il n'y a rien à forcer.";

		if (player.getClanId() == owner.getClanId())
			return "Cette salle appartient à votre clan.";

		if (Config.LOCKPICK_REQUIRE_OWNER_ONLINE && owner.getOnlineMembersCount() == 0)
			return "Aucun membre du clan n'est là : la serrure est condamnée.";

		final Long cooldown = getInstance()._cooldowns.get(door.getObjectId());
		if (cooldown != null && cooldown > System.currentTimeMillis())
			return "La serrure vient d'être forcée : elle a été renforcée pour un temps.";

		if (!player.isIn3DRadius(door, MAX_DISTANCE))
			return "Vous êtes trop loin de la porte.";

		if (player.getInventory().getItemCount(Config.LOCKPICK_ITEM_ID) <= 0)
			return "Il vous faut des crochets de serrurier.";

		return null;
	}

	public static int difficulty(ClanHall ch)
	{
		return Config.LOCKPICK_BASE_DIFFICULTY + Config.LOCKPICK_DIFFICULTY_PER_GRADE * ch.getGrade();
	}

	public static int bonus(Player player)
	{
		return Math.max(0, (player.getStatus().getDEX() - 30) / 3);
	}

	private static void alertOwners(ClanHall ch, String message)
	{
		final Clan owner = ClanTable.getInstance().getClan(ch.getOwnerId());
		if (owner == null)
			return;

		for (Player member : owner.getOnlineMembers())
			member.sendMessage(message);
	}

	private static void send(Player player, Door door, Session session, int state, int die, int result)
	{
		final ClanHall ch = (ClanHall) door.getResidence();
		player.sendPacket(new ExLockpick(door.getObjectId(), ch.getName(), state, session.pins, Config.LOCKPICK_PINS, difficulty(ch), bonus(player), die, result, player.getInventory().getItemCount(Config.LOCKPICK_ITEM_ID)));
	}

	public static LockpickManager getInstance()
	{
		return SingletonHolder.INSTANCE;
	}

	private static class SingletonHolder
	{
		protected static final LockpickManager INSTANCE = new LockpickManager();
	}
}
