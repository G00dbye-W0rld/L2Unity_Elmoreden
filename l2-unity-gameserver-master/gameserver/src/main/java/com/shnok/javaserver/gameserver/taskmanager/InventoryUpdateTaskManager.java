package com.shnok.javaserver.gameserver.taskmanager;

import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

import com.shnok.javaserver.commons.pool.ThreadPool;

import com.shnok.javaserver.gameserver.model.actor.Playable;
import com.shnok.javaserver.gameserver.model.itemcontainer.Inventory;

/**
 * Send client packet to related {@link Inventory}'s owners, if an update is asked.
 */
public class InventoryUpdateTaskManager implements Runnable
{
	private final Set<Inventory> _list = ConcurrentHashMap.newKeySet();
	
	protected InventoryUpdateTaskManager()
	{
		// Run task every 333ms.
		ThreadPool.scheduleAtFixedRate(this, 150L, 150L);
	}
	
	@Override
	public final void run()
	{
		// List is empty, skip.
		if (_list.isEmpty())
			return;
		
		// Loop all inventories and if needed, send the IU and update weight.
		for (Inventory inv : _list)
		{
			// Don't send packet if the Playable isn't visible and isn't teleporting.
			final Playable owner = inv.getOwner();
			if (!owner.isVisible() && !owner.isTeleporting())
			{
				// Le poids reste a jour meme sans envoi : sinon la penalite de surcharge
				// se figeait jusqu'au changement d'inventaire suivant.
				inv.updateWeight();
				_list.remove(inv);
				continue;
			}

			if (!inv.getUpdateList().isEmpty())
				owner.sendIU();

			// Le poids est recalcule meme si la liste a deja ete videe ailleurs (envoi direct
			// d'ItemList ou d'InventoryUpdate) : sinon la penalite de surcharge restait perimee.
			inv.updateWeight();

			// Plus rien a envoyer : l'inventaire quitte le gestionnaire.
			if (inv.getUpdateList().isEmpty())
				_list.remove(inv);
		}
	}
	
	public void add(Inventory inv)
	{
		if (!_list.contains(inv))
			_list.add(inv);
	}
	
	public static final InventoryUpdateTaskManager getInstance()
	{
		return SingletonHolder.INSTANCE;
	}
	
	private static class SingletonHolder
	{
		protected static final InventoryUpdateTaskManager INSTANCE = new InventoryUpdateTaskManager();
	}
}