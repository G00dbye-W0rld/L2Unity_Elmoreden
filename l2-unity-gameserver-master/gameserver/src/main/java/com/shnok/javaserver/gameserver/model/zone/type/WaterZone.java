package com.shnok.javaserver.gameserver.model.zone.type;

import com.shnok.javaserver.gameserver.enums.ZoneId;
import com.shnok.javaserver.gameserver.enums.actors.MoveType;
import com.shnok.javaserver.gameserver.geoengine.GeoEngine;
import com.shnok.javaserver.gameserver.model.actor.Creature;
import com.shnok.javaserver.gameserver.model.actor.Npc;
import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.model.zone.type.subtype.ZoneType;
import com.shnok.javaserver.gameserver.network.serverpackets.actor.AbstractNpcInfo.NpcInfo;
import com.shnok.javaserver.gameserver.network.serverpackets.actor.ChangeMoveType;
import com.shnok.javaserver.gameserver.network.serverpackets.unused.ServerObjectInfo;

/**
 * A zone extending {@link ZoneType}, used for the water behavior. {@link Player}s can drown if they stay too long below water line.
 */
public class WaterZone extends ZoneType
{
	// Profondeur minimale (fraction de la CollisionHeight du personnage) pour
	// declencher la nage. Sans ce seuil, la zone (un simple cuboide plat entre
	// minZ/maxZ) considere "dans l'eau" des l'entree sur la plage, y compris
	// en eau tres peu profonde - le personnage se retrouvait a nager au-dessus
	// du sol visible pres du rivage. Ajustable ici si le ressenti ne convient
	// pas (0.7 = nage a peu pres quand l'eau atteint la poitrine).
	private static final double SWIM_DEPTH_RATIO = 0.7;

	public WaterZone(int id)
	{
		super(id);
	}

	@Override
	protected void onEnter(Creature creature)
	{
		// Marque uniquement l'appartenance geometrique a la zone (utilisee par
		// ailleurs, ex. Player.java:5532 pour bloquer /restart en zone d'eau,
		// peu importe la profondeur). Le declenchement reel de la nage est
		// gere par revalidateInZone ci-dessous, reevalue en continu (onEnter/
		// onExit ne se declenchent qu'une fois par sejour dans la zone,
		// incompatible avec un seuil qui doit se reevaluer pendant qu'on
		// s'avance vers le large).
		creature.setInsideZone(ZoneId.WATER, true);
	}

	@Override
	protected void onExit(Creature creature)
	{
		creature.setInsideZone(ZoneId.WATER, false);

		// Filet de securite : si le personnage sort carrement du cuboide zone
		// (ex. teleport) alors qu'il nageait, on force l'arret de la nage
		// immediatement plutot que d'attendre le prochain revalidateInZone.
		if (creature.isInWater())
		{
			creature.getMove().removeMoveType(MoveType.SWIM);
			notifyMoveTypeChanged(creature);
		}
	}

	@Override
	public void revalidateInZone(Creature creature)
	{
		super.revalidateInZone(creature);

		// isInsideZone() re-teste la geometrie (cuboide plat) independamment
		// du Set _creatures deja mis a jour par le super : on ne veut evaluer
		// la profondeur que si le personnage est reellement dans la zone
		// (evite un GeoEngine inutile pour tout le reste du monde).
		if (!isInsideZone(creature))
			return;

		boolean deepEnough = isDeepEnough(creature);
		if (deepEnough == creature.isInWater())
			return;

		if (deepEnough)
			creature.getMove().addMoveType(MoveType.SWIM);
		else
			creature.getMove().removeMoveType(MoveType.SWIM);

		notifyMoveTypeChanged(creature);
	}

	// Profondeur locale = hauteur de surface (fixe, definie par la zone) moins
	// la hauteur du sol au point exact (geodata, variable - c'est elle qui
	// distingue le rivage peu profond du large). Comparee a une fraction de la
	// hauteur de collision du personnage plutot qu'un seuil fixe, pour rester
	// coherent entre un Kobold minuscule et un Orc massif.
	private boolean isDeepEnough(Creature creature)
	{
		int groundZ = GeoEngine.getInstance().getHeight(creature.getX(), creature.getY(), creature.getZ());
		int depth = getWaterZ() - groundZ;
		double threshold = creature.getCollisionHeight() * SWIM_DEPTH_RATIO;
		return depth >= threshold;
	}

	private void notifyMoveTypeChanged(Creature creature)
	{
		// UserInfo/CharInfo ne transportent qu'un bit "running", pas l'etat de
		// nage : sans ce packet dedie, le client n'apprend jamais le
		// changement (a moins que le joueur ne bascule course/marche au meme
		// moment, ce qui envoie ChangeMoveType par coincidence). broadcastPacket
		// inclut le joueur lui-meme (cf. Player.broadcastPacket selfToo=true).
		creature.broadcastPacket(new ChangeMoveType(creature));

		if (creature instanceof Player player)
			player.broadcastUserInfo();
		else if (creature instanceof Npc npc)
		{
			npc.forEachKnownType(Player.class, player ->
			{
				if (npc.getStatus().getMoveSpeed() == 0)
					player.sendPacket(new ServerObjectInfo(npc, player));
				else
					player.sendPacket(new NpcInfo(npc, player));
			});
		}
	}

	public int getWaterZ()
	{
		return getZone().getHighZ();
	}
}