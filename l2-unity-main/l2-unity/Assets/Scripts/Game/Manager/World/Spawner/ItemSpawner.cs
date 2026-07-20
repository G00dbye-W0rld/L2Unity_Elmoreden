using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

// Gere l'apparition/suppression des objets au sol (WorldItem). Volontairement
// PAS derive de EntitySpawnStrategy<T> : ce systeme est concu autour d'Entity
// (HP, combat, controleur d'animation...), inadapte a un simple objet
// ramassable. WorldSpawner garde un dictionnaire separe pour ces objets et
// verifie ItemSpawner en premier dans RemoveObject, sans toucher au chemin
// Entity existant.
public class ItemSpawner
{
    private readonly Transform _itemsContainer;
    private readonly GameObject _placeholderPrefab;

    private readonly ConcurrentDictionary<int, WorldItem> _items = new ConcurrentDictionary<int, WorldItem>();

    // Caches Resources.Load par nom de mesh/materiau (evite de re-charger a
    // chaque drop). Une valeur null en cache signifie "deja tente, introuvable"
    // - evite de retenter Resources.Load en boucle pour un mesh non extrait.
    private readonly Dictionary<string, GameObject> _meshPrefabCache = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, Material> _materialCache = new Dictionary<string, Material>();

    // Multiplicateur d'echelle applique aux vrais meshes "dropitems" a
    // l'instanciation (pas figé dans le prefab, pour pouvoir l'ajuster sans
    // regenerer les 415 prefabs). 1 = taille brute de l'import FBX (rotation
    // d'axe deja corrigee par DropItemAssetGenerator, mais pas de facteur
    // d'echelle - contrairement aux personnages, ces petits props n'en ont
    // pas besoin). Ne s'applique PAS au placeholder (deja calibre separement).
    private readonly float _worldScale;

    public ItemSpawner(Transform itemsContainer, float worldScale = 1f)
    {
        _itemsContainer = itemsContainer;
        _worldScale = worldScale;
        _placeholderPrefab = Resources.Load<GameObject>("Prefabs/World/WorldItemPlaceholder");

        if (_placeholderPrefab == null)
        {
            Debug.LogError("[ItemSpawner] Prefab 'Prefabs/World/WorldItemPlaceholder' introuvable.");
        }
    }

    public bool HasItem(int objectId)
    {
        return _items.ContainsKey(objectId);
    }

    // Pour l'action "Pickup" de la barre de raccourcis (ActionType.Pickup) :
    // ramasse l'objet au sol le plus proche dans un rayon donne, sans avoir a
    // cliquer dessus.
    public WorldItem GetNearestItem(Vector3 position, float maxRadius)
    {
        WorldItem nearest = null;
        float nearestDistSqr = maxRadius * maxRadius;

        foreach (WorldItem item in _items.Values)
        {
            if (item == null) continue;

            float distSqr = (item.transform.position - position).sqrMagnitude;
            if (distSqr <= nearestDistSqr)
            {
                nearest = item;
                nearestDistSqr = distSqr;
            }
        }

        return nearest;
    }

    public void OnReceiveSpawnItem(int objectId, int itemTemplateId, Vector3 position, bool isStackable, int count)
    {
        SpawnItem(objectId, itemTemplateId, position, isStackable, count);
    }

    public void OnReceiveDropItem(int itemObjectId, int itemTemplateId, Vector3 position, bool isStackable, int count)
    {
        // Le dropperObjectId (celui qui a fait tomber l'objet) n'est pas
        // exploite pour l'instant - pas d'animation de "jet" cote dropper
        // (voir plan, jalon 5 optionnel).
        SpawnItem(itemObjectId, itemTemplateId, position, isStackable, count);
    }

    private void SpawnItem(int objectId, int itemTemplateId, Vector3 position, bool isStackable, int count)
    {
        if (_items.ContainsKey(objectId))
        {
            return;
        }

        GameObject prefab = ResolvePrefab(itemTemplateId, out Material overrideMaterial);
        if (prefab == null)
        {
            return;
        }

        position.y = World.Instance.GetGroundHeight(position);

        GameObject go = Object.Instantiate(prefab, position, Quaternion.identity, _itemsContainer);
        go.name = $"Item_{itemTemplateId}_{objectId}";

        Renderer renderer = go.GetComponentInChildren<Renderer>();

        if (prefab != _placeholderPrefab)
        {
            go.transform.localScale = Vector3.one * _worldScale;

            // Les meshes "dropitems" n'ont pas forcement leur pivot a leur
            // base (confirme visuellement : l'objet s'enfoncait legerement
            // sous le sol). On recale donc sur la base REELLE du mesh
            // (bounds.min.y) plutot que sur le pivot brut de l'objet.
            if (renderer != null)
            {
                float bottomOffset = renderer.bounds.min.y - go.transform.position.y;
                go.transform.position -= new Vector3(0f, bottomOffset, 0f);
            }
        }

        if (overrideMaterial != null && renderer != null)
        {
            renderer.sharedMaterial = overrideMaterial;
        }

        WorldItem worldItem = go.GetComponent<WorldItem>();
        if (worldItem == null)
        {
            Debug.LogError($"[ItemSpawner] Le prefab pour l'item {itemTemplateId} n'a pas de composant WorldItem.");
            Object.Destroy(go);
            return;
        }

        worldItem.Initialize(objectId, itemTemplateId, count, isStackable);
        _items.TryAdd(objectId, worldItem);
    }

    // Resout le vrai mesh de l'item ("DropModel", deja parse mais jusqu'ici
    // inutilise - cf Abstractgrp.cs) s'il a ete extrait/importe, avec repli
    // sur le placeholder generique sinon. Resout aussi le materiau associe
    // ("DropTexture") quand un .props.txt correspondant existe (couvre les
    // objets "etc item" generiques ; les pieces d'armure/arme referencent la
    // texture du personnage equipe, pas encore geree - garde le materiau par
    // defaut du mesh importe dans ce cas).
    private GameObject ResolvePrefab(int itemTemplateId, out Material overrideMaterial)
    {
        overrideMaterial = null;

        Abstractgrp grp = ItemTable.Instance.GetItem(itemTemplateId)?.Itemgrp;
        if (grp == null || string.IsNullOrEmpty(grp.DropModel))
        {
            return _placeholderPrefab;
        }

        string meshName = StripNamespace(grp.DropModel);
        if (!_meshPrefabCache.TryGetValue(meshName, out GameObject prefab))
        {
            prefab = Resources.Load<GameObject>($"Prefabs/World/DropItems/{meshName}");
            _meshPrefabCache[meshName] = prefab;
        }

        if (prefab == null)
        {
            return _placeholderPrefab;
        }

        if (!string.IsNullOrEmpty(grp.DropTexture))
        {
            string materialName = StripNamespace(grp.DropTexture);
            if (!_materialCache.TryGetValue(materialName, out overrideMaterial))
            {
                overrideMaterial = Resources.Load<Material>($"Data/Animations/DropItems/Materials/{materialName}");
                _materialCache[materialName] = overrideMaterial;
            }
        }

        return prefab;
    }

    private static string StripNamespace(string value)
    {
        int dot = value.IndexOf('.');
        return dot >= 0 ? value.Substring(dot + 1) : value;
    }

    public void OnReceiveGetItem(int pickerObjectId, int itemObjectId)
    {
        if (_items.TryGetValue(itemObjectId, out WorldItem worldItem) && worldItem != null)
        {
            // Desactive immediatement la possibilite de re-cliquer pendant
            // que le DeleteObject (suppression reelle) arrive du serveur.
            Collider collider = worldItem.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            // "SkillSound/Pickup" existe dans le projet FMOD Studio mais pas
            // dans les banks exportees (Master.strings.bank), donc muet en
            // silence - "itemequip_etc_money" est confirme audible.
            AudioManager.Instance.PlayEquipSound("itemequip_etc_money");
        }

        WorldSpawner.Instance.ExecuteWithEntityAsync(pickerObjectId, e => e.AnimationController.PickupItem());
    }

    public void RemoveItem(int objectId)
    {
        // GameServerPacketHandler.OnRemoveObject appelle WorldSpawner.RemoveObject
        // directement (pas via _eventProcessor.QueueEvent, contrairement aux
        // autres handlers) - ce chemin s'execute donc sur le thread reseau, pas
        // le thread principal Unity. Le chemin Entity existant s'en sort car
        // ExecuteWithEntityAsync differe deja l'appel a Destroy(); ce chemin
        // Item doit le faire lui-meme, sinon Object.Destroy plante
        // ("get_gameObject can only be called from the main thread") et l'objet
        // reste visible indefiniment (le TryRemove du dictionnaire reussit avant
        // le crash, donc plus rien ne le detruit ensuite).
        if (_items.TryRemove(objectId, out WorldItem worldItem) && worldItem != null)
        {
            EventProcessor.Instance.QueueEvent(() => Object.Destroy(worldItem.gameObject));
        }
    }
}
