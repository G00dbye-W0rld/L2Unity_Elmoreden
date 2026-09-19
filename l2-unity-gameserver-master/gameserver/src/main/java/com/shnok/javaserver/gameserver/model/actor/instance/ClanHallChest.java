package com.shnok.javaserver.gameserver.model.actor.instance;

import com.shnok.javaserver.Config;
import com.shnok.javaserver.gameserver.data.manager.ClanHallManager;
import com.shnok.javaserver.gameserver.enums.PrivilegeType;
import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.model.actor.template.NpcTemplate;
import com.shnok.javaserver.gameserver.model.pledge.Clan;
import com.shnok.javaserver.gameserver.model.residence.clanhall.ClanHall;
import com.shnok.javaserver.gameserver.network.SystemMessageId;
import com.shnok.javaserver.gameserver.network.serverpackets.combat.ActionFailed;
import com.shnok.javaserver.gameserver.network.serverpackets.unused.NpcHtmlMessage;
import com.shnok.javaserver.gameserver.network.serverpackets.unused.WarehouseDepositList;
import com.shnok.javaserver.gameserver.network.serverpackets.unused.WarehouseWithdrawList;

/**
 * Coffre d'une salle de clan (ajout du projet) : acces a l'entrepot du clan
 * proprietaire. Invisible pour le client, qui affiche le meuble a sa place.
 */
public class ClanHallChest extends Folk
{
	private int _clanHallId;

	public ClanHallChest(int objectId, NpcTemplate template)
	{
		super(objectId, template);
	}

	public void setClanHallId(int clanHallId)
	{
		_clanHallId = clanHallId;
	}

	@Override
	public boolean isWarehouse()
	{
		return true;
	}

	@Override
	public void sendInfo(Player player)
	{
		// Le meuble est affiche par le client : pas de PNJ a montrer.
	}

	private boolean isOwner(Player player)
	{
		final ClanHall ch = ClanHallManager.getInstance().getClanHall(_clanHallId);
		return ch != null && player.getClan() != null && ch.getOwnerId() == player.getClanId();
	}

	@Override
	public void showChatWindow(Player player)
	{
		player.sendPacket(ActionFailed.STATIC_PACKET);

		final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
		html.setFile("data/html/clanHallManager/chest" + (isOwner(player) ? "" : "-locked") + ".htm");
		html.replace("%objectId%", getObjectId());
		player.sendPacket(html);
	}

	@Override
	public void onBypassFeedback(Player player, String command)
	{
		if (!isOwner(player))
			return;

		final Clan clan = player.getClan();
		if (clan.getLevel() < Config.CLAN_WAREHOUSE_MIN_LEVEL)
		{
			player.sendPacket(SystemMessageId.ONLY_LEVEL_1_CLAN_OR_HIGHER_CAN_USE_WAREHOUSE);
			return;
		}

		if (command.equals("WithdrawC"))
		{
			if (!player.hasClanPrivileges(PrivilegeType.SP_WAREHOUSE_SEARCH))
			{
				player.sendPacket(SystemMessageId.YOU_DO_NOT_HAVE_THE_RIGHT_TO_USE_CLAN_WAREHOUSE);
				return;
			}

			player.setActiveWarehouse(clan.getWarehouse());
			player.sendPacket(new WarehouseWithdrawList(player, WarehouseWithdrawList.CLAN));
		}
		else if (command.equals("DepositC"))
		{
			player.setActiveWarehouse(clan.getWarehouse());
			player.tempInventoryDisable();
			player.sendPacket(new WarehouseDepositList(player, WarehouseDepositList.CLAN));
		}

		player.sendPacket(ActionFailed.STATIC_PACKET);
	}
}
