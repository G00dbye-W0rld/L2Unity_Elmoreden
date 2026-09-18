package com.shnok.javaserver.gameserver.network.serverpackets;

import com.shnok.javaserver.gameserver.model.pledge.Clan;

/**
 * Fiche publique d'un clan (sous-code 0x5c, hors de la plage d'Interlude).
 * L'etat de guerre est vu depuis le clan du demandeur : 1 nous l'avons
 * declaree, 2 il nous l'a declaree, 3 guerre mutuelle.
 */
public class ExClanCard extends L2GameServerPacket
{
	private final Clan _clan;
	private final int _warState;
	
	public ExClanCard(Clan clan, Clan viewerClan)
	{
		_clan = clan;
		
		int state = 0;
		if (viewerClan != null && viewerClan != clan)
		{
			if (viewerClan.isAtWarWith(clan.getClanId()))
				state |= 1;
			if (clan.isAtWarWith(viewerClan.getClanId()))
				state |= 2;
		}
		_warState = state;
	}
	
	@Override
	protected void writeImpl()
	{
		writeC(0xfe);
		writeH(0x5c);
		writeD(_clan.getClanId());
		writeS(_clan.getName());
		writeD(_clan.getLevel());
		writeS(_clan.getLeaderName());
		writeD(_clan.getMembersCount());
		writeD(_clan.getOnlineMembersCount());
		writeD(_clan.getAllyId());
		writeS(_clan.getAllyName() != null ? _clan.getAllyName() : "");
		writeD(_clan.getCrestId());
		writeD(_clan.getAllyCrestId());
		writeD(_clan.getCrestLargeId());
		writeD(_clan.getCastleId());
		writeD(_clan.getClanHallId());
		writeD(_clan.getReputationScore());
		writeD(_warState);
	}
}
