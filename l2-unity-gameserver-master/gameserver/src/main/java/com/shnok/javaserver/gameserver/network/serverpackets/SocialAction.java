package com.shnok.javaserver.gameserver.network.serverpackets;

import com.shnok.javaserver.gameserver.model.actor.Creature;

public class SocialAction extends L2GameServerPacket
{
	/** Montee de niveau. Hors de la plage des emotes (2 a 21) pour ne pas entrer en conflit
	 * avec la timidite, que ActionName numerote justement 15. */
	public static final int LEVEL_UP = 100;

	private final int _charObjId;
	private final int _actionId;
	
	public SocialAction(Creature cha, int actionId)
	{
		_charObjId = cha.getObjectId();
		_actionId = actionId;
	}
	
	@Override
	protected final void writeImpl()
	{
		writeC(0x2d);
		writeD(_charObjId);
		writeD(_actionId);
		System.out.println("Social action: " + _actionId);
	}
}
