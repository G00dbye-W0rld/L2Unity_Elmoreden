using System;
using System.Collections.Generic;
using UnityEngine;

/// Objets de decoration des salles de clan (cheminee, tapis, lustre...).
/// Le serveur donne le niveau de chaque emplacement ; la position de chaque
/// emplacement vient d'une disposition propre a la salle, preparee dans
/// l'editeur (fenetre L2 > Salles de clan > Disposition du decor).
public static class ClanHallDecor
{
    public const int SlotCount = 12;
    public const string LayoutFolder = "Data/ClanHalls/";
    private const string MeshFolder = "Data/StaticMeshes/Agit_B_s/";
    private const int LayerStaticMesh = 7;

    public static readonly string[] SlotNames =
    {
        "Cheminée (PV)", "Cristal de mana (PM)", "Statue", "Lustre (expérience)",
        "Miroir (téléportation)", "Cristal", "Tentures", "Tentures murales",
        "Insigne (soutien)", "Drapeau", "Estrade", "Établi (fabrication)"
    };

    [Serializable]
    public class Slot
    {
        public int slot;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale = Vector3.one;
    }

    [Serializable]
    public class Layout
    {
        public int hallId;
        public List<Slot> slots = new List<Slot>();

        public Slot Find(int slot)
        {
            return slots.Find(s => s.slot == slot);
        }
    }

    private class Shown
    {
        public int Depth;
        public GameObject Instance;
    }

    private static readonly Dictionary<int, Layout> _layouts = new Dictionary<int, Layout>();
    private static readonly Dictionary<(int, int), Shown> _shown = new Dictionary<(int, int), Shown>();

    public static string LayoutName(int hallId)
    {
        return "ClanHallDecor_" + hallId;
    }

    public static string MeshName(int slot, int depth)
    {
        return slot + "_" + Mathf.Clamp(depth - 1, 0, 2);
    }

    public const int WorkbenchSlot = 12;
    private const int ManaSlot = 2;
    private const int CrystalMeshSlot = 6;
    private const string WorkbenchMesh = "Data/StaticMeshes/interior_B_S/interior_B_304";

    public static GameObject LoadMesh(int slot, int depth)
    {
        // La fabrication est symbolisee par un etabli de forgeron, le meme a tous les niveaux.
        if (slot == WorkbenchSlot)
        {
            return Resources.Load<GameObject>(WorkbenchMesh);
        }

        // Les PM sont symbolises par le cristal : le tapis d'origine est celui de l'estrade.
        if (slot == ManaSlot)
        {
            slot = CrystalMeshSlot;
        }

        GameObject prefab = Resources.Load<GameObject>(MeshFolder + MeshName(slot, depth));
        return prefab != null ? prefab : Resources.Load<GameObject>(MeshFolder + MeshName(slot, 1));
    }

    private static readonly Dictionary<int, int> _workbenches = new Dictionary<int, int>();

    /// L'etabli est le seul objet d'installation cliquable : le serveur le represente
    /// par un marchand invisible, qui ouvre la fabrication au niveau installe.
    public static void SetWorkbench(int hallId, int objectId)
    {
        _workbenches[hallId] = objectId;
    }

    public static bool TryFindWorkbench(Transform hit, out int objectId, out Vector3 position)
    {
        foreach (KeyValuePair<(int, int), Shown> entry in _shown)
        {
            if (entry.Key.Item2 == WorkbenchSlot && entry.Value.Instance != null && hit.IsChildOf(entry.Value.Instance.transform)
                && _workbenches.TryGetValue(entry.Key.Item1, out objectId) && objectId != 0)
            {
                position = entry.Value.Instance.transform.position;
                return true;
            }
        }

        objectId = 0;
        position = Vector3.zero;
        return false;
    }

    public static void Apply(int hallId, byte[] depths)
    {
        Layout layout = GetLayout(hallId);

        for (int slot = 1; slot <= SlotCount; slot++)
        {
            int depth = depths[slot - 1];
            _shown.TryGetValue((hallId, slot), out Shown shown);

            if (shown != null && shown.Depth == depth && shown.Instance != null)
            {
                continue;
            }

            if (shown != null && shown.Instance != null)
            {
                UnityEngine.Object.Destroy(shown.Instance);
            }
            _shown.Remove((hallId, slot));

            Slot place = layout?.Find(slot);
            if (depth <= 0 || place == null)
            {
                continue;
            }

            GameObject prefab = LoadMesh(slot, depth);
            if (prefab == null)
            {
                Debug.LogWarning($"[Salle de clan] Objet de decor introuvable : {MeshName(slot, depth)}");
                continue;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, place.position, Quaternion.Euler(place.rotation));
            instance.name = $"ClanHallDecor_{hallId}_{slot}";
            instance.transform.localScale = place.scale;
            Prepare(instance);

            _shown[(hallId, slot)] = new Shown { Depth = depth, Instance = instance };
        }
    }

    private static Layout GetLayout(int hallId)
    {
        if (_layouts.TryGetValue(hallId, out Layout layout))
        {
            return layout;
        }

        TextAsset json = Resources.Load<TextAsset>(LayoutFolder + LayoutName(hallId));
        layout = json != null ? JsonUtility.FromJson<Layout>(json.text) : null;
        if (layout == null)
        {
            Debug.LogWarning($"[Salle de clan] Pas de disposition de decor pour la salle {hallId}.");
        }

        _layouts[hallId] = layout;
        return layout;
    }

    // Meme couche que les meshes statiques de la carte ; l'import FBX ne cree
    // pas toujours les collisions, on les ajoute ici.
    public static void Prepare(GameObject instance)
    {
        foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = LayerStaticMesh;

            MeshFilter filter = child.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && child.GetComponent<Collider>() == null)
            {
                child.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
        }
    }
}
