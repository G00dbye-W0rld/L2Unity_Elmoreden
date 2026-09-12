package com.shnok.javaserver.gameserver.handler.chathandlers;

import com.shnok.javaserver.Config;
import com.shnok.javaserver.gameserver.enums.FloodProtector;
import com.shnok.javaserver.gameserver.enums.SayType;
import com.shnok.javaserver.gameserver.handler.IChatHandler;
import com.shnok.javaserver.gameserver.model.World;
import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.network.serverpackets.CreatureSay;

public class ChatTrade implements IChatHandler
{
	private static final SayType[] COMMAND_IDS =
	{
		SayType.TRADE
	};
	
	@Override
	public void handleChat(SayType type, Player player, String target, String text)
	{
		if (Config.WORLD_CHAT_MIN_LEVEL > 0 && player.getStatus().getLevel() < Config.WORLD_CHAT_MIN_LEVEL)
		{
			player.sendMessage("Vous devez atteindre le niveau " + Config.WORLD_CHAT_MIN_LEVEL + " pour parler dans le canal Monde.");
			return;
		}
		
		if (!player.getClient().performAction(FloodProtector.TRADE_CHAT))
		{
			player.sendMessage("Vous parlez trop vite dans le canal Monde, patientez un instant.");
			return;
		}
		
		final CreatureSay cs = new CreatureSay(player, type, text);
		
		// Canal Monde : atteint tous les joueurs connectes. Sinon la portee
		// d'origine, qui regroupe les joueurs par point de resurrection.
		if (Config.WORLD_CHAT_GLOBAL)
		{
			for (Player p : World.getInstance().getPlayers())
			{
				if (p.isOnline())
					p.sendPacket(cs);
			}
		}
		else
			World.broadcastToSameRegion(player, cs);
	}
	
	@Override
	public SayType[] getChatTypeList()
	{
		return COMMAND_IDS;
	}
}
