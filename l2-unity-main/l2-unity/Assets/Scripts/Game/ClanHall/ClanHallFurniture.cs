using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Mobilier pose dans les salles de clan. Le serveur dit quel meuble occupe quel
/// emplacement ; le catalogue donne son modele, la disposition de la salle sa place.
public static class ClanHallFurniture
{
    private const string MeshFolder = "Data/StaticMeshes/";
    public const string CatalogName = "FurnitureCatalog";

    [Serializable]
    public class CatalogItem
    {
        public int itemId;
        public string category;
        public string mesh;
        public float scale = 1f;
    }

    [Serializable]
    public class Catalog
    {
        public List<CatalogItem> items = new List<CatalogItem>();

        public CatalogItem Find(int itemId)
        {
            return items.Find(i => i.itemId == itemId);
        }
    }

    [Serializable]
    public class Slot
    {
        public int slot;
        public string category;
        public Vector3 position;
        public Vector3 rotation;
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

    public struct Placed
    {
        public int Slot;
        public int ItemId;
        public int ChestObjectId;
    }

    private class Shown
    {
        public int ItemId;
        public int ChestObjectId;
        public GameObject Instance;
    }

    private static Catalog _catalog;
    private static readonly Dictionary<int, Layout> _layouts = new Dictionary<int, Layout>();
    private static readonly Dictionary<int, Dictionary<int, Shown>> _shown = new Dictionary<int, Dictionary<int, Shown>>();

    public static string LayoutName(int hallId)
    {
        return "ClanHallFurniture_" + hallId;
    }

    public static Catalog GetCatalog()
    {
        if (_catalog == null)
        {
            TextAsset json = Resources.Load<TextAsset>(ClanHallDecor.LayoutFolder + CatalogName);
            _catalog = json != null ? JsonUtility.FromJson<Catalog>(json.text) : new Catalog();
        }

        return _catalog;
    }

    public static GameObject LoadMesh(CatalogItem item)
    {
        return item != null ? Resources.Load<GameObject>(MeshFolder + item.mesh) : null;
    }

    private static readonly Dictionary<string, string> CategoryNames = new Dictionary<string, string>
    {
        { "SEAT", "Siège" }, { "TABLE", "Table" }, { "DECOR", "Décoration" }, { "CARPET", "Tapis" }, { "CURTAIN", "Tentures" }, { "PLATFORM", "Estrade" }, { "CHEST", "Coffre" }
    };

    private static int _currentHall;
    private static readonly List<GameObject> _labels = new List<GameObject>();

    /// Etiquettes flottantes "Siege 3" sur chaque emplacement, pendant qu'on amenage :
    /// numerotees par categorie dans l'ordre des identifiants, comme le menu du serveur.
    public static void ShowSlotLabels(bool show)
    {
        foreach (GameObject label in _labels)
        {
            if (label != null)
            {
                UnityEngine.Object.Destroy(label);
            }
        }
        _labels.Clear();

        Layout layout = show ? GetLayout(_currentHall) : null;
        if (layout == null)
        {
            return;
        }

        Dictionary<string, int> ordinals = new Dictionary<string, int>();
        foreach (Slot slot in layout.slots.OrderBy(s => s.slot))
        {
            ordinals.TryGetValue(slot.category, out int ordinal);
            ordinals[slot.category] = ++ordinal;

            // Seuls les emplacements libres sont signales.
            if (_shown.TryGetValue(_currentHall, out Dictionary<int, Shown> hall) && hall.ContainsKey(slot.slot))
            {
                continue;
            }

            string name = CategoryNames.TryGetValue(slot.category, out string text) ? text : slot.category;
            GameObject label = CreateStoreStyleLabel($"{name} {ordinal}", slot.position + Vector3.up * 0.9f);
            if (label != null)
            {
                _labels.Add(label);
            }
        }
    }

    // Meme habillage que l'encart d'un magasin prive : le titre d'une nameplate
    // sur un fond noir arrondi, tourne vers la camera et mis a l'echelle comme elle.
    private static GameObject CreateStoreStyleLabel(string content, Vector3 position)
    {
        WorldNameplateRenderer renderer = WorldNameplateRenderer.Instance;
        if (renderer == null || renderer.NameplatePrefab == null)
        {
            return null;
        }

        GameObject label = UnityEngine.Object.Instantiate(renderer.NameplatePrefab, position, Quaternion.identity);
        label.name = "ClanHallSlotLabel";
        foreach (Transform child in label.transform)
        {
            child.gameObject.SetActive(child.name == "Title");
        }

        TMPro.TMP_Text title = label.transform.Find("Title").GetComponent<TMPro.TMP_Text>();
        title.transform.localPosition = Vector3.zero;
        title.text = $"<color={SlotLabelColor}><noparse>{content}</noparse></color>";
        title.ForceMeshUpdate();

        if (_labelMaterial == null)
        {
            MeshRenderer bubble = label.transform.Find("BubbleIcon")?.GetComponent<MeshRenderer>();
            _labelMaterial = WorldBubbleVisual.CreateMaterial(bubble != null ? bubble.sharedMaterial : null, title.fontSharedMaterial.renderQueue - 1);
        }

        Bounds bounds = title.textBounds;
        GameObject background = new GameObject("StoreBackground");
        background.transform.SetParent(title.transform, false);
        background.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0.01f);
        Mesh mesh = new Mesh { name = "SlotLabelBackground" };
        WorldBubbleVisual.UpdateSlicedMesh(mesh, bounds.size.x + 0.14f, bounds.size.y + 0.07f);
        background.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer backgroundRenderer = background.AddComponent<MeshRenderer>();
        backgroundRenderer.sharedMaterial = _labelMaterial;
        backgroundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0.9f));
        backgroundRenderer.SetPropertyBlock(block);

        label.AddComponent<NameplateBillboard>();
        return label;
    }

    private const string SlotLabelColor = "#FFD27F";
    private static Material _labelMaterial;

    private class NameplateBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera camera = CameraController.Instance != null ? CameraController.Instance.GetComponent<Camera>() : null;
            if (camera == null || WorldNameplateRenderer.Instance == null)
            {
                return;
            }

            Vector3 forward = camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(forward);
            }
            float distance = Vector3.Distance(transform.position, camera.transform.position);
            transform.localScale = Vector3.one * WorldNameplateRenderer.Instance.ScaleAt(distance);
        }
    }

    public static void Apply(int hallId, List<Placed> placed)
    {
        _currentHall = hallId;
        if (!_shown.TryGetValue(hallId, out Dictionary<int, Shown> shown))
        {
            shown = new Dictionary<int, Shown>();
            _shown[hallId] = shown;
        }

        Layout layout = GetLayout(hallId);
        HashSet<int> kept = new HashSet<int>();

        foreach (Placed entry in placed)
        {
            kept.Add(entry.Slot);
            if (shown.TryGetValue(entry.Slot, out Shown current) && current.Instance != null && current.ItemId == entry.ItemId)
            {
                current.ChestObjectId = entry.ChestObjectId;
                continue;
            }

            Remove(shown, entry.Slot);

            Slot place = layout?.Find(entry.Slot);
            CatalogItem item = GetCatalog().Find(entry.ItemId);
            GameObject prefab = LoadMesh(item);
            if (place == null || prefab == null)
            {
                Debug.LogWarning($"[Salle de clan] Meuble {entry.ItemId} impossible a placer (emplacement {entry.Slot}).");
                continue;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, place.position, Quaternion.Euler(place.rotation));
            instance.name = $"ClanHallFurniture_{hallId}_{entry.Slot}";
            instance.transform.localScale = prefab.transform.localScale * item.scale;
            ClanHallDecor.Prepare(instance);

            shown[entry.Slot] = new Shown { ItemId = entry.ItemId, ChestObjectId = entry.ChestObjectId, Instance = instance };
        }

        foreach (int slot in new List<int>(shown.Keys))
        {
            if (!kept.Contains(slot))
            {
                Remove(shown, slot);
            }
        }
    }

    private static void Remove(Dictionary<int, Shown> shown, int slot)
    {
        if (shown.TryGetValue(slot, out Shown current) && current.Instance != null)
        {
            UnityEngine.Object.Destroy(current.Instance);
        }
        shown.Remove(slot);
    }

    /// Le coffre clique, s'il en est un : le serveur le represente par un PNJ invisible.
    public static bool TryFindChest(Transform hit, out int objectId, out Vector3 position)
    {
        foreach (Dictionary<int, Shown> hall in _shown.Values)
        {
            foreach (Shown shown in hall.Values)
            {
                if (shown.ChestObjectId != 0 && shown.Instance != null && hit.IsChildOf(shown.Instance.transform))
                {
                    objectId = shown.ChestObjectId;
                    position = shown.Instance.transform.position;
                    return true;
                }
            }
        }

        objectId = 0;
        position = Vector3.zero;
        return false;
    }

    private static Layout GetLayout(int hallId)
    {
        if (_layouts.TryGetValue(hallId, out Layout layout))
        {
            return layout;
        }

        TextAsset json = Resources.Load<TextAsset>(ClanHallDecor.LayoutFolder + LayoutName(hallId));
        layout = json != null ? JsonUtility.FromJson<Layout>(json.text) : null;
        _layouts[hallId] = layout;
        return layout;
    }
}
