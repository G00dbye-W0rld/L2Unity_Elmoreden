package com.shnok.javaserver.gameserver.network.clientpackets;

import com.shnok.javaserver.Config;
import com.shnok.javaserver.gameserver.data.sql.ClanTable;
import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.model.pledge.Clan;
import com.shnok.javaserver.gameserver.network.SystemMessageId;
import com.shnok.javaserver.gameserver.network.serverpackets.unused.PledgeShowInfoUpdate;

/**
 * Dissolution d'un clan par son chef. Le clan n'est pas detruit tout de suite :
 * il entre en dissolution, et la tache de ClanTable le supprime au terme du
 * delai. Le chef peut donc encore l'annuler cote serveur en remettant le delai
 * a zero.
 */
public final class RequestDismissPledge extends L2GameClientPacket
{
	@Override
	protected void readImpl()
	{
		// Do nothing.
	}
	
	@Override
	protected void runImpl()
	{
		final Player player = getClient().getPlayer();
		if (player == null)
			return;
		
		final Clan clan = player.getClan();
		if (clan == null)
		{
			player.sendPacket(SystemMessageId.YOU_ARE_NOT_A_CLAN_MEMBER);
			return;
		}
		
		if (!player.isClanLeader())
		{
			player.sendPacket(SystemMessageId.YOU_ARE_NOT_AUTHORIZED_TO_DO_THAT);
			return;
		}
		
		if (clan.getAllyId() != 0)
		{
			player.sendPacket(SystemMessageId.FAILED_TO_DISPERSE_CLAN);
			return;
		}
		
		if (clan.isAtWar())
		{
			player.sendPacket(SystemMessageId.CANNOT_DISSOLVE_WHILE_IN_WAR);
			return;
		}
		
		if (clan.isRegisteredOnSiege())
		{
			player.sendPacket(SystemMessageId.CANNOT_DISSOLVE_CAUSE_CLAN_WILL_PARTICIPATE_IN_CASTLE_SIEGE);
			return;
		}
		
		if (clan.hasCastle() || clan.hasClanHall())
		{
			player.sendPacket(SystemMessageId.CANNOT_DISSOLVE_CAUSE_CLAN_OWNS_CASTLES_HIDEOUTS);
			return;
		}
		
		if (clan.getDissolvingExpiryTime() > System.currentTimeMillis())
		{
			player.sendPacket(SystemMessageId.DISSOLUTION_IN_PROGRESS);
			return;
		}
		
		clan.setDissolvingExpiryTime(System.currentTimeMillis() + Config.CLAN_DISSOLVE_DAYS * 86400000L);
		clan.updateClanInDB();
		
		ClanTable.getInstance().scheduleRemoveClan(clan);
		
		player.sendPacket(new PledgeShowInfoUpdate(clan));
		player.sendPacket(SystemMessageId.DISSOLUTION_IN_PROGRESS);
	}
}
