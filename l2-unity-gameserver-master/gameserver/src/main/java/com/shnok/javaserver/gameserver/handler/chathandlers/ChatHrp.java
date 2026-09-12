package com.shnok.javaserver.gameserver.handler.chathandlers;

import com.shnok.javaserver.gameserver.enums.FloodProtector;
import com.shnok.javaserver.gameserver.enums.SayType;
import com.shnok.javaserver.gameserver.handler.IChatHandler;
import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.network.serverpackets.CreatureSay;

// Canal hors Role Play, meme portee que le Role Play : ce qui se dit autour de
// soi, mais en tant que joueur et non en tant que personnage.
public class ChatHrp implements IChatHandler
{
	private static final SayType[] COMMAND_IDS =
	{
		SayType.HRP
	};
	
	@Override
	public void handleChat(SayType type, Player player, String target, String text)
	{
		if (!player.getClient().performAction(FloodProtector.GLOBAL_CHAT))
		{
			player.sendMessage("Vous parlez trop vite, patientez un instant.");
			return;
		}
		
		final CreatureSay cs = new CreatureSay(player, type, text);
		
		player.sendPacket(cs);
		player.forEachKnownTypeInRadius(Player.class, 1250, p -> p.sendPacket(cs));
	}
	
	@Override
	public SayType[] getChatTypeList()
	{
		return COMMAND_IDS;
	}
}
