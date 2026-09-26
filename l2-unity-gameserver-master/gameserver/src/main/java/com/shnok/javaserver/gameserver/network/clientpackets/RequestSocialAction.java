package com.shnok.javaserver.gameserver.network.clientpackets;

import com.shnok.javaserver.gameserver.enums.FloodProtector;
import com.shnok.javaserver.gameserver.enums.IntentionType;
import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.network.SystemMessageId;
import com.shnok.javaserver.gameserver.network.serverpackets.SocialAction;

public class RequestSocialAction extends L2GameClientPacket
{
	private int _actionId;
	
	@Override
	protected void readImpl()
	{
		_actionId = readD();
	}
	
	@Override
	protected void runImpl()
	{
		if (!getClient().performAction(FloodProtector.SOCIAL))
			return;
		
		final Player player = getClient().getPlayer();
		if (player == null)
			return;
		
		if (player.isFishing())
		{
			player.sendPacket(SystemMessageId.CANNOT_DO_WHILE_FISHING_3);
			return;
		}
		
		// Les 17 emotes declarees par ActionName vont de 2 a 21. L'effet de montee de
		// niveau a ete deplace hors de cette plage pour ne plus entrer en conflit.
		if (_actionId < 2 || _actionId > 21)
			return;
		
		if (player.isOperating() || player.getActiveRequester() != null || player.isAlikeDead() || player.getAI().getCurrentIntention().getType() != IntentionType.IDLE)
			return;
		
		player.broadcastPacket(new SocialAction(player, _actionId));
	}
}