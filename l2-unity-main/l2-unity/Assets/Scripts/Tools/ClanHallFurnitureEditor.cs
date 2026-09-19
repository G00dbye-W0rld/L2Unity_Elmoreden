#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Emplacements de mobilier d'une salle de clan : chaque emplacement a une
// categorie (siege, table...) et montre un meuble du catalogue en apercu.
// L'enregistrement ecrit la disposition du client et les emplacements du serveur.
public class ClanHallFurnitureEditor : EditorWindow
{
    private const string LayoutPath = "Assets/Resources/Data/ClanHalls/";
    private const string ServerFile = "/../../../l2-unity-gameserver-master/gameserver/data/xml/clanHallFurniture.xml";
    private const float Scale = 52.5f;

    private static readonly string[] Categories = { "SEAT", "TABLE", "DECOR", "CARPET", "CURTAIN", "PLATFORM", "CHEST" };
    private static readonly string[] CategoryNames = { "Siège", "Table", "Décoration", "Tapis", "Tentures", "Estrade", "Coffre" };

    private int _hallId = 65;
    private int _category;
    private GameObject _root;

    [MenuItem("L2/Salles de clan/Emplacements du mobilier", false, 941)]
    public static void Open()
    {
        GetWindow<ClanHallFurnitureEditor>("Mobilier de salle").Show();
    }

    private string RootName => $"ClanHallFurniture_{_hallId} (edition)";

    private void OnGUI()
    {
        if (_root == null)
        {
            _root = ClanHallEditorMockups.Find(RootName);
        }

        _hallId = EditorGUILayout.IntField("Salle de clan", _hallId);
        if (GUILayout.Button("Charger pour edition"))
        {
            Load();
        }

        using (new EditorGUI.DisabledScope(_root == null))
        {
            EditorGUILayout.Space();
            _category = EditorGUILayout.Popup("Categorie", _category, CategoryNames);
            if (GUILayout.Button("Ajouter un emplacement (au centre de la vue)"))
            {
                Vector3 at = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
                GameObject slot = Spawn(NextId(), Categories[_category], null);
                if (slot != null)
                {
                    slot.transform.position = at;
                    Selection.activeGameObject = slot;
                }
            }

            if (GUILayout.Button("Meuble suivant pour l'apercu (emplacement selectionne)"))
            {
                CyclePreview();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Enregistrer"))
            {
                Save();
            }

            if (GUILayout.Button("Fermer l'edition (sans enregistrer)"))
            {
                DestroyImmediate(_root);
            }
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Generer les icones du catalogue"))
        {
            GenerateIcons();
        }

        EditorGUILayout.HelpBox(
            "Placez et tournez chaque emplacement. Mettre un meuble a l'echelle regle " +
            "l'echelle de CE meuble dans le catalogue. Supprimer un objet supprime l'emplacement.",
            MessageType.Info);
    }

    private void Load()
    {
        if (_root != null)
        {
            DestroyImmediate(_root);
        }

        _root = new GameObject(RootName);
        _root.tag = "EditorOnly";
        _root.hideFlags = HideFlags.DontSaveInEditor;

        TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath + ClanHallFurniture.LayoutName(_hallId) + ".json");
        if (json == null)
        {
            return;
        }

        ClanHallFurniture.Layout layout = JsonUtility.FromJson<ClanHallFurniture.Layout>(json.text);
        foreach (ClanHallFurniture.Slot saved in layout.slots)
        {
            GameObject slot = Spawn(saved.slot, saved.category, null);
            if (slot != null)
            {
                slot.transform.SetPositionAndRotation(saved.position, Quaternion.Euler(saved.rotation));
            }
        }
    }

    // Nom : "M03 SEAT 95001" -> emplacement, categorie, meuble montre.
    private GameObject Spawn(int id, string category, ClanHallFurniture.CatalogItem preview)
    {
        ClanHallFurniture.Catalog catalog = LoadCatalog();
        preview ??= catalog.items.FirstOrDefault(i => i.category == category);
        GameObject prefab = ClanHallFurniture.LoadMesh(preview);
        if (prefab == null)
        {
            Debug.LogWarning($"[Salle de clan] Aucun meuble du catalogue pour la categorie {category}.");
            return null;
        }

        GameObject slot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _root.transform);
        slot.name = $"M{id:00} {category} {preview.itemId}";
        slot.transform.localScale = prefab.transform.localScale * preview.scale;
        foreach (Transform part in slot.GetComponentsInChildren<Transform>(true))
        {
            part.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        }
        return slot;
    }

    private void CyclePreview()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || selected.transform.parent != _root.transform)
        {
            return;
        }

        Parse(selected.name, out int id, out string category, out int itemId);
        List<ClanHallFurniture.CatalogItem> choices = LoadCatalog().items.Where(i => i.category == category).ToList();
        int index = (choices.FindIndex(i => i.itemId == itemId) + 1) % choices.Count;

        GameObject replacement = Spawn(id, category, choices[index]);
        replacement.transform.SetPositionAndRotation(selected.transform.position, selected.transform.rotation);
        DestroyImmediate(selected);
        Selection.activeGameObject = replacement;
    }

    private int NextId()
    {
        int max = 0;
        foreach (Transform slot in _root.transform)
        {
            Parse(slot.name, out int id, out _, out _);
            max = Mathf.Max(max, id);
        }
        return max + 1;
    }

    private static void Parse(string name, out int id, out string category, out int itemId)
    {
        string[] parts = name.Split(' ');
        id = int.Parse(parts[0].Substring(1));
        category = parts[1];
        itemId = int.Parse(parts[2]);
    }

    // Icones des meubles (icon.furniture_<id>) et des installations de la salle
    // (icon.clanhall_upgrade_<type>, affichees dans les menus de la gerante),
    // tirees de l'apercu des modeles calcule par Unity.
    private static readonly int[] UpgradeTypes = { 1, 2, 4, 5, 9, 12 };

    private static void GenerateIcons()
    {
        int done = 0;
        int pending = 0;

        foreach (ClanHallFurniture.CatalogItem item in LoadCatalog().items)
        {
            SaveIcon(ClanHallFurniture.LoadMesh(item), $"furniture_{item.itemId}", ref done, ref pending);
        }

        foreach (int type in UpgradeTypes)
        {
            SaveIcon(ClanHallDecor.LoadMesh(type, 2), $"clanhall_upgrade_{type}", ref done, ref pending);
        }

        AssetDatabase.Refresh();
        Debug.Log($"[Salle de clan] {done} icone(s) generee(s)" + (pending > 0 ? $", {pending} apercu(s) pas encore prets : cliquez a nouveau dans quelques secondes." : "."));
    }

    // Unity calcule les apercus en arriere-plan : au premier clic certains ne
    // sont pas prets, un second clic les recupere.
    private static void SaveIcon(GameObject prefab, string name, ref int done, ref int pending)
    {
        if (prefab == null)
        {
            return;
        }

        Texture2D preview = AssetPreview.GetAssetPreview(prefab);
        if (preview == null)
        {
            pending++;
            return;
        }

        RenderTexture target = RenderTexture.GetTemporary(64, 64);
        Graphics.Blit(preview, target);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D icon = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        icon.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
        icon.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);

        File.WriteAllBytes($"Assets/Resources/Data/SysTextures/Icon/{name}.png", icon.EncodeToPNG());
        DestroyImmediate(icon);
        done++;
    }

    private static ClanHallFurniture.Catalog LoadCatalog()
    {
        TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath + ClanHallFurniture.CatalogName + ".json");
        return json != null ? JsonUtility.FromJson<ClanHallFurniture.Catalog>(json.text) : new ClanHallFurniture.Catalog();
    }

    private void Save()
    {
        ClanHallFurniture.Catalog catalog = LoadCatalog();
        ClanHallFurniture.Layout layout = new ClanHallFurniture.Layout { hallId = _hallId };
        StringBuilder server = new StringBuilder();
        server.Append($"\t<hall id=\"{_hallId}\">\n");

        foreach (Transform slot in _root.transform.Cast<Transform>().OrderBy(t => t.name))
        {
            Parse(slot.name, out int id, out string category, out int itemId);
            layout.slots.Add(new ClanHallFurniture.Slot
            {
                slot = id,
                category = category,
                position = slot.position,
                rotation = slot.rotation.eulerAngles
            });

            // L'echelle donnee a l'apercu devient celle du meuble dans le catalogue.
            ClanHallFurniture.CatalogItem item = catalog.Find(itemId);
            GameObject prefab = ClanHallFurniture.LoadMesh(item);
            if (item != null && prefab != null && prefab.transform.localScale.x != 0f)
            {
                item.scale = (float)System.Math.Round(slot.localScale.x / prefab.transform.localScale.x, 3);
            }

            // Serveur en unites Unreal : Unity = (y, z, x) / 52.5.
            Vector3 p = slot.position * Scale;
            server.Append(string.Format(CultureInfo.InvariantCulture,
                "\t\t<slot id=\"{0}\" category=\"{1}\" x=\"{2}\" y=\"{3}\" z=\"{4}\" />\n",
                id, category, Mathf.RoundToInt(p.z), Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y)));
        }
        server.Append("\t</hall>\n");

        Directory.CreateDirectory(LayoutPath);
        File.WriteAllText(LayoutPath + ClanHallFurniture.LayoutName(_hallId) + ".json", JsonUtility.ToJson(layout, true));
        File.WriteAllText(LayoutPath + ClanHallFurniture.CatalogName + ".json", JsonUtility.ToJson(catalog, true));
        AssetDatabase.Refresh();

        string serverPath = Application.dataPath + ServerFile;
        if (File.Exists(serverPath))
        {
            string xml = File.ReadAllText(serverPath);
            xml = Regex.Replace(xml, $"\\t<hall id=\"{_hallId}\">.*?</hall>\\n", "", RegexOptions.Singleline);
            xml = xml.Replace("</list>", server + "</list>");
            File.WriteAllText(serverPath, xml);
            Debug.Log($"[Salle de clan] {layout.slots.Count} emplacement(s) enregistre(s), serveur mis a jour : {serverPath}");
        }
        else
        {
            Debug.LogWarning($"[Salle de clan] Fichier serveur introuvable ({serverPath}). A coller dans clanHallFurniture.xml :\n{server}");
        }
    }
}
#endif
