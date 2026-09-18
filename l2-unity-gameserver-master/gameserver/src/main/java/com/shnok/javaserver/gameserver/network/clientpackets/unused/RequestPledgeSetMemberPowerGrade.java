package com.shnok.javaserver.gameserver.network.clientpackets.unused;

import com.shnok.javaserver.gameserver.enums.PrivilegeType;
import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.model.pledge.Clan;
import com.shnok.javaserver.gameserver.model.pledge.ClanMember;
import com.shnok.javaserver.gameserver.network.SystemMessageId;
import com.shnok.javaserver.gameserver.network.clientpackets.L2GameClientPacket;
import com.shnok.javaserver.gameserver.network.serverpackets.unused.PledgeShowMemberListUpdate;
import com.shnok.javaserver.gameserver.network.serverpackets.SystemMessage;

public final class RequestPledgeSetMemberPowerGrade extends L2GameClientPacket
{
	private String _memberName;
	private int _powerGrade;
	
	@Override
	protected void readImpl()
	{
		_memberName = readS();
		_powerGrade = readD();
	}
	
	@Override
	protected void runImpl()
	{
		final Player player = getClient().getPlayer();
		if (player == null)
			return;
		
		final Clan clan = player.getClan();
		if (clan == null)
			return;
		
		// Sans ce controle, n'importe quel membre pouvait changer le rang de
		// n'importe qui, le sien compris, et s'octroyer les privileges d'un rang.
		if (!player.hasClanPrivileges(PrivilegeType.SP_MANAGE_RANKS))
		{
			player.sendPacket(SystemMessageId.YOU_ARE_NOT_AUTHORIZED_TO_DO_THAT);
			return;
		}
		
		// Hors des rangs 1 a 9, PledgePowerGradeList deborderait de son tableau.
		if (_powerGrade < 1 || _powerGrade > 9)
			return;
		
		final ClanMember member = clan.getClanMember(_memberName);
		if (member == null || member.getObjectId() == clan.getLeaderId())
			return;
		
		if (member.getPledgeType() == Clan.SUBUNIT_ACADEMY)
			return;
		
		member.setPowerGrade(_powerGrade);
		
		clan.broadcastToMembers(new PledgeShowMemberListUpdate(member), SystemMessage.getSystemMessage(SystemMessageId.CLAN_MEMBER_S1_PRIVILEGE_CHANGED_TO_S2).addString(member.getName()).addNumber(_powerGrade));
	}
}