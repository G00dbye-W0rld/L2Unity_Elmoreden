package com.shnok.javaserver.gameserver.network.clientpackets.unused;

import com.shnok.javaserver.gameserver.enums.PrivilegeType;
import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.model.pledge.Clan;
import com.shnok.javaserver.gameserver.model.pledge.ClanMember;
import com.shnok.javaserver.gameserver.network.SystemMessageId;
import com.shnok.javaserver.gameserver.network.clientpackets.L2GameClientPacket;
import com.shnok.javaserver.gameserver.network.serverpackets.unused.PledgeReceiveMemberInfo;

public final class RequestPledgeReorganizeMember extends L2GameClientPacket
{
	private int _isMemberSelected;
	private String _memberName;
	private int _newPledgeType;
	private String _selectedMemberName;
	
	@Override
	protected void readImpl()
	{
		_isMemberSelected = readD();
		_memberName = readS();
		_newPledgeType = readD();
		_selectedMemberName = readS();
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
		
		if (!player.hasClanPrivileges(PrivilegeType.SP_MANAGE_RANKS))
		{
			player.sendPacket(SystemMessageId.YOU_ARE_NOT_AUTHORIZED_TO_DO_THAT);
			return;
		}
		
		final ClanMember member1 = clan.getClanMember(_memberName);
		if (member1 == null || member1.getObjectId() == clan.getLeaderId())
			return;
		
		// Unite cible reelle uniquement ; l'academie a ses propres conditions d'entree.
		final int oldPledgeType = member1.getPledgeType();
		final boolean validTarget = _newPledgeType == 0 || clan.getSubPledge(_newPledgeType) != null;
		if (!validTarget || _newPledgeType == Clan.SUBUNIT_ACADEMY || oldPledgeType == Clan.SUBUNIT_ACADEMY || oldPledgeType == _newPledgeType || clan.isSubPledgeLeader(member1.getObjectId()))
		{
			player.sendPacket(new PledgeReceiveMemberInfo(member1));
			return;
		}
		
		// Place libre : simple deplacement. Unite pleine : echange avec le membre choisi.
		if (_isMemberSelected == 0)
		{
			if (clan.getSubPledgeMembersCount(_newPledgeType) >= clan.getMaxNrOfMembers(_newPledgeType))
			{
				player.sendPacket(SystemMessageId.SUBCLAN_IS_FULL);
				player.sendPacket(new PledgeReceiveMemberInfo(member1));
				return;
			}
			
			member1.setPledgeType(_newPledgeType);
			clan.broadcastClanStatus();
			return;
		}
		
		final ClanMember member2 = clan.getClanMember(_selectedMemberName);
		if (member2 == null || member2.getObjectId() == clan.getLeaderId() || member2.getPledgeType() != _newPledgeType || clan.isSubPledgeLeader(member2.getObjectId()))
		{
			player.sendPacket(new PledgeReceiveMemberInfo(member1));
			return;
		}
		
		member1.setPledgeType(_newPledgeType);
		member2.setPledgeType(oldPledgeType);
		
		clan.broadcastClanStatus();
	}
}
