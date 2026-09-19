package com.shnok.javaserver.gameserver.model.actor.instance;

import com.shnok.javaserver.gameserver.data.manager.ClanHallManager;
import com.shnok.javaserver.gameserver.enums.PrivilegeType;
import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.model.actor.template.NpcTemplate;
import com.shnok.javaserver.gameserver.model.residence.clanhall.ClanHall;
import com.shnok.javaserver.gameserver.model.residence.clanhall.ClanHallFunction;
import com.shnok.javaserver.gameserver.network.serverpackets.combat.ActionFailed;

/**
 * Etabli d'une salle de clan (ajout du projet) : ouvre la fabrication au niveau
 * installe. Invisible pour le client, qui affiche l'etabli du decor a sa place.
 */
public class ClanHallWorkbench extends Merchant
{
	private int _clanHallId;

	public ClanHallWorkbench(int objectId, NpcTemplate template)
	{
		super(objectId, template);
	}

	public void setClanHallId(int clanHallId)
	{
		_clanHallId = clanHallId;
	}

	@Override
	public void sendInfo(Player player)
	{
		// L'etabli est affiche par le client : pas de PNJ a montrer.
	}

	@Override
	public void showChatWindow(Player player)
	{
		player.sendPacket(ActionFailed.STATIC_PACKET);

		final ClanHall ch = ClanHallManager.getInstance().getClanHall(_clanHallId);
		if (ch == null || player.getClan() == null || ch.getOwnerId() != player.getClanId())
		{
			player.sendMessage("Cet établi appartient au clan de cette salle.");
			return;
		}

		if (!player.hasClanPrivileges(PrivilegeType.CHP_USE_FUNCTIONS))
		{
			player.sendMessage("Vous n'avez pas le droit d'utiliser les installations de la salle.");
			return;
		}

		final ClanHallFunction chf = ch.getFunction(ClanHall.FUNC_CREATE_ITEM);
		if (chf == null)
		{
			player.sendMessage("L'établi n'est pas en service : installez-le depuis les Améliorations de la gérante.");
			return;
		}

		showBuyWindow(player, chf.getLvl() * 100000 + getNpcId());
	}
}
