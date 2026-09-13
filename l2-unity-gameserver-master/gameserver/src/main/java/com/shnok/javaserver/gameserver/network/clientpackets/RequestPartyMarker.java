package com.shnok.javaserver.gameserver.network.clientpackets;

import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.model.group.Party;
import com.shnok.javaserver.gameserver.network.serverpackets.unused.RadarControl;

/**
 * Marqueur pose par un joueur sur sa carte et partage avec son groupe. Le
 * client envoie la position visee, le serveur la rediffuse aux membres.
 */
public final class RequestPartyMarker extends L2GameClientPacket
{
	private static final int MARKER_TYPE = 2;
	
	private int _x;
	private int _y;
	private int _z;
	
	@Override
	protected void readImpl()
	{
		_x = readD();
		_y = readD();
		_z = readD();
	}
	
	@Override
	protected void runImpl()
	{
		final Player player = getClient().getPlayer();
		if (player == null)
			return;
		
		final Party party = player.getParty();
		// Hors groupe le marqueur reste local : rien a rediffuser.
		if (party == null)
			return;
		
		final RadarControl marker = new RadarControl(0, MARKER_TYPE, _x, _y, _z);
		for (Player member : party.getMembers())
		{
			if (member != player)
				member.sendPacket(marker);
		}
	}
}
