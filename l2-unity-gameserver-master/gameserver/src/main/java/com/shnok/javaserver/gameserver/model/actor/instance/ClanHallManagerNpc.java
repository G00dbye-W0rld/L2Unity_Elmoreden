package com.shnok.javaserver.gameserver.model.actor.instance;

import java.text.SimpleDateFormat;
import java.util.Map;
import java.util.StringTokenizer;
import java.util.concurrent.TimeUnit;

import com.shnok.javaserver.Config;
import com.shnok.javaserver.gameserver.data.manager.ClanHallFurnitureManager;
import com.shnok.javaserver.gameserver.data.xml.ClanHallDecoData;
import com.shnok.javaserver.gameserver.data.xml.ClanHallUpgradeData;
import com.shnok.javaserver.gameserver.data.xml.ItemData;
import com.shnok.javaserver.gameserver.model.item.instance.ItemInstance;
import com.shnok.javaserver.gameserver.enums.PrivilegeType;
import com.shnok.javaserver.gameserver.enums.TeleportType;
import com.shnok.javaserver.gameserver.enums.actors.NpcTalkCond;
import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.model.actor.ai.type.ClanHallManagerNpcAI;
import com.shnok.javaserver.gameserver.model.actor.template.NpcTemplate;
import com.shnok.javaserver.gameserver.model.pledge.Clan;
import com.shnok.javaserver.gameserver.model.residence.clanhall.ClanHall;
import com.shnok.javaserver.gameserver.model.residence.clanhall.ClanHallFunction;
import com.shnok.javaserver.gameserver.model.residence.clanhall.SiegableHall;
import com.shnok.javaserver.gameserver.network.SystemMessageId;
import com.shnok.javaserver.gameserver.network.serverpackets.combat.ActionFailed;
import com.shnok.javaserver.gameserver.network.serverpackets.unused.ClanHallDecoration;
import com.shnok.javaserver.gameserver.network.serverpackets.unused.NpcHtmlMessage;
import com.shnok.javaserver.gameserver.network.serverpackets.unused.WarehouseDepositList;
import com.shnok.javaserver.gameserver.network.serverpackets.unused.WarehouseWithdrawList;

public class ClanHallManagerNpc extends Merchant
{
	private static final String REMOVE_HP = "[<a action=\"bypass -h npc_%objectId%_manage recovery hp_cancel\">Retirer</a>]";
	private static final String HP_GRADE_1 = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 2\">40%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 5\">100%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 8\">160%</a>]";
	private static final String HP_GRADE_2 = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 4\">80%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 7\">140%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 10\">200%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 260\">260%</a>]";
	private static final String HP_GRADE_3 = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 4\">80%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 6\">120%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 9\">180%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 12\">240%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 15\">300%</a>]";
	private static final String HP_GRADE_2_SCH = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 25\">300%</a>]";
	private static final String HP_GRADE_3_SCH = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 25\">300%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 30\">400%</a>]";
	
	private static final String REMOVE_EXP = "[<a action=\"bypass -h npc_%objectId%_manage recovery exp_cancel\">Retirer</a>]";
	private static final String EXP_GRADE_1 = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_exp 1\">5%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_exp 3\">15%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_exp 6\">30%</a>]";
	private static final String EXP_GRADE_2 = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_exp 1\">5%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_exp 3\">15%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_exp 5\">25%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_exp 8\">40%</a>]";
	private static final String EXP_GRADE_3 = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_exp 3\">15%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_exp 5\">25%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_exp 7\">35%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_exp 10\">50%</a>]";
	private static final String EXP_GRADE_2_SCH = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_exp 19\">45%</a>]";
	private static final String EXP_GRADE_3_SCH = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_exp 19\">45%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_exp 20\">50%</a>]";
	
	private static final String REMOVE_MP = "[<a action=\"bypass -h npc_%objectId%_manage recovery mp_cancel\">Retirer</a>]";
	private static final String MP_GRADE_1 = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_mp 1\">5%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_mp 3\">15%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_mp 5\">25%</a>]";
	private static final String MP_GRADE_2 = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_mp 1\">5%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_mp 3\">15%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_mp 6\">30%</a>]";
	private static final String MP_GRADE_3 = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_mp 1\">5%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_mp 3\">15%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_mp 6\">30%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_mp 8\">40%</a>]";
	private static final String MP_GRADE_2_SCH = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_mp 18\">40%</a>]";
	private static final String MP_GRADE_3_SCH = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_mp 18\">40%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_mp 20\">50%</a>]";
	
	private static final String REMOVE_SUPPORT = "[<a action=\"bypass -h npc_%objectId%_manage other support_cancel\">Retirer</a>]";
	private static final String SUPPORT_GRADE_1 = "[<a action=\"bypass -h npc_%objectId%_manage other edit_support 1\">Niveau 1</a>][<a action=\"bypass -h npc_%objectId%_manage other edit_support 2\">Niveau 2</a>][<a action=\"bypass -h npc_%objectId%_manage other edit_support 4\">Niveau 4</a>]";
	private static final String SUPPORT_GRADE_2 = "[<a action=\"bypass -h npc_%objectId%_manage other edit_support 3\">Niveau 3</a>][<a action=\"bypass -h npc_%objectId%_manage other edit_support 4\">Niveau 4</a>][<a action=\"bypass -h npc_%objectId%_manage other edit_support 5\">Niveau 5</a>]";
	private static final String SUPPORT_GRADE_3 = "[<a action=\"bypass -h npc_%objectId%_manage other edit_support 3\">Niveau 3</a>][<a action=\"bypass -h npc_%objectId%_manage other edit_support 5\">Niveau 5</a>][<a action=\"bypass -h npc_%objectId%_manage other edit_support 7\">Niveau 7</a>][<a action=\"bypass -h npc_%objectId%_manage other edit_support 8\">Niveau 8</a>]";
	private static final String SUPPORT_GRADE_2_SCH = "[<a action=\"bypass -h npc_%objectId%_manage other edit_support 15\">Niveau 5</a>]";
	private static final String SUPPORT_GRADE_3_SCH = "[<a action=\"bypass -h npc_%objectId%_manage other edit_support 15\">Niveau 5</a>][<a action=\"bypass -h npc_%objectId%_manage other edit_support 18\">Niveau 8</a>]";
	
	private static final String REMOVE_ITEM = "[<a action=\"bypass -h npc_%objectId%_manage other item_cancel\">Retirer</a>]";
	private static final String ITEM = "[<a action=\"bypass -h npc_%objectId%_manage other edit_item 1\">Niveau 1</a>][<a action=\"bypass -h npc_%objectId%_manage other edit_item 2\">Niveau 2</a>][<a action=\"bypass -h npc_%objectId%_manage other edit_item 3\">Niveau 3</a>]";
	private static final String ITEM_SCH = "[<a action=\"bypass -h npc_%objectId%_manage other edit_item 11\">Niveau 1</a>][<a action=\"bypass -h npc_%objectId%_manage other edit_item 12\">Niveau 2</a>][<a action=\"bypass -h npc_%objectId%_manage other edit_item 13\">Niveau 3</a>]";
	
	private static final String REMOVE_TELE = "[<a action=\"bypass -h npc_%objectId%_manage other tele_cancel\">Retirer</a>]";
	private static final String TELE = "[<a action=\"bypass -h npc_%objectId%_manage other edit_tele 1\">Niveau 1</a>][<a action=\"bypass -h npc_%objectId%_manage other edit_tele 2\">Niveau 2</a>]";
	private static final String TELE_SCH = "[<a action=\"bypass -h npc_%objectId%_manage other edit_tele 11\">Niveau 1</a>][<a action=\"bypass -h npc_%objectId%_manage other edit_tele 12\">Niveau 2</a>]";
	
	private static final String REMOVE_CURTAINS = "[<a action=\"bypass -h npc_%objectId%_manage deco curtains_cancel\">Retirer</a>]";
	private static final String CURTAINS = "[<a action=\"bypass -h npc_%objectId%_manage deco edit_curtains 1\">Niveau 1</a>][<a action=\"bypass -h npc_%objectId%_manage deco edit_curtains 2\">Niveau 2</a>]";
	
	private static final String REMOVE_FIXTURES = "[<a action=\"bypass -h npc_%objectId%_manage deco fixtures_cancel\">Retirer</a>]";
	private static final String FIXTURES = "[<a action=\"bypass -h npc_%objectId%_manage deco edit_fixtures 1\">Niveau 1</a>][<a action=\"bypass -h npc_%objectId%_manage deco edit_fixtures 2\">Niveau 2</a>]";
	
	private static final String NONE = "aucune";
	
	public ClanHallManagerNpc(int objectId, NpcTemplate template)
	{
		super(objectId, template);
	}
	
	@Override
	public ClanHallManagerNpcAI getAI()
	{
		return (ClanHallManagerNpcAI) _ai;
	}
	
	@Override
	public void setAI()
	{
		_ai = new ClanHallManagerNpcAI(this);
	}
	
	@Override
	public boolean isWarehouse()
	{
		return true;
	}
	
	@Override
	public void onBypassFeedback(Player player, String command)
	{
		final NpcTalkCond condition = getNpcTalkCond(player);
		if (condition != NpcTalkCond.OWNER)
			return;
		
		final StringTokenizer st = new StringTokenizer(command, " ");
		final String actualCommand = st.nextToken();
		
		String val = (st.hasMoreTokens()) ? st.nextToken() : "";
		
		if (actualCommand.equalsIgnoreCase("services") || actualCommand.equalsIgnoreCase("charges") || actualCommand.equalsIgnoreCase("security") || actualCommand.startsWith("upgrade"))
		{
			handleHallMenu(player, actualCommand, val, st);
			return;
		}
		
		if (actualCommand.startsWith("furnish") || actualCommand.equalsIgnoreCase("furniture_shop"))
		{
			handleFurnish(player, actualCommand, val, st);
			return;
		}
		
		if (actualCommand.equalsIgnoreCase("banish_foreigner"))
		{
			if (!validatePrivileges(player, PrivilegeType.CHP_RIGHT_TO_DISMISS))
				return;
			
			final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
			if (val.equalsIgnoreCase("list"))
				html.setFile("data/html/clanHallManager/banish-list.htm");
			else if (val.equalsIgnoreCase("banish"))
			{
				getClanHall().banishForeigners();
				html.setFile("data/html/clanHallManager/banish.htm");
			}
			html.replace("%objectId%", getObjectId());
			player.sendPacket(html);
		}
		else if (actualCommand.equalsIgnoreCase("manage_vault"))
		{
			if (!validatePrivileges(player, PrivilegeType.SP_WAREHOUSE_SEARCH))
				return;
			
			final boolean isSCH = (getClanHall() instanceof SiegableHall);
			final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
			html.setFile("data/html/clanHallManager/vault" + (isSCH ? "-sch" : "") + ".htm");
			html.replace("%rent%", getClanHall().getLease());
			html.replace("%date%", new SimpleDateFormat("dd-MM-yyyy HH:mm").format(getClanHall().getPaidUntil()));
			html.replace("%objectId%", getObjectId());
			player.sendPacket(html);
		}
		else if (actualCommand.equalsIgnoreCase("door"))
		{
			if (!validatePrivileges(player, PrivilegeType.CHP_ENTRY_EXIT_RIGHTS))
				return;
			
			final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
			if (val.equalsIgnoreCase("open"))
			{
				getClanHall().openDoors();
				html.setFile("data/html/clanHallManager/door-open.htm");
			}
			else if (val.equalsIgnoreCase("close"))
			{
				getClanHall().closeDoors();
				html.setFile("data/html/clanHallManager/door-close.htm");
			}
			else
				html.setFile("data/html/clanHallManager/door.htm");
			
			html.replace("%objectId%", getObjectId());
			player.sendPacket(html);
		}
		else if (actualCommand.equalsIgnoreCase("functions"))
		{
			if (!validatePrivileges(player, PrivilegeType.CHP_USE_FUNCTIONS))
				return;
			
			if (val.equalsIgnoreCase("tele"))
			{
				final ClanHallFunction chf = getClanHall().getFunction(ClanHall.FUNC_TELEPORT);
				if (chf == null)
				{
					final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
					html.setFile("data/html/clanHallManager/functions-disabled.htm");
					html.replace("%objectId%", getObjectId());
					player.sendPacket(html);
					return;
				}
				
				showTeleportWindow(player, (chf.getLvl() == 2) ? TeleportType.CHF_LEVEL_2 : TeleportType.CHF_LEVEL_1);
			}
			else if (val.equalsIgnoreCase("item_creation"))
			{
				if (!st.hasMoreTokens())
					return;
				
				final ClanHallFunction chf = getClanHall().getFunction(ClanHall.FUNC_CREATE_ITEM);
				if (chf == null)
				{
					final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
					html.setFile("data/html/clanHallManager/functions-disabled.htm");
					html.replace("%objectId%", getObjectId());
					player.sendPacket(html);
					return;
				}
				
				showBuyWindow(player, Integer.parseInt(st.nextToken()) + (chf.getLvl() * 100000));
			}
			else if (val.equalsIgnoreCase("support"))
			{
				final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
				
				final ClanHallFunction chf = getClanHall().getFunction(ClanHall.FUNC_SUPPORT_MAGIC);
				if (chf == null)
					html.setFile("data/html/clanHallManager/functions-disabled.htm");
				else
				{
					html.setFile("data/html/clanHallManager/support" + chf.getLvl() + ".htm");
					html.replace("%mp%", (int) getStatus().getMp());
				}
				html.replace("%objectId%", getObjectId());
				player.sendPacket(html);
			}
			else
			{
				final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
				html.setFile("data/html/clanHallManager/functions.htm");
				html.replace("%npcId%", getNpcId());
				html.replace("%objectId%", getObjectId());
				html.replace("%hp_regen%", getClanHall().getFunctionLevel(ClanHall.FUNC_RESTORE_HP));
				html.replace("%mp_regen%", getClanHall().getFunctionLevel(ClanHall.FUNC_RESTORE_MP));
				html.replace("%xp_regen%", getClanHall().getFunctionLevel(ClanHall.FUNC_RESTORE_EXP));
				player.sendPacket(html);
			}
		}
		else if (actualCommand.equalsIgnoreCase("manage"))
		{
			if (!validatePrivileges(player, PrivilegeType.CHP_SET_FUNCTIONS))
				return;
			
			if (val.equalsIgnoreCase("recovery"))
			{
				if (st.hasMoreTokens())
				{
					if (getClanHall().getOwnerId() == 0)
						return;
					
					val = st.nextToken();
					
					if (val.equalsIgnoreCase("hp_cancel"))
					{
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile("data/html/clanHallManager/functions-cancel.htm");
						html.replace("%apply%", "recovery hp 0");
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("mp_cancel"))
					{
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile("data/html/clanHallManager/functions-cancel.htm");
						html.replace("%apply%", "recovery mp 0");
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("exp_cancel"))
					{
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile("data/html/clanHallManager/functions-cancel.htm");
						html.replace("%apply%", "recovery exp 0");
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("edit_hp"))
					{
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile("data/html/clanHallManager/functions-apply.htm");
						html.replace("%name%", "Cheminée (récupération des PV)");
						
						int level = Integer.parseInt(st.nextToken());
						final int funcLvl = level;
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_RESTORE_HP, level);
						final int cost = ClanHallDecoData.getInstance().getDecoFee(ClanHall.FUNC_RESTORE_HP, level);
						if (level > 20)
							level -= 10;
						final int percent = level * 20;
						
						html.replace("%cost%", cost + "</font> Adena / " + days + " jour(s)</font>");
						html.replace("%use%", "Récupération de PV supplémentaire pour les membres du clan dans la salle : <font color=\"00FFFF\">" + percent + "%</font>");
						html.replace("%apply%", "recovery hp " + funcLvl);
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("edit_mp"))
					{
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile("data/html/clanHallManager/functions-apply.htm");
						html.replace("%name%", "Tapis (récupération des PM)");
						
						int level = Integer.parseInt(st.nextToken());
						final int funcLvl = level;
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_RESTORE_MP, level);
						final int cost = ClanHallDecoData.getInstance().getDecoFee(ClanHall.FUNC_RESTORE_MP, level);
						if (level > 10)
							level -= 10;
						final int percent = level * 5;
						
						html.replace("%cost%", cost + "</font> Adena / " + days + " jour(s)</font>");
						html.replace("%use%", "Récupération de PM supplémentaire pour les membres du clan dans la salle : <font color=\"00FFFF\">" + percent + "%</font>");
						html.replace("%apply%", "recovery mp " + funcLvl);
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("edit_exp"))
					{
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile("data/html/clanHallManager/functions-apply.htm");
						html.replace("%name%", "Lustre (expérience rendue)");
						
						int level = Integer.parseInt(st.nextToken());
						final int funcLvl = level;
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_RESTORE_EXP, level);
						final int cost = ClanHallDecoData.getInstance().getDecoFee(ClanHall.FUNC_RESTORE_EXP, level);
						if (level > 10)
							level -= 10;
						final int percent = level * 5;
						
						html.replace("%cost%", cost + "</font> Adena / " + days + " jour(s)</font>");
						html.replace("%use%", "Rend l'expérience perdue aux membres du clan ressuscités dans la salle : <font color=\"00FFFF\">" + percent + "%</font>");
						html.replace("%apply%", "recovery exp " + funcLvl);
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("hp"))
					{
						int level = Integer.parseInt(st.nextToken());
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_RESTORE_HP, level);
						final int cost = ClanHallDecoData.getInstance().getDecoFee(ClanHall.FUNC_RESTORE_HP, level);
						if (level > 20)
							level -= 10;
						final int percent = level * 20;
						
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						
						final ClanHallFunction chf = getClanHall().getFunction(ClanHall.FUNC_RESTORE_HP);
						if (chf != null && chf.getLvl() == percent)
						{
							html.setFile("data/html/clanHallManager/functions-used.htm");
							html.replace("%val%", level + "%");
							html.replace("%objectId%", getObjectId());
							player.sendPacket(html);
							return;
						}
						
						html.setFile("data/html/clanHallManager/functions-apply_confirmed.htm");
						
						int fee = cost;
						if (percent == 0)
						{
							fee = 0;
							html.setFile("data/html/clanHallManager/functions-cancel_confirmed.htm");
						}
						
						if (!getClanHall().updateFunction(player, ClanHall.FUNC_RESTORE_HP, percent, fee, TimeUnit.DAYS.toMillis(days)))
							html.setFile("data/html/clanHallManager/low_adena.htm");
						else
							revalidateDeco(player);
						
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("mp"))
					{
						int level = Integer.parseInt(st.nextToken());
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_RESTORE_MP, level);
						final int cost = ClanHallDecoData.getInstance().getDecoFee(ClanHall.FUNC_RESTORE_MP, level);
						if (level > 10)
							level -= 10;
						final int percent = level * 5;
						
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						
						final ClanHallFunction chf = getClanHall().getFunction(ClanHall.FUNC_RESTORE_MP);
						if (chf != null && chf.getLvl() == percent)
						{
							html.setFile("data/html/clanHallManager/functions-used.htm");
							html.replace("%val%", level + "%");
							html.replace("%objectId%", getObjectId());
							player.sendPacket(html);
							return;
						}
						
						html.setFile("data/html/clanHallManager/functions-apply_confirmed.htm");
						
						int fee = cost;
						if (percent == 0)
						{
							fee = 0;
							html.setFile("data/html/clanHallManager/functions-cancel_confirmed.htm");
						}
						
						if (!getClanHall().updateFunction(player, ClanHall.FUNC_RESTORE_MP, percent, fee, TimeUnit.DAYS.toMillis(days)))
							html.setFile("data/html/clanHallManager/low_adena.htm");
						else
							revalidateDeco(player);
						
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("exp"))
					{
						int level = Integer.parseInt(st.nextToken());
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_RESTORE_EXP, level);
						final int cost = ClanHallDecoData.getInstance().getDecoFee(ClanHall.FUNC_RESTORE_EXP, level);
						if (level > 20)
							level -= 10;
						final int percent = level * 5;
						
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						
						final ClanHallFunction chf = getClanHall().getFunction(ClanHall.FUNC_RESTORE_EXP);
						if (chf != null && chf.getLvl() == percent)
						{
							html.setFile("data/html/clanHallManager/functions-used.htm");
							html.replace("%val%", level + "%");
							html.replace("%objectId%", getObjectId());
							player.sendPacket(html);
							return;
						}
						
						html.setFile("data/html/clanHallManager/functions-apply_confirmed.htm");
						
						int fee = cost;
						if (percent == 0)
						{
							fee = 0;
							html.setFile("data/html/clanHallManager/functions-cancel_confirmed.htm");
						}
						
						if (!getClanHall().updateFunction(player, ClanHall.FUNC_RESTORE_EXP, percent, fee, TimeUnit.DAYS.toMillis(days)))
							html.setFile("data/html/clanHallManager/low_adena.htm");
						else
							revalidateDeco(player);
						
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
				}
				else
				{
					final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
					html.setFile("data/html/clanHallManager/edit_recovery.htm");
					
					final int grade = getClanHall().getGrade();
					final boolean isSCH = (getClanHall() instanceof SiegableHall);
					
					// Restore HP function.
					ClanHallFunction chf = getClanHall().getFunction(ClanHall.FUNC_RESTORE_HP);
					if (chf != null)
					{
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_RESTORE_HP, chf.getFuncLvl());
						html.replace("%hp_recovery%", chf.getLvl() + "%</font> (<font color=\"FFAABB\">" + chf.getLease() + "</font> Adena / " + days + " jour(s))");
						html.replace("%hp_period%", "Prochain prélèvement le " + new SimpleDateFormat("dd-MM-yyyy HH:mm").format(chf.getEndTime()));
						
						switch (grade)
						{
							case 1:
								html.replace("%change_hp%", REMOVE_HP + HP_GRADE_1);
								break;
							
							case 2:
								html.replace("%change_hp%", REMOVE_HP + (isSCH ? HP_GRADE_2_SCH : HP_GRADE_2));
								break;
							
							case 3:
								html.replace("%change_hp%", REMOVE_HP + (isSCH ? HP_GRADE_3_SCH : HP_GRADE_3));
								break;
						}
					}
					else
					{
						html.replace("%hp_recovery%", NONE);
						html.replace("%hp_period%", NONE);
						
						switch (grade)
						{
							case 1:
								html.replace("%change_hp%", HP_GRADE_1);
								break;
							
							case 2:
								html.replace("%change_hp%", (isSCH ? HP_GRADE_2_SCH : HP_GRADE_2));
								break;
							
							case 3:
								html.replace("%change_hp%", (isSCH ? HP_GRADE_3_SCH : HP_GRADE_3));
								break;
						}
					}
					
					// Restore exp function.
					chf = getClanHall().getFunction(ClanHall.FUNC_RESTORE_EXP);
					if (chf != null)
					{
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_RESTORE_EXP, chf.getFuncLvl());
						html.replace("%exp_recovery%", chf.getLvl() + "%</font> (<font color=\"FFAABB\">" + chf.getLease() + "</font> Adena / " + days + " jour(s))");
						html.replace("%exp_period%", "Prochain prélèvement le " + new SimpleDateFormat("dd-MM-yyyy HH:mm").format(chf.getEndTime()));
						
						switch (grade)
						{
							case 1:
								html.replace("%change_exp%", REMOVE_EXP + EXP_GRADE_1);
								break;
							
							case 2:
								html.replace("%change_exp%", REMOVE_EXP + (isSCH ? EXP_GRADE_2_SCH : EXP_GRADE_2));
								break;
							
							case 3:
								html.replace("%change_exp%", REMOVE_EXP + (isSCH ? EXP_GRADE_3_SCH : EXP_GRADE_3));
								break;
						}
					}
					else
					{
						html.replace("%exp_recovery%", NONE);
						html.replace("%exp_period%", NONE);
						
						switch (grade)
						{
							case 1:
								html.replace("%change_exp%", EXP_GRADE_1);
								break;
							
							case 2:
								html.replace("%change_exp%", (isSCH ? EXP_GRADE_2_SCH : EXP_GRADE_2));
								break;
							
							case 3:
								html.replace("%change_exp%", (isSCH ? EXP_GRADE_3_SCH : EXP_GRADE_3));
								break;
						}
					}
					
					// Restore MP function.
					chf = getClanHall().getFunction(ClanHall.FUNC_RESTORE_MP);
					if (chf != null)
					{
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_RESTORE_MP, chf.getFuncLvl());
						html.replace("%mp_recovery%", chf.getLvl() + "%</font> (<font color=\"FFAABB\">" + chf.getLease() + "</font> Adena / " + days + " jour(s))");
						html.replace("%mp_period%", "Prochain prélèvement le " + new SimpleDateFormat("dd-MM-yyyy HH:mm").format(chf.getEndTime()));
						
						switch (grade)
						{
							case 1:
								html.replace("%change_mp%", REMOVE_MP + MP_GRADE_1);
								break;
							
							case 2:
								html.replace("%change_mp%", REMOVE_MP + (isSCH ? MP_GRADE_2_SCH : MP_GRADE_2));
								break;
							
							case 3:
								html.replace("%change_mp%", REMOVE_MP + (isSCH ? MP_GRADE_3_SCH : MP_GRADE_3));
								break;
						}
					}
					else
					{
						html.replace("%mp_recovery%", NONE);
						html.replace("%mp_period%", NONE);
						
						switch (grade)
						{
							case 1:
								html.replace("%change_mp%", MP_GRADE_1);
								break;
							
							case 2:
								html.replace("%change_mp%", (isSCH ? MP_GRADE_2_SCH : MP_GRADE_2));
								break;
							
							case 3:
								html.replace("%change_mp%", (isSCH ? MP_GRADE_3_SCH : MP_GRADE_3));
								break;
						}
					}
					html.replace("%objectId%", getObjectId());
					player.sendPacket(html);
				}
			}
			else if (val.equalsIgnoreCase("other"))
			{
				if (st.hasMoreTokens())
				{
					if (getClanHall().getOwnerId() == 0)
						return;
					
					val = st.nextToken();
					
					if (val.equalsIgnoreCase("item_cancel"))
					{
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile("data/html/clanHallManager/functions-cancel.htm");
						html.replace("%apply%", "other item 0");
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("tele_cancel"))
					{
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile("data/html/clanHallManager/functions-cancel.htm");
						html.replace("%apply%", "other tele 0");
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("support_cancel"))
					{
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile("data/html/clanHallManager/functions-cancel.htm");
						html.replace("%apply%", "other support 0");
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("edit_item"))
					{
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile("data/html/clanHallManager/functions-apply.htm");
						html.replace("%name%", "Équipement magique (fabrication d'objets)");
						
						int level = Integer.parseInt(st.nextToken());
						final int funcLvl = level;
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_CREATE_ITEM, level);
						final int cost = ClanHallDecoData.getInstance().getDecoFee(ClanHall.FUNC_CREATE_ITEM, level);
						if (level > 10)
							level -= 10;
						
						html.replace("%cost%", cost + "</font> Adena / " + days + " jour(s)</font>");
						html.replace("%use%", "Permet d'acheter des objets spéciaux à intervalles réguliers.");
						html.replace("%apply%", "other item " + funcLvl);
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("edit_support"))
					{
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile("data/html/clanHallManager/functions-apply.htm");
						html.replace("%name%", "Insigne (magie de soutien)");
						
						int level = Integer.parseInt(st.nextToken());
						final int funcLvl = level;
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_SUPPORT_MAGIC, level);
						final int cost = ClanHallDecoData.getInstance().getDecoFee(ClanHall.FUNC_SUPPORT_MAGIC, level);
						if (level > 10)
							level -= 10;
						
						html.replace("%cost%", cost + "</font> Adena / " + days + " jour(s)</font>");
						html.replace("%use%", "Permet de recevoir la magie de soutien de la gérance.");
						html.replace("%apply%", "other support " + funcLvl);
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("edit_tele"))
					{
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile("data/html/clanHallManager/functions-apply.htm");
						html.replace("%name%", "Miroir (téléportation)");
						
						int level = Integer.parseInt(st.nextToken());
						final int funcLvl = level;
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_TELEPORT, level);
						final int cost = ClanHallDecoData.getInstance().getDecoFee(ClanHall.FUNC_TELEPORT, level);
						if (level > 10)
							level -= 10;
						
						html.replace("%cost%", cost + "</font> Adena / " + days + " jour(s)</font>");
						html.replace("%use%", "Téléporte les membres du clan vers les destinations de <font color=\"00FFFF\">niveau " + level + "</font>");
						html.replace("%apply%", "other tele " + funcLvl);
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("item"))
					{
						if (getClanHall().getOwnerId() == 0)
							return;
						
						int level = Integer.parseInt(st.nextToken());
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_CREATE_ITEM, level);
						final int cost = ClanHallDecoData.getInstance().getDecoFee(ClanHall.FUNC_CREATE_ITEM, level);
						if (level > 10)
							level -= 10;
						
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						
						final ClanHallFunction chf = getClanHall().getFunction(ClanHall.FUNC_CREATE_ITEM);
						if (chf != null && chf.getLvl() == level)
						{
							html.setFile("data/html/clanHallManager/functions-used.htm");
							html.replace("%val%", "Niveau " + val);
							html.replace("%objectId%", getObjectId());
							player.sendPacket(html);
							return;
						}
						
						html.setFile("data/html/clanHallManager/functions-apply_confirmed.htm");
						
						int fee = cost;
						if (level == 0)
						{
							fee = 0;
							html.setFile("data/html/clanHallManager/functions-cancel_confirmed.htm");
						}
						
						if (!getClanHall().updateFunction(player, ClanHall.FUNC_CREATE_ITEM, level, fee, TimeUnit.DAYS.toMillis(days)))
							html.setFile("data/html/clanHallManager/low_adena.htm");
						else
							revalidateDeco(player);
						
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("tele"))
					{
						int level = Integer.parseInt(st.nextToken());
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_TELEPORT, level);
						final int cost = ClanHallDecoData.getInstance().getDecoFee(ClanHall.FUNC_TELEPORT, level);
						if (level > 10)
							level -= 10;
						
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						
						final ClanHallFunction chf = getClanHall().getFunction(ClanHall.FUNC_TELEPORT);
						if (chf != null && chf.getLvl() == level)
						{
							html.setFile("data/html/clanHallManager/functions-used.htm");
							html.replace("%val%", "Niveau " + level);
							html.replace("%objectId%", getObjectId());
							player.sendPacket(html);
							return;
						}
						
						html.setFile("data/html/clanHallManager/functions-apply_confirmed.htm");
						
						int fee = cost;
						if (level == 0)
						{
							fee = 0;
							html.setFile("data/html/clanHallManager/functions-cancel_confirmed.htm");
						}
						
						if (!getClanHall().updateFunction(player, ClanHall.FUNC_TELEPORT, level, fee, TimeUnit.DAYS.toMillis(days)))
							html.setFile("data/html/clanHallManager/low_adena.htm");
						else
							revalidateDeco(player);
						
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("support"))
					{
						int level = Integer.parseInt(st.nextToken());
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_SUPPORT_MAGIC, level);
						final int cost = ClanHallDecoData.getInstance().getDecoFee(ClanHall.FUNC_SUPPORT_MAGIC, level);
						if (level > 10)
							level -= 10;
						
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						
						final ClanHallFunction chf = getClanHall().getFunction(ClanHall.FUNC_SUPPORT_MAGIC);
						if (chf != null && chf.getLvl() == level)
						{
							html.setFile("data/html/clanHallManager/functions-used.htm");
							html.replace("%val%", "Niveau " + val);
							html.replace("%objectId%", getObjectId());
							player.sendPacket(html);
							return;
						}
						
						html.setFile("data/html/clanHallManager/functions-apply_confirmed.htm");
						
						int fee = cost;
						if (level == 0)
						{
							fee = 0;
							html.setFile("data/html/clanHallManager/functions-cancel_confirmed.htm");
						}
						
						if (!getClanHall().updateFunction(player, ClanHall.FUNC_SUPPORT_MAGIC, level, fee, TimeUnit.DAYS.toMillis(days)))
						{
							html.setFile("data/html/clanHallManager/low_adena.htm");
						}
						else
						{
							getAI().resetBuffCheckTime();
							revalidateDeco(player);
						}
						
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
				}
				else
				{
					final boolean isSCH = (getClanHall() instanceof SiegableHall);
					final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
					html.setFile("data/html/clanHallManager/edit_other.htm");
					
					ClanHallFunction chf = getClanHall().getFunction(ClanHall.FUNC_TELEPORT);
					if (chf != null)
					{
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_TELEPORT, chf.getFuncLvl());
						html.replace("%tele%", "Niveau " + chf.getLvl() + "</font> (<font color=\"FFAABB\">" + chf.getLease() + "</font> Adena / " + days + " jour(s))");
						html.replace("%tele_period%", "Prochain prélèvement le " + new SimpleDateFormat("dd-MM-yyyy HH:mm").format(chf.getEndTime()));
						html.replace("%change_tele%", REMOVE_TELE + (isSCH ? TELE_SCH : TELE));
					}
					else
					{
						html.replace("%tele%", NONE);
						html.replace("%tele_period%", NONE);
						html.replace("%change_tele%", (isSCH ? TELE_SCH : TELE));
					}
					
					final int grade = getClanHall().getGrade();
					
					chf = getClanHall().getFunction(ClanHall.FUNC_SUPPORT_MAGIC);
					if (chf != null)
					{
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_SUPPORT_MAGIC, chf.getFuncLvl());
						html.replace("%support%", "Niveau " + chf.getLvl() + "</font> (<font color=\"FFAABB\">" + chf.getLease() + "</font> Adena / " + days + " jour(s))");
						html.replace("%support_period%", "Prochain prélèvement le " + new SimpleDateFormat("dd-MM-yyyy HH:mm").format(chf.getEndTime()));
						
						switch (grade)
						{
							case 1:
								html.replace("%change_support%", REMOVE_SUPPORT + SUPPORT_GRADE_1);
								break;
							
							case 2:
								html.replace("%change_support%", REMOVE_SUPPORT + (isSCH ? SUPPORT_GRADE_2_SCH : SUPPORT_GRADE_2));
								break;
							
							case 3:
								html.replace("%change_support%", REMOVE_SUPPORT + (isSCH ? SUPPORT_GRADE_3_SCH : SUPPORT_GRADE_3));
								break;
						}
					}
					else
					{
						html.replace("%support%", NONE);
						html.replace("%support_period%", NONE);
						
						switch (grade)
						{
							case 1:
								html.replace("%change_support%", SUPPORT_GRADE_1);
								break;
							
							case 2:
								html.replace("%change_support%", (isSCH ? SUPPORT_GRADE_2_SCH : SUPPORT_GRADE_2));
								break;
							
							case 3:
								html.replace("%change_support%", (isSCH ? SUPPORT_GRADE_3_SCH : SUPPORT_GRADE_3));
								break;
						}
					}
					
					chf = getClanHall().getFunction(ClanHall.FUNC_CREATE_ITEM);
					if (chf != null)
					{
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_CREATE_ITEM, chf.getFuncLvl());
						html.replace("%item%", "Niveau " + chf.getLvl() + "</font> (<font color=\"FFAABB\">" + chf.getLease() + "</font> Adena / " + days + " jour(s))");
						html.replace("%item_period%", "Prochain prélèvement le " + new SimpleDateFormat("dd-MM-yyyy HH:mm").format(chf.getEndTime()));
						html.replace("%change_item%", REMOVE_ITEM + (isSCH ? ITEM_SCH : ITEM));
					}
					else
					{
						html.replace("%item%", NONE);
						html.replace("%item_period%", NONE);
						html.replace("%change_item%", (isSCH ? ITEM_SCH : ITEM));
					}
					html.replace("%objectId%", getObjectId());
					player.sendPacket(html);
				}
			}
			else if (val.equalsIgnoreCase("deco"))
			{
				if (st.hasMoreTokens())
				{
					if (getClanHall().getOwnerId() == 0)
						return;
					
					val = st.nextToken();
					if (val.equalsIgnoreCase("curtains_cancel"))
					{
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile("data/html/clanHallManager/functions-cancel.htm");
						html.replace("%apply%", "deco curtains 0");
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("fixtures_cancel"))
					{
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile("data/html/clanHallManager/functions-cancel.htm");
						html.replace("%apply%", "deco fixtures 0");
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("edit_curtains"))
					{
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile("data/html/clanHallManager/functions-apply.htm");
						html.replace("%name%", "Tentures (décoration)");
						
						int level = Integer.parseInt(st.nextToken());
						final int funcLvl = level;
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_DECO_CURTAINS, level);
						final int cost = ClanHallDecoData.getInstance().getDecoFee(ClanHall.FUNC_DECO_CURTAINS, level);
						if (level > 10)
							level -= 10;
						
						html.replace("%cost%", cost + "</font> Adena / " + days + " jour(s)</font>");
						html.replace("%use%", "Des tentures pour décorer la salle de clan.");
						html.replace("%apply%", "deco curtains " + funcLvl);
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("edit_fixtures"))
					{
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile("data/html/clanHallManager/functions-apply.htm");
						html.replace("%name%", "Estrade (décoration)");
						
						int level = Integer.parseInt(st.nextToken());
						final int funcLvl = level;
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_DECO_FIXTURES, level);
						final int cost = ClanHallDecoData.getInstance().getDecoFee(ClanHall.FUNC_DECO_FIXTURES, level);
						if (level > 10)
							level -= 10;
						
						html.replace("%cost%", cost + "</font> Adena / " + days + " jour(s)</font>");
						html.replace("%use%", "Sert à décorer la salle de clan.");
						html.replace("%apply%", "deco fixtures " + funcLvl);
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("curtains"))
					{
						int level = Integer.parseInt(st.nextToken());
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_DECO_CURTAINS, level);
						final int cost = ClanHallDecoData.getInstance().getDecoFee(ClanHall.FUNC_DECO_CURTAINS, level);
						if (level > 10)
							level -= 10;
						
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						
						final ClanHallFunction chf = getClanHall().getFunction(ClanHall.FUNC_DECO_CURTAINS);
						if (chf != null && chf.getLvl() == level)
						{
							html.setFile("data/html/clanHallManager/functions-used.htm");
							html.replace("%val%", "Niveau " + val);
							html.replace("%objectId%", getObjectId());
							player.sendPacket(html);
							return;
						}
						
						html.setFile("data/html/clanHallManager/functions-apply_confirmed.htm");
						
						int fee = cost;
						if (level == 0)
						{
							fee = 0;
							html.setFile("data/html/clanHallManager/functions-cancel_confirmed.htm");
						}
						
						if (!getClanHall().updateFunction(player, ClanHall.FUNC_DECO_CURTAINS, level, fee, TimeUnit.DAYS.toMillis(days)))
							html.setFile("data/html/clanHallManager/low_adena.htm");
						else
							revalidateDeco(player);
						
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
					else if (val.equalsIgnoreCase("fixtures"))
					{
						int level = Integer.parseInt(st.nextToken());
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_DECO_FIXTURES, level);
						final int cost = ClanHallDecoData.getInstance().getDecoFee(ClanHall.FUNC_DECO_FIXTURES, level);
						if (level > 10)
							level -= 10;
						
						final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						
						final ClanHallFunction chf = getClanHall().getFunction(ClanHall.FUNC_DECO_FIXTURES);
						if (chf != null && chf.getLvl() == level)
						{
							html.setFile("data/html/clanHallManager/functions-used.htm");
							html.replace("%val%", "Niveau " + val);
							html.replace("%objectId%", getObjectId());
							player.sendPacket(html);
							return;
						}
						
						html.setFile("data/html/clanHallManager/functions-apply_confirmed.htm");
						
						int fee = cost;
						if (level == 0)
						{
							fee = 0;
							html.setFile("data/html/clanHallManager/functions-cancel_confirmed.htm");
						}
						
						if (!getClanHall().updateFunction(player, ClanHall.FUNC_DECO_FIXTURES, level, fee, TimeUnit.DAYS.toMillis(days)))
							html.setFile("data/html/clanHallManager/low_adena.htm");
						else
							revalidateDeco(player);
						
						html.replace("%objectId%", getObjectId());
						player.sendPacket(html);
					}
				}
				else
				{
					final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
					html.setFile("data/html/clanHallManager/deco.htm");
					
					ClanHallFunction chf = getClanHall().getFunction(ClanHall.FUNC_DECO_CURTAINS);
					if (chf != null)
					{
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_DECO_CURTAINS, chf.getFuncLvl());
						html.replace("%curtain%", "Niveau " + chf.getLvl() + "</font>&nbsp;(<font color=\"FFAABB\">" + chf.getLease() + "</font> Adena / " + days + " jour(s))");
						html.replace("%curtain_period%", "Prochain prélèvement le " + new SimpleDateFormat("dd-MM-yyyy HH:mm").format(chf.getEndTime()));
						html.replace("%change_curtain%", REMOVE_CURTAINS + CURTAINS);
					}
					else
					{
						html.replace("%curtain%", NONE);
						html.replace("%curtain_period%", NONE);
						html.replace("%change_curtain%", CURTAINS);
					}
					
					chf = getClanHall().getFunction(ClanHall.FUNC_DECO_FIXTURES);
					if (chf != null)
					{
						final int days = ClanHallDecoData.getInstance().getDecoDays(ClanHall.FUNC_DECO_FIXTURES, chf.getFuncLvl());
						html.replace("%fixture%", "Niveau " + chf.getLvl() + "</font>&nbsp;(<font color=\"FFAABB\">" + chf.getLease() + "</font> Adena / " + days + " jour(s))");
						html.replace("%fixture_period%", "Prochain prélèvement le " + new SimpleDateFormat("dd-MM-yyyy HH:mm").format(chf.getEndTime()));
						html.replace("%change_fixture%", REMOVE_FIXTURES + FIXTURES);
					}
					else
					{
						html.replace("%fixture%", NONE);
						html.replace("%fixture_period%", NONE);
						html.replace("%change_fixture%", FIXTURES);
					}
					html.replace("%objectId%", getObjectId());
					player.sendPacket(html);
				}
			}
			else if (val.equalsIgnoreCase("back"))
				showChatWindow(player);
			else
			{
				final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
				html.setFile((getClanHall() instanceof SiegableHall) ? "data/html/clanHallManager/manage_sch.htm" : "data/html/clanHallManager/manage.htm");
				html.replace("%objectId%", getObjectId());
				player.sendPacket(html);
			}
		}
		else if (actualCommand.equalsIgnoreCase("support"))
		{
			if (!validatePrivileges(player, PrivilegeType.CHP_USE_FUNCTIONS))
				return;
			
			final ClanHallFunction chf = getClanHall().getFunction(ClanHall.FUNC_SUPPORT_MAGIC);
			if (chf == null || chf.getLvl() == 0)
				return;
			
			if (player.isCursedWeaponEquipped())
			{
				// Custom system message
				player.sendMessage("Le porteur d'une arme maudite ne peut recevoir ni soin ni bienfait.");
				return;
			}
			
			setTarget(player);
			
			try
			{
				final int id = Integer.parseInt(val);
				final int lvl = (st.hasMoreTokens()) ? Integer.parseInt(st.nextToken()) : 0;
				
				getAI().addCastDesire(player, id, lvl, 1000000);
			}
			catch (Exception e)
			{
				player.sendMessage("Compétence invalide, prévenez l'administrateur du serveur.");
			}
		}
		else if (actualCommand.equalsIgnoreCase("list_back"))
		{
			final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
			html.setFile("data/html/clanHallManager/chamberlain.htm");
			html.replace("%npcname%", getName());
			html.replace("%objectId%", getObjectId());
			player.sendPacket(html);
		}
		else if (actualCommand.equalsIgnoreCase("support_back"))
		{
			if (!validatePrivileges(player, PrivilegeType.CHP_USE_FUNCTIONS))
				return;
			
			final ClanHallFunction chf = getClanHall().getFunction(ClanHall.FUNC_SUPPORT_MAGIC);
			if (chf == null || chf.getLvl() == 0)
				return;
			
			final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
			html.setFile("data/html/clanHallManager/support" + chf.getLvl() + ".htm");
			html.replace("%mp%", (int) getStatus().getMp());
			html.replace("%objectId%", getObjectId());
			player.sendPacket(html);
		}
		else if (actualCommand.equalsIgnoreCase("WithdrawC"))
		{
			if (!validatePrivileges(player, PrivilegeType.SP_WAREHOUSE_SEARCH))
			{
				player.sendPacket(SystemMessageId.YOU_DO_NOT_HAVE_THE_RIGHT_TO_USE_CLAN_WAREHOUSE);
				return;
			}
			
			final Clan clan = player.getClan();
			if (clan == null || clan.getLevel() < Config.CLAN_WAREHOUSE_MIN_LEVEL)
			{
				player.sendPacket(SystemMessageId.ONLY_LEVEL_1_CLAN_OR_HIGHER_CAN_USE_WAREHOUSE);
				return;
			}
			
			player.setActiveWarehouse(clan.getWarehouse());
			player.sendPacket(new WarehouseWithdrawList(player, WarehouseWithdrawList.CLAN));
			player.sendPacket(ActionFailed.STATIC_PACKET);
		}
		else if (actualCommand.equalsIgnoreCase("DepositC"))
		{
			final Clan clan = player.getClan();
			if (clan == null || clan.getLevel() < Config.CLAN_WAREHOUSE_MIN_LEVEL)
			{
				player.sendPacket(SystemMessageId.ONLY_LEVEL_1_CLAN_OR_HIGHER_CAN_USE_WAREHOUSE);
				return;
			}
			
			player.setActiveWarehouse(clan.getWarehouse());
			player.tempInventoryDisable();
			player.sendPacket(new WarehouseDepositList(player, WarehouseDepositList.CLAN));
			player.sendPacket(ActionFailed.STATIC_PACKET);
		}
		else
			super.onBypassFeedback(player, command);
	}
	
	@Override
	public void showChatWindow(Player player)
	{
		player.sendPacket(ActionFailed.STATIC_PACKET);
		
		final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
		html.setFile("data/html/clanHallManager/chamberlain" + ((getNpcTalkCond(player) == NpcTalkCond.OWNER) ? ".htm" : "-no.htm"));
		html.replace("%objectId%", getObjectId());
		player.sendPacket(html);
	}
	
	@Override
	protected boolean isTeleportAllowed(Player player)
	{
		return validatePrivileges(player, PrivilegeType.CHP_USE_FUNCTIONS);
	}
	
	@Override
	protected NpcTalkCond getNpcTalkCond(Player player)
	{
		if (getClanHall() != null && player.getClan() != null && getClanHall().getOwnerId() == player.getClanId())
			return NpcTalkCond.OWNER;
		
		return NpcTalkCond.NONE;
	}
	
	
	private static final Map<String, String> FURNITURE_CATEGORIES = Map.of("SEAT", "Siège", "TABLE", "Table", "LIGHT", "Luminaire", "DECOR", "Décoration", "BANNER", "Étendard", "CARPET", "Tapis", "CURTAIN", "Tentures", "PLATFORM", "Estrade", "CHEST", "Coffre");
	
	// Menus de la salle (ajout du projet) : services, ameliorations, charges, securite.
	private static final String BUTTON = "<button value=\"%s\" action=\"bypass -h npc_%d_%s\" width=%d height=21 back=\"L2UI_ch3.Btn1_normalOn\" fore=\"L2UI_ch3.Btn1_normal\">";

	private static String adena(long value)
	{
		return String.format("%,d", value).replace(',', ' ').replace(' ', ' ').replace(' ', ' ');
	}

	private String button(String label, String command, int width)
	{
		return String.format(BUTTON, label, getObjectId(), command, width);
	}

	private void sendHallPage(Player player, String title, String content)
	{
		final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
		html.setFile("data/html/clanHallManager/hall-page.htm");
		html.replace("%title%", title);
		html.replace("%content%", content);
		html.replace("%objectId%", getObjectId());
		player.sendPacket(html);
	}

	private static String state(ClanHall ch, ClanHallUpgradeData.Upgrade upgrade)
	{
		final ClanHallFunction chf = ch.getInstalledFunction(upgrade.type());
		if (chf == null)
			return "<font color=\"808080\">Non installé</font>";

		if (chf.isSuspended())
			return "<font color=\"FF6060\">Suspendu (entrepôt insuffisant)</font>";

		final ClanHallUpgradeData.Tier tier = upgrade.findByLvl(ch.getGrade(), chf.getLvl());
		return "<font color=\"A0E0A0\">" + ((tier != null) ? tier.name() + " : " + tier.effect() : "Niveau " + chf.getLvl()) + "</font>";
	}

	private void handleHallMenu(Player player, String command, String val, StringTokenizer st)
	{
		final ClanHall ch = getClanHall();
		final ClanHallUpgradeData data = ClanHallUpgradeData.getInstance();

		if (command.equalsIgnoreCase("services"))
		{
			final StringBuilder sb = new StringBuilder("Ce que la salle offre à ses membres. Les installations se commandent dans les Améliorations.<br><br>");
			for (ClanHallUpgradeData.Upgrade upgrade : data.getAll())
			{
				sb.append("<table width=270><tr><td width=36><img src=\"icon.clanhall_upgrade_").append(upgrade.type()).append("\" width=32 height=32></td><td width=234>");
				sb.append("<font color=\"LEVEL\">").append(upgrade.name()).append("</font><br1>").append(state(ch, upgrade)).append("</td></tr></table>");

				final String use = switch (upgrade.type())
				{
					case ClanHall.FUNC_TELEPORT -> "functions tele";
					case ClanHall.FUNC_SUPPORT_MAGIC -> "functions support";
					case ClanHall.FUNC_CREATE_ITEM -> "functions item_creation " + getNpcId();
					default -> null;
				};
				if (use != null && ch.getFunction(upgrade.type()) != null)
					sb.append("<center>").append(button("Utiliser : " + upgrade.name(), use, 170)).append("</center>");
				sb.append("<br1>");
			}
			sendHallPage(player, "Services de la salle", sb.toString());
			return;
		}

		if (command.equalsIgnoreCase("charges"))
		{
			final Clan clan = player.getClan();
			final long total = ch.getWeeklyCharges();
			final long balance = (clan != null) ? clan.getWarehouse().getAdena() : 0;
			final long weeks = (total > 0) ? balance / total : 0;

			final StringBuilder sb = new StringBuilder("Tout est prélevé chaque semaine sur l'entrepôt du clan.<br><br><table width=270>");
			sb.append("<tr><td width=170>Loyer de la salle</td><td width=100 align=right>").append(adena(ch.getLease())).append("</td></tr>");
			for (ClanHallUpgradeData.Upgrade upgrade : data.getAll())
			{
				final ClanHallFunction chf = ch.getInstalledFunction(upgrade.type());
				if (chf == null)
					continue;

				final ClanHallUpgradeData.Tier tier = upgrade.findByLvl(ch.getGrade(), chf.getLvl());
				sb.append("<tr><td>").append(upgrade.name()).append((tier != null) ? " (" + tier.name() + ")" : "");
				sb.append(chf.isSuspended() ? " <font color=\"FF6060\">suspendu</font>" : "");
				sb.append("</td><td align=right>").append(adena((long) chf.getLease() * 604800000L / Math.max(1, chf.getRate()))).append("</td></tr>");
			}
			sb.append("<tr><td><font color=\"LEVEL\">Total par semaine</font></td><td align=right><font color=\"LEVEL\">").append(adena(total)).append("</font></td></tr></table><br>");
			sb.append("Entrepôt du clan : <font color=\"LEVEL\">").append(adena(balance)).append(" adena</font><br1>");
			sb.append((weeks >= 2) ? "Assez pour environ <font color=\"A0E0A0\">" + weeks + " semaines</font>." : "<font color=\"FF6060\">Attention : l'entrepôt couvre moins de deux semaines.</font>");
			sb.append("<br1>Prochain loyer le ").append(new SimpleDateFormat("dd/MM/yyyy HH:mm").format(ch.getPaidUntil())).append(".");
			sendHallPage(player, "Charges de la salle", sb.toString());
			return;
		}

		if (command.equalsIgnoreCase("security"))
		{
			final String content = "Tant que la porte est ouverte, n'importe qui peut entrer. Les intrus sont reconduits dehors d'un seul ordre.<br><br><center>"
				+ button("Ouvrir la porte", "door open", 150) + "<br>" + button("Fermer la porte", "door close", 150) + "<br>"
				+ button("Expulser les intrus", "banish_foreigner list", 150) + "</center>";
			sendHallPage(player, "Porte et sécurité", content);
			return;
		}

		// Ameliorations : commander, changer de niveau, retirer.
		if (!validatePrivileges(player, PrivilegeType.CHP_SET_FUNCTIONS))
			return;

		String message = null;
		try
		{
			if (command.equalsIgnoreCase("upgrade"))
			{
				final ClanHallUpgradeData.Upgrade upgrade = data.get(Integer.parseInt(val));
				final ClanHallUpgradeData.Tier tier = upgrade.find(ch.getGrade(), Integer.parseInt(st.nextToken()));
				final long balance = player.getClan().getWarehouse().getAdena();
				final String content = "<font color=\"LEVEL\">" + upgrade.name() + " : " + tier.name() + "</font><br1>" + tier.effect() + "<br><br>"
					+ "Coût : <font color=\"LEVEL\">" + adena(tier.price()) + " adena par semaine</font>, prélevés sur l'entrepôt du clan.<br1>"
					+ "La première semaine est payée tout de suite. Un changement de niveau repart pour une semaine entière.<br1>"
					+ "Entrepôt du clan : " + adena(balance) + " adena.<br><br><center>"
					+ button("Valider", "upgrade_apply " + upgrade.type() + " " + tier.tier(), 120) + "<br>" + button("Retour", "upgrades", 120) + "</center>";
				sendHallPage(player, "Commander une amélioration", content);
				return;
			}

			if (command.equalsIgnoreCase("upgrade_remove"))
			{
				final ClanHallUpgradeData.Upgrade upgrade = data.get(Integer.parseInt(val));
				final String content = "Retirer l'installation <font color=\"LEVEL\">" + upgrade.name() + "</font> ?<br1>La semaine en cours n'est pas remboursée.<br><br><center>"
					+ button("Retirer", "upgrade_remove_apply " + upgrade.type(), 120) + "<br>" + button("Retour", "upgrades", 120) + "</center>";
				sendHallPage(player, "Retirer une installation", content);
				return;
			}

			if (command.equalsIgnoreCase("upgrade_apply"))
			{
				final ClanHallUpgradeData.Upgrade upgrade = data.get(Integer.parseInt(val));
				final ClanHallUpgradeData.Tier tier = upgrade.find(ch.getGrade(), Integer.parseInt(st.nextToken()));
				message = ch.installUpgrade(player.getClan(), upgrade.type(), tier.lvl(), tier.price()) ? upgrade.name() + " : " + tier.name() + " installé." : "L'entrepôt du clan ne contient pas assez d'adena.";
			}
			else if (command.equalsIgnoreCase("upgrade_remove_apply"))
			{
				final ClanHallFunction chf = ch.getInstalledFunction(Integer.parseInt(val));
				if (chf != null)
				{
					chf.removeFunction();
					ch.refreshDecoration();
					message = "Installation retirée.";
				}
			}
		}
		catch (Exception e)
		{
			message = "Commande invalide.";
		}

		final StringBuilder sb = new StringBuilder();
		if (message != null)
			sb.append("<font color=\"LEVEL\">").append(message).append("</font><br><br>");
		sb.append("Chaque installation se voit dans la salle et se paie chaque semaine sur l'entrepôt du clan.<br><br>");

		for (ClanHallUpgradeData.Upgrade upgrade : data.getAll())
		{
			final ClanHallFunction chf = ch.getInstalledFunction(upgrade.type());
			final ClanHallUpgradeData.Tier current = (chf != null) ? upgrade.findByLvl(ch.getGrade(), chf.getLvl()) : null;

			sb.append("<table width=270><tr><td width=36><img src=\"icon.clanhall_upgrade_").append(upgrade.type()).append("\" width=32 height=32></td><td width=234>");
			sb.append("<font color=\"LEVEL\">").append(upgrade.name()).append("</font><br1><font color=\"A0A0A0\">").append(upgrade.effect()).append("</font><br1>").append(state(ch, upgrade)).append("</td></tr></table>");

			for (ClanHallUpgradeData.Tier tier : upgrade.forGrade(ch.getGrade()))
			{
				final String label = tier.name() + " · " + adena(tier.price());
				if (current != null && current.tier() == tier.tier())
					sb.append("<font color=\"LEVEL\">[").append(label).append("]</font> ");
				else
					sb.append("<a action=\"bypass -h npc_").append(getObjectId()).append("_upgrade ").append(upgrade.type()).append(" ").append(tier.tier()).append("\">").append(label).append("</a> ");
			}
			if (chf != null)
				sb.append("<a action=\"bypass -h npc_").append(getObjectId()).append("_upgrade_remove ").append(upgrade.type()).append("\">Retirer</a>");
			sb.append("<br><br>");
		}
		sendHallPage(player, "Améliorations de la salle", sb.toString());
	}
	
	// Amenagement de la salle (ajout du projet) : liste des emplacements, choix d'un
	// meuble de l'inventaire, reprise vers l'entrepot du clan.
	private void handleFurnish(Player player, String command, String val, StringTokenizer st)
	{
		if (command.equalsIgnoreCase("furniture_shop"))
		{
			showBuyWindow(player, 950110);
			return;
		}
		
		if (!validatePrivileges(player, PrivilegeType.CHP_FURNISH))
			return;
		
		final ClanHallFurnitureManager manager = ClanHallFurnitureManager.getInstance();
		final ClanHall ch = getClanHall();
		String message = null;
		
		try
		{
			if (command.equalsIgnoreCase("furnish_slot"))
			{
				showFurnitureChoice(player, Integer.parseInt(val));
				return;
			}
			else if (command.equalsIgnoreCase("furnish_place"))
				message = manager.place(player, ch, Integer.parseInt(val), Integer.parseInt(st.nextToken()));
			else if (command.equalsIgnoreCase("furnish_remove"))
			{
				message = manager.remove(ch, Integer.parseInt(val));
				if (message == null)
					message = "Le meuble a été rangé dans l'entrepôt du clan.";
			}
		}
		catch (Exception e)
		{
			message = "Commande invalide.";
		}
		
		final StringBuilder sb = new StringBuilder();
		final Map<Integer, Integer> placed = manager.getPlaced(ch.getId());
		for (ClanHallFurnitureManager.Slot slot : manager.getSlots(ch.getId()).values())
		{
			final Integer itemId = placed.get(slot.id());
			sb.append("<tr><td width=90><font color=\"999999\">").append(FURNITURE_CATEGORIES.getOrDefault(slot.category(), slot.category())).append(" ").append(manager.getOrdinal(ch.getId(), slot.id())).append("</font></td><td width=180>");
			if (itemId == null)
				sb.append("<a action=\"bypass -h npc_").append(getObjectId()).append("_furnish_slot ").append(slot.id()).append("\">Poser un meuble</a>");
			else
				sb.append(ItemData.getInstance().getTemplate(itemId).getName()).append(" <a action=\"bypass -h npc_").append(getObjectId()).append("_furnish_remove ").append(slot.id()).append("\">[Reprendre]</a>");
			sb.append("</td></tr>");
		}
		
		final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
		html.setFile("data/html/clanHallManager/furnish.htm");
		html.replace("%message%", (message != null) ? "<font color=\"LEVEL\">" + message + "</font><br>" : "");
		html.replace("%slots%", (sb.length() > 0) ? sb.toString() : "<tr><td>Cette salle n'a pas encore d'emplacement pour le mobilier.</td></tr>");
		html.replace("%objectId%", getObjectId());
		player.sendPacket(html);
	}
	
	private void showFurnitureChoice(Player player, int slotId)
	{
		final ClanHallFurnitureManager.Slot slot = ClanHallFurnitureManager.getInstance().getSlots(getClanHall().getId()).get(slotId);
		if (slot == null)
			return;
		
		final StringBuilder sb = new StringBuilder();
		for (ItemInstance item : player.getInventory().getItems())
		{
			if (slot.category().equals(ClanHallFurnitureManager.getInstance().getCategory(item.getItemId())))
				sb.append("<a action=\"bypass -h npc_").append(getObjectId()).append("_furnish_place ").append(slotId).append(" ").append(item.getObjectId()).append("\">").append(item.getName()).append("</a><br>");
		}
		
		final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
		html.setFile("data/html/clanHallManager/furnish-choice.htm");
		html.replace("%category%", FURNITURE_CATEGORIES.getOrDefault(slot.category(), slot.category()) + " " + ClanHallFurnitureManager.getInstance().getOrdinal(getClanHall().getId(), slotId));
		html.replace("%items%", (sb.length() > 0) ? sb.toString() : "Vous n'avez aucun meuble de ce type.<br>Le catalogue du mobilier est disponible auprès de moi.<br>");
		html.replace("%objectId%", getObjectId());
		player.sendPacket(html);
	}
	private void revalidateDeco(Player player)
	{
		getClanHall().getZone().broadcastPacket(new ClanHallDecoration(getClanHall()));
	}
	
	private boolean validatePrivileges(Player player, PrivilegeType privilege)
	{
		if (!player.hasClanPrivileges(privilege))
		{
			final NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
			html.setFile("data/html/clanHallManager/not_authorized.htm");
			player.sendPacket(html);
			return false;
		}
		return true;
	}
}