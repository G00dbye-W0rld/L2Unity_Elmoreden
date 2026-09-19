package com.shnok.javaserver.gameserver.data.manager;

import java.nio.file.Path;
import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;

import com.shnok.javaserver.commons.data.StatSet;
import com.shnok.javaserver.commons.data.xml.IXmlReader;
import com.shnok.javaserver.commons.pool.ConnectionPool;

import com.shnok.javaserver.gameserver.data.sql.ClanTable;
import com.shnok.javaserver.gameserver.data.xml.NpcData;
import com.shnok.javaserver.gameserver.model.actor.Npc;
import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.model.actor.instance.ClanHallChest;
import com.shnok.javaserver.gameserver.model.actor.instance.ClanHallWorkbench;
import com.shnok.javaserver.gameserver.model.actor.template.NpcTemplate;
import com.shnok.javaserver.gameserver.model.item.instance.ItemInstance;
import com.shnok.javaserver.gameserver.model.pledge.Clan;
import com.shnok.javaserver.gameserver.model.residence.clanhall.ClanHall;
import com.shnok.javaserver.gameserver.model.spawn.Spawn;
import com.shnok.javaserver.gameserver.network.serverpackets.ExClanHallFurniture;

import org.w3c.dom.Document;

/**
 * Mobilier des salles de clan (ajout du projet). Chaque salle offre des emplacements
 * d'une categorie donnee ; un meuble pose est un objet retire de l'inventaire, qui
 * retourne a l'entrepot du clan quand on le reprend ou que la salle change de mains.
 */
public class ClanHallFurnitureManager implements IXmlReader
{
	public static final String CHEST = "CHEST";
	private static final int CHEST_NPC_ID = 50112;
	private static final int WORKBENCH_NPC_ID = 50113;

	private static final String CREATE_TABLE = "CREATE TABLE IF NOT EXISTS clanhall_furniture (hall_id INT NOT NULL, slot_id INT NOT NULL, item_id INT NOT NULL, PRIMARY KEY (hall_id, slot_id))";
	private static final String LOAD = "SELECT hall_id, slot_id, item_id FROM clanhall_furniture";
	private static final String INSERT = "REPLACE INTO clanhall_furniture (hall_id, slot_id, item_id) VALUES (?,?,?)";
	private static final String DELETE = "DELETE FROM clanhall_furniture WHERE hall_id=? AND slot_id=?";

	public record Slot(int id, String category, int x, int y, int z, int heading)
	{
	}

	private final Map<Integer, String> _catalog = new HashMap<>();
	private final Map<Integer, Map<Integer, Slot>> _slots = new HashMap<>();
	private final Map<Integer, Map<Integer, Integer>> _placed = new HashMap<>();
	private final Map<Integer, Spawn> _chests = new HashMap<>();
	private final Map<Integer, int[]> _workbenchLocs = new HashMap<>();
	private final Map<Integer, Spawn> _workbenches = new HashMap<>();

	protected ClanHallFurnitureManager()
	{
		load();
		restore();
	}

	@Override
	public void load()
	{
		parseFile("data/xml/clanHallFurniture.xml");
		LOGGER.info("Loaded {} furniture items and {} furnished clan halls.", _catalog.size(), _slots.size());
	}

	@Override
	public void parseDocument(Document doc, Path path)
	{
		forEach(doc, "list", listNode ->
		{
			forEach(listNode, "catalog", catalogNode -> forEach(catalogNode, "furniture", node ->
			{
				final StatSet set = parseAttributes(node);
				_catalog.put(set.getInteger("itemId"), set.getString("category"));
			}));

			forEach(listNode, "hall", hallNode ->
			{
				final int hallId = parseInteger(hallNode.getAttributes(), "id");
				final Map<Integer, Slot> slots = new TreeMap<>();
				forEach(hallNode, "slot", node ->
				{
					final StatSet set = parseAttributes(node);
					slots.put(set.getInteger("id"), new Slot(set.getInteger("id"), set.getString("category"), set.getInteger("x"), set.getInteger("y"), set.getInteger("z"), set.getInteger("heading", 0)));
				});
				_slots.put(hallId, slots);
			});
			
			// Etabli du decor : position ecrite par l'outil de disposition du decor.
			forEach(listNode, "workbench", node ->
			{
				final StatSet set = parseAttributes(node);
				_workbenchLocs.put(set.getInteger("hall"), new int[] { set.getInteger("x"), set.getInteger("y"), set.getInteger("z") });
			});
		});
	}

	private void restore()
	{
		try (Connection con = ConnectionPool.getConnection())
		{
			try (PreparedStatement ps = con.prepareStatement(CREATE_TABLE))
			{
				ps.execute();
			}

			try (PreparedStatement ps = con.prepareStatement(LOAD);
				ResultSet rs = ps.executeQuery())
			{
				while (rs.next())
					_placed.computeIfAbsent(rs.getInt("hall_id"), k -> new TreeMap<>()).put(rs.getInt("slot_id"), rs.getInt("item_id"));
			}
		}
		catch (Exception e)
		{
			LOGGER.error("Couldn't load clan hall furniture.", e);
		}

		// Tentures et estrade sont devenues des meubles : les anciennes installations
		// de decoration (toujours facturees) sont retirees.
		for (ClanHall ch : ClanHallManager.getInstance().getClanHalls().values())
			for (int type : new int[] { ClanHall.FUNC_DECO_CURTAINS, ClanHall.FUNC_DECO_FIXTURES })
				if (ch.getInstalledFunction(type) != null)
					ch.getInstalledFunction(type).removeFunction();

		for (Map.Entry<Integer, int[]> entry : _workbenchLocs.entrySet())
			spawnWorkbench(entry.getKey(), entry.getValue());
		
		for (Map.Entry<Integer, Map<Integer, Integer>> hall : _placed.entrySet())
			for (Map.Entry<Integer, Integer> placed : hall.getValue().entrySet())
				if (CHEST.equals(getCategory(placed.getValue())))
					spawnChest(hall.getKey(), placed.getKey());
	}

	public String getCategory(int itemId)
	{
		return _catalog.get(itemId);
	}

	public boolean isFurniture(int itemId)
	{
		return _catalog.containsKey(itemId);
	}

	public Map<Integer, Slot> getSlots(int hallId)
	{
		return _slots.getOrDefault(hallId, Collections.emptyMap());
	}

	/**
	 * Numero de l'emplacement dans sa categorie (1, 2...), dans l'ordre des identifiants :
	 * le client numerote ses etiquettes de la meme facon.
	 */
	public int getOrdinal(int hallId, int slotId)
	{
		final Slot slot = getSlots(hallId).get(slotId);
		if (slot == null)
			return 0;
		
		int ordinal = 0;
		for (Slot other : getSlots(hallId).values())
		{
			if (other.category().equals(slot.category()))
				ordinal++;
			if (other.id() == slotId)
				break;
		}
		return ordinal;
	}
	
	public Map<Integer, Integer> getPlaced(int hallId)
	{
		return _placed.getOrDefault(hallId, Collections.emptyMap());
	}

	public int getWorkbenchObjectId(int hallId)
	{
		final Spawn spawn = _workbenches.get(hallId);
		return (spawn != null && spawn.getNpc() != null) ? spawn.getNpc().getObjectId() : 0;
	}
	
	public int getChestObjectId(int hallId, int slotId)
	{
		final Spawn spawn = _chests.get(key(hallId, slotId));
		return (spawn != null && spawn.getNpc() != null) ? spawn.getNpc().getObjectId() : 0;
	}

	/**
	 * Pose un meuble de l'inventaire du joueur sur un emplacement libre de la bonne categorie.
	 * @return un message d'erreur, ou null en cas de reussite.
	 */
	public synchronized String place(Player player, ClanHall ch, int slotId, int itemObjectId)
	{
		final Slot slot = getSlots(ch.getId()).get(slotId);
		if (slot == null)
			return "Cet emplacement n'existe pas.";

		if (getPlaced(ch.getId()).containsKey(slotId))
			return "Cet emplacement est déjà occupé.";

		final ItemInstance item = player.getInventory().getItemByObjectId(itemObjectId);
		if (item == null || !slot.category().equals(getCategory(item.getItemId())))
			return "Ce meuble ne convient pas à cet emplacement.";

		if (CHEST.equals(slot.category()) && hasChest(ch.getId()))
			return "La salle a déjà un coffre.";

		final int itemId = item.getItemId();
		if (!player.destroyItem(item, 1, true))
			return "Ce meuble ne peut pas être posé.";

		_placed.computeIfAbsent(ch.getId(), k -> new TreeMap<>()).put(slotId, itemId);
		save(ch.getId(), slotId, itemId);

		if (CHEST.equals(slot.category()))
			spawnChest(ch.getId(), slotId);

		broadcast(ch);
		return null;
	}

	/**
	 * Reprend un meuble : il retourne dans l'entrepot du clan proprietaire.
	 */
	public synchronized String remove(ClanHall ch, int slotId)
	{
		final Integer itemId = getPlaced(ch.getId()).get(slotId);
		if (itemId == null)
			return "Cet emplacement est vide.";

		final Clan clan = ClanTable.getInstance().getClan(ch.getOwnerId());
		release(ch, slotId, itemId, clan);
		broadcast(ch);
		return null;
	}

	/**
	 * La salle change de mains : tout le mobilier retourne a l'ancien clan proprietaire.
	 */
	public synchronized void releaseAll(ClanHall ch, Clan previousOwner)
	{
		final Map<Integer, Integer> placed = _placed.get(ch.getId());
		if (placed == null || placed.isEmpty())
			return;

		for (Map.Entry<Integer, Integer> entry : new ArrayList<>(placed.entrySet()))
			release(ch, entry.getKey(), entry.getValue(), previousOwner);

		broadcast(ch);
	}

	private void release(ClanHall ch, int slotId, int itemId, Clan clan)
	{
		_placed.get(ch.getId()).remove(slotId);
		delete(ch.getId(), slotId);

		final Spawn chest = _chests.remove(key(ch.getId(), slotId));
		if (chest != null)
			chest.doDelete();

		if (clan != null)
			clan.getWarehouse().addItem(itemId, 1);
	}

	private boolean hasChest(int hallId)
	{
		for (int itemId : getPlaced(hallId).values())
			if (CHEST.equals(getCategory(itemId)))
				return true;

		return false;
	}

	// Le coffre est un PNJ d'entrepot invisible, pose a l'emplacement du meuble :
	// le serveur exige un PNJ d'entrepot a portee pour tout depot ou retrait.
	private void spawnChest(int hallId, int slotId)
	{
		final Slot slot = getSlots(hallId).get(slotId);
		final NpcTemplate template = NpcData.getInstance().getTemplate(CHEST_NPC_ID);
		if (slot == null || template == null)
		{
			LOGGER.warn("Couldn't spawn clan hall chest (hall {}, slot {}).", hallId, slotId);
			return;
		}

		try
		{
			final Spawn spawn = new Spawn(template);
			spawn.setLoc(slot.x(), slot.y(), slot.z(), slot.heading());
			final Npc npc = spawn.doSpawn(false);
			if (npc instanceof ClanHallChest chest)
				chest.setClanHallId(hallId);

			_chests.put(key(hallId, slotId), spawn);
		}
		catch (Exception e)
		{
			LOGGER.error("Couldn't spawn clan hall chest.", e);
		}
	}

	private void spawnWorkbench(int hallId, int[] loc)
	{
		final NpcTemplate template = NpcData.getInstance().getTemplate(WORKBENCH_NPC_ID);
		if (template == null)
			return;
		
		try
		{
			final Spawn spawn = new Spawn(template);
			spawn.setLoc(loc[0], loc[1], loc[2], 0);
			if (spawn.doSpawn(false) instanceof ClanHallWorkbench workbench)
				workbench.setClanHallId(hallId);
			_workbenches.put(hallId, spawn);
		}
		catch (Exception e)
		{
			LOGGER.error("Couldn't spawn clan hall workbench.", e);
		}
	}
	
	public void broadcast(ClanHall ch)
	{
		if (ch.getZone() != null)
			ch.getZone().broadcastPacket(new ExClanHallFurniture(ch.getId()));
	}

	private static int key(int hallId, int slotId)
	{
		return hallId * 1000 + slotId;
	}

	private static void save(int hallId, int slotId, int itemId)
	{
		try (Connection con = ConnectionPool.getConnection();
			PreparedStatement ps = con.prepareStatement(INSERT))
		{
			ps.setInt(1, hallId);
			ps.setInt(2, slotId);
			ps.setInt(3, itemId);
			ps.execute();
		}
		catch (Exception e)
		{
			LOGGER.error("Couldn't save clan hall furniture.", e);
		}
	}

	private static void delete(int hallId, int slotId)
	{
		try (Connection con = ConnectionPool.getConnection();
			PreparedStatement ps = con.prepareStatement(DELETE))
		{
			ps.setInt(1, hallId);
			ps.setInt(2, slotId);
			ps.execute();
		}
		catch (Exception e)
		{
			LOGGER.error("Couldn't delete clan hall furniture.", e);
		}
	}

	public static ClanHallFurnitureManager getInstance()
	{
		return SingletonHolder.INSTANCE;
	}

	private static class SingletonHolder
	{
		protected static final ClanHallFurnitureManager INSTANCE = new ClanHallFurnitureManager();
	}
}
