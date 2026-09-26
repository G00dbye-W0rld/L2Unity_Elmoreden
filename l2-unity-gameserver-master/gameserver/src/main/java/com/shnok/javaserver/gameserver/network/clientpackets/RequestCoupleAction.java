package com.shnok.javaserver.gameserver.network.clientpackets;

import com.shnok.javaserver.gameserver.enums.FloodProtector;
import com.shnok.javaserver.gameserver.enums.IntentionType;
import com.shnok.javaserver.gameserver.model.World;
import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.network.SystemMessageId;
import com.shnok.javaserver.gameserver.network.serverpackets.unused.ConfirmDlg;

/**
 * Emote a deux : saluts croises, tape dans la main, danse. Le demandeur vise un joueur, qui
 * recoit une fenetre de confirmation ; les deux moities ne sont jouees que s'il accepte.
 */
public class RequestCoupleAction extends L2GameClientPacket
{
	/** Portee maximale, comme un echange. */
	private static final int RANGE = 150;

	private int _targetId;
	private int _actionId;

	@Override
	protected void readImpl()
	{
		_targetId = readD();
		_actionId = readD();
	}

	@Override
	protected void runImpl()
	{
		if (!getClient().performAction(FloodProtector.SOCIAL))
			return;

		final Player player = getClient().getPlayer();
		if (player == null)
			return;

		// Les trois seules emotes a deux que le client declare.
		if (_actionId < 16 || _actionId > 18)
			return;

		if (!canPlay(player))
			return;

		final Player target = World.getInstance().getPlayer(_targetId);
		if (target == null || target == player || !canPlay(target))
		{
			player.sendPacket(SystemMessageId.TARGET_IS_INCORRECT);
			return;
		}

		if (!player.isIn3DRadius(target, RANGE))
		{
			player.sendPacket(SystemMessageId.TARGET_TOO_FAR);
			return;
		}

		// Une demande en attente remplace la precedente : rien ne se joue tant que la cible
		// n'a pas repondu, donc aucune ne peut rester bloquee.
		target.setCoupleRequest(player.getObjectId(), _actionId);

		final ConfirmDlg dlg = new ConfirmDlg(Player.COUPLE_ACTION_MESSAGE_ID);
		dlg.addString(player.getName());
		dlg.addTime(30000);
		dlg.addRequesterId(player.getObjectId());
		target.sendPacket(dlg);
	}

	private static boolean canPlay(Player player)
	{
		return !player.isOperating() && player.getActiveRequester() == null && !player.isAlikeDead()
			&& player.getAI().getCurrentIntention().getType() == IntentionType.IDLE;
	}
}
