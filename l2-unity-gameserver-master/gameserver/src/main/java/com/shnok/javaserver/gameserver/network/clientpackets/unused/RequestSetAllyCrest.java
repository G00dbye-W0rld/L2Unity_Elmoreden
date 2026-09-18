package com.shnok.javaserver.gameserver.network.clientpackets.unused;

import com.shnok.javaserver.gameserver.data.cache.CrestCache;
import com.shnok.javaserver.gameserver.enums.CrestType;
import com.shnok.javaserver.gameserver.idfactory.IdFactory;
import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.model.pledge.Clan;
import com.shnok.javaserver.gameserver.network.SystemMessageId;
import com.shnok.javaserver.gameserver.network.clientpackets.L2GameClientPacket;

/**
 * Blason d'alliance, pose par le chef du clan qui dirige l'alliance. Memes
 * limites que le blason de clan : PNG ou DDS, taille maximale de CrestType.
 */
public final class RequestSetAllyCrest extends L2GameClientPacket
{
	private static final int MAX_SIDE = 256;
	
	private int _length;
	private byte[] _data;
	
	@Override
	protected void readImpl()
	{
		_length = readD();
		if (_length < 0 || _length > CrestType.ALLY.getSize())
			return;
		
		_data = new byte[_length];
		readB(_data);
	}
	
	@Override
	protected void runImpl()
	{
		if (_data == null)
			return;
		
		final Player player = getClient().getPlayer();
		if (player == null)
			return;
		
		// L'alliance porte l'identifiant du clan qui la dirige.
		final Clan clan = player.getClan();
		if (clan == null || clan.getAllyId() == 0 || clan.getAllyId() != clan.getClanId() || !player.isClanLeader())
		{
			player.sendPacket(SystemMessageId.FEATURE_ONLY_FOR_ALLIANCE_LEADER);
			return;
		}
		
		if (_length == 0)
		{
			if (clan.getAllyCrestId() != 0)
			{
				clan.changeAllyCrest(0, false);
				player.sendPacket(SystemMessageId.CLAN_CREST_HAS_BEEN_DELETED);
			}
			return;
		}
		
		if (!CrestCache.isAcceptedImage(_data, MAX_SIDE))
		{
			player.sendPacket(SystemMessageId.YOU_ARE_NOT_AUTHORIZED_TO_DO_THAT);
			return;
		}
		
		final int crestId = IdFactory.getInstance().getNextId();
		if (CrestCache.getInstance().saveCrest(CrestType.ALLY, crestId, _data))
		{
			clan.changeAllyCrest(crestId, false);
			player.sendPacket(SystemMessageId.CLAN_EMBLEM_WAS_SUCCESSFULLY_REGISTERED);
		}
	}
}
