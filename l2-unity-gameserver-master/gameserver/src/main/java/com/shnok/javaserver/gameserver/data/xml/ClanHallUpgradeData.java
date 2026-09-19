package com.shnok.javaserver.gameserver.data.xml;

import java.nio.file.Path;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import com.shnok.javaserver.commons.data.StatSet;
import com.shnok.javaserver.commons.data.xml.IXmlReader;

import org.w3c.dom.Document;

/**
 * Installations des salles de clan (ajout du projet) : pour chaque installation et
 * chaque categorie de salle, trois niveaux avec leur effet et leur prix hebdomadaire.
 */
public class ClanHallUpgradeData implements IXmlReader
{
	public record Tier(int grade, int tier, String name, int lvl, int price, String effect)
	{
	}

	public record Upgrade(int type, String name, String effect, List<Tier> tiers)
	{
		public List<Tier> forGrade(int grade)
		{
			final List<Tier> list = new ArrayList<>();
			for (Tier t : tiers)
				if (t.grade() == grade)
					list.add(t);
			return list;
		}

		public Tier find(int grade, int tier)
		{
			for (Tier t : tiers)
				if (t.grade() == grade && t.tier() == tier)
					return t;
			return null;
		}

		/** Le niveau installe, retrouve depuis la valeur interne du serveur. */
		public Tier findByLvl(int grade, int lvl)
		{
			for (Tier t : tiers)
				if (t.grade() == grade && t.lvl() == lvl)
					return t;
			return null;
		}
	}

	private final Map<Integer, Upgrade> _upgrades = new LinkedHashMap<>();

	protected ClanHallUpgradeData()
	{
		load();
	}

	@Override
	public void load()
	{
		parseFile("data/xml/clanHallUpgrades.xml");
		LOGGER.info("Loaded {} clan hall upgrades.", _upgrades.size());
	}

	@Override
	public void parseDocument(Document doc, Path path)
	{
		forEach(doc, "list", listNode -> forEach(listNode, "upgrade", upgradeNode ->
		{
			final StatSet set = parseAttributes(upgradeNode);
			final List<Tier> tiers = new ArrayList<>();
			forEach(upgradeNode, "tier", tierNode ->
			{
				final StatSet t = parseAttributes(tierNode);
				tiers.add(new Tier(t.getInteger("grade"), t.getInteger("tier"), t.getString("name"), t.getInteger("lvl"), t.getInteger("price"), t.getString("effect")));
			});
			_upgrades.put(set.getInteger("type"), new Upgrade(set.getInteger("type"), set.getString("name"), set.getString("effect"), tiers));
		}));
	}

	public Upgrade get(int type)
	{
		return _upgrades.get(type);
	}

	public Iterable<Upgrade> getAll()
	{
		return _upgrades.values();
	}

	public static ClanHallUpgradeData getInstance()
	{
		return SingletonHolder.INSTANCE;
	}

	private static class SingletonHolder
	{
		protected static final ClanHallUpgradeData INSTANCE = new ClanHallUpgradeData();
	}
}
