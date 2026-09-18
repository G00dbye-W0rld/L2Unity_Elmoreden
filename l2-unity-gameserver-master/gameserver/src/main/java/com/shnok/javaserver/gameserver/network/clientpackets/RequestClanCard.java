package com.shnok.javaserver.gameserver.network.clientpackets;

import com.shnok.javaserver.gameserver.data.sql.ClanTable;
import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.model.pledge.Clan;
import com.shnok.javaserver.gameserver.network.serverpackets.ExClanCard;

/**
 * Fiche publique d'un clan, demandee depuis la plaque de selection d'un joueur.
 * Ajout du projet : Interlude ne donnait que le nom du clan et de l'alliance.
 */
public final class RequestClanCard extends L2GameClientPacket
{
	private int _clanId;
	
	@Override
	protected void readImpl()
	{
		_clanId = readD();
	}
	
	@Override
	protected void runImpl()
	{
		final Player player = getClient().getPlayer();
		if (player == null)
			return;
		
		final Clan clan = ClanTable.getInstance().getClan(_clanId);
		if (clan == null)
			return;
		
		player.sendPacket(new ExClanCard(clan, player.getClan()));
	}
	
	@Override
	protected boolean triggersOnActionRequest()
	{
		return false;
	}
}
