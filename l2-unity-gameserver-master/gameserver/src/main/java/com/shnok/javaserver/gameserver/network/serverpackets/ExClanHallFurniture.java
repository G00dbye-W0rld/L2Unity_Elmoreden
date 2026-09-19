package com.shnok.javaserver.gameserver.network.serverpackets;

import java.util.Map;

import com.shnok.javaserver.gameserver.data.manager.ClanHallFurnitureManager;

/**
 * Paquet du projet : mobilier pose dans une salle de clan. Pour le coffre, l'objet
 * du PNJ d'entrepot invisible, que le client vise quand on clique le meuble.
 */
public class ExClanHallFurniture extends L2GameServerPacket
{
	private final int _hallId;

	public ExClanHallFurniture(int hallId)
	{
		_hallId = hallId;
	}

	@Override
	protected void writeImpl()
	{
		final ClanHallFurnitureManager manager = ClanHallFurnitureManager.getInstance();
		final Map<Integer, Integer> placed = manager.getPlaced(_hallId);

		writeC(0xfe);
		writeH(0x5d);
		writeD(_hallId);
		writeD(placed.size());
		for (Map.Entry<Integer, Integer> entry : placed.entrySet())
		{
			writeD(entry.getKey());
			writeD(entry.getValue());
			writeD(manager.getChestObjectId(_hallId, entry.getKey()));
		}
		writeD(manager.getWorkbenchObjectId(_hallId));
	}
}
