package com.shnok.javaserver.gameserver.network.clientpackets;

import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.model.residence.clanhall.LockpickManager;

/**
 * Paquet du projet : jet de de de la fenetre de crochetage (1), ou abandon (0).
 */
public final class RequestLockpick extends L2GameClientPacket
{
	private int _doorObjectId;
	private int _action;

	@Override
	protected void readImpl()
	{
		_doorObjectId = readD();
		_action = readD();
	}

	@Override
	protected void runImpl()
	{
		final Player player = getClient().getPlayer();
		if (player == null)
			return;

		if (_action == 1)
			LockpickManager.getInstance().roll(player, _doorObjectId);
		else
			LockpickManager.getInstance().abandon(player);
	}
}
