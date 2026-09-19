#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Disposition du decor d'une salle de clan : on place les 12 objets dans la
// scene avec les poignees d'Unity, puis on enregistre. Le jeu relit le fichier
// produit (Resources/Data/ClanHalls/ClanHallDecor_<id>.json).
public class ClanHallDecorEditor : EditorWindow
{
    private const string LayoutPath = "Assets/Resources/Data/ClanHalls/";

    private int _hallId = 65;
    private int _depth = 1;
    // Gerante de la Maison des Murmures : point de depart d'une disposition vierge.
    private Vector3 _centre = new Vector3(241343f, -3751f, -85169f) / 52.5f;
    private GameObject _root;

    [MenuItem("L2/Salles de clan/Disposition du decor", false, 940)]
    public static void Open()
    {
        GetWindow<ClanHallDecorEditor>("Decor de salle").Show();
    }

    private void OnGUI()
    {
        // Une recompilation vide les champs de la fenetre, pas la scene.
        if (_root == null)
        {
            _root = ClanHallEditorMockups.Find($"ClanHallDecor_{_hallId} (edition)");
        }

        _hallId = EditorGUILayout.IntField("Salle de clan", _hallId);
        _centre = EditorGUILayout.Vector3Field("Centre (disposition vierge)", _centre);
        if (GUILayout.Button("Centrer sur la vue Scene") && SceneView.lastActiveSceneView != null)
        {
            _centre = SceneView.lastActiveSceneView.pivot;
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Charger pour edition"))
        {
            Load();
        }

        using (new EditorGUI.DisabledScope(_root == null))
        {
            int depth = EditorGUILayout.IntSlider("Niveau affiche", _depth, 1, 3);
            if (depth != _depth)
            {
                _depth = depth;
                ShowDepth();
            }

            if (GUILayout.Button("Enregistrer"))
            {
                Save();
            }

            if (GUILayout.Button("Fermer l'edition (sans enregistrer)"))
            {
                DestroyImmediate(_root);
            }
        }

        EditorGUILayout.HelpBox(
            "Deplacez et tournez chaque objet dans la vue Scene. Un objet desactive " +
            "n'est pas enregistre : cet emplacement restera vide en jeu.", MessageType.Info);
    }

    private void Load()
    {
        if (_root != null)
        {
            DestroyImmediate(_root);
        }

        ClanHallDecor.Layout layout = null;
        string file = FilePath();
        if (File.Exists(file))
        {
            layout = JsonUtility.FromJson<ClanHallDecor.Layout>(File.ReadAllText(file));
        }

        _root = new GameObject($"ClanHallDecor_{_hallId} (edition)");
        // Jamais enregistree avec la scene : sinon la maquette s'affiche en jeu
        // comme si toutes les installations etaient actives.
        _root.tag = "EditorOnly";
        _root.hideFlags = HideFlags.DontSaveInEditor;

        for (int slot = 1; slot <= ClanHallDecor.SlotCount; slot++)
        {
            ClanHallDecor.Slot saved = layout?.Find(slot);
            GameObject model = Spawn(slot);
            if (model == null)
            {
                continue;
            }

            if (saved != null)
            {
                model.transform.SetPositionAndRotation(saved.position, Quaternion.Euler(saved.rotation));
                model.transform.localScale = saved.scale;
            }
            else
            {
                float angle = (slot - 1) * Mathf.PI * 2f / ClanHallDecor.SlotCount;
                model.transform.position = _centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 3f;
                model.SetActive(layout == null);
            }
        }

        Selection.activeGameObject = _root;
        SceneView.lastActiveSceneView?.Frame(new Bounds(_centre, Vector3.one * 8f), false);
    }

    private GameObject Spawn(int slot)
    {
        GameObject prefab = ClanHallDecor.LoadMesh(slot, _depth);
        if (prefab == null)
        {
            Debug.LogWarning($"[Salle de clan] Modele introuvable : {ClanHallDecor.MeshName(slot, _depth)}");
            return null;
        }

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _root.transform);
        model.name = $"{slot:00} - {ClanHallDecor.SlotNames[slot - 1]}";
        foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
        {
            part.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        }
        return model;
    }

    // Change le modele de chaque emplacement en gardant sa position.
    private void ShowDepth()
    {
        Transform[] models = new Transform[_root.transform.childCount];
        for (int i = 0; i < models.Length; i++)
        {
            models[i] = _root.transform.GetChild(i);
        }

        foreach (Transform old in models)
        {
            int slot = int.Parse(old.name.Substring(0, 2));
            GameObject model = Spawn(slot);
            if (model == null)
            {
                continue;
            }

            model.transform.SetPositionAndRotation(old.position, old.rotation);
            model.transform.localScale = old.localScale;
            model.SetActive(old.gameObject.activeSelf);
            DestroyImmediate(old.gameObject);
        }
    }

    private void Save()
    {
        ClanHallDecor.Layout layout = new ClanHallDecor.Layout { hallId = _hallId };

        foreach (Transform model in _root.transform)
        {
            if (!model.gameObject.activeSelf)
            {
                continue;
            }

            layout.slots.Add(new ClanHallDecor.Slot
            {
                slot = int.Parse(model.name.Substring(0, 2)),
                position = model.position,
                rotation = model.rotation.eulerAngles,
                scale = model.localScale
            });
        }

        Directory.CreateDirectory(LayoutPath);
        File.WriteAllText(FilePath(), JsonUtility.ToJson(layout, true));
        AssetDatabase.Refresh();
        Debug.Log($"[Salle de clan] Disposition enregistree : {layout.slots.Count} emplacement(s) -> {FilePath()}");

        SaveWorkbench(layout.Find(ClanHallDecor.WorkbenchSlot));
    }

    // L'etabli est cliquable : le serveur pose un marchand invisible a sa position.
    private void SaveWorkbench(ClanHallDecor.Slot workbench)
    {
        string serverPath = Application.dataPath + "/../../../l2-unity-gameserver-master/gameserver/data/xml/clanHallFurniture.xml";
        if (!File.Exists(serverPath))
        {
            Debug.LogWarning($"[Salle de clan] Fichier serveur introuvable ({serverPath}) : position de l'etabli non transmise.");
            return;
        }

        string xml = File.ReadAllText(serverPath);
        xml = System.Text.RegularExpressions.Regex.Replace(xml, $"\\t<workbench hall=\"{_hallId}\"[^\\n]*\\n", "");
        if (workbench != null)
        {
            // Serveur en unites Unreal : Unity = (y, z, x) / 52.5.
            Vector3 p = workbench.position * 52.5f;
            xml = xml.Replace("</list>", $"\t<workbench hall=\"{_hallId}\" x=\"{Mathf.RoundToInt(p.z)}\" y=\"{Mathf.RoundToInt(p.x)}\" z=\"{Mathf.RoundToInt(p.y)}\" />\n</list>");
        }
        File.WriteAllText(serverPath, xml);
        Debug.Log($"[Salle de clan] Position de l'etabli transmise au serveur : {serverPath}");
    }

    private string FilePath()
    {
        return LayoutPath + ClanHallDecor.LayoutName(_hallId) + ".json";
    }
}

// Les maquettes ne sont jamais sauvegardees, ce qui les rend invisibles pour
// FindObjectsByType et GameObject.Find, et leur permet de survivre au mode Play
// et aux changements de scene : on les cherche donc parmi tous les objets charges.
[InitializeOnLoad]
public static class ClanHallEditorMockups
{
    static ClanHallEditorMockups()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                RemoveAll();
            }
        };
        UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (scene, mode) => RemoveAll();
    }

    public static GameObject Find(string name)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == name && go.transform.parent == null && !EditorUtility.IsPersistent(go))
            {
                return go;
            }
        }
        return null;
    }

    [MenuItem("L2/Salles de clan/Retirer les maquettes d'edition", false, 945)]
    public static void RemoveAll()
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go != null && go.transform.parent == null && !EditorUtility.IsPersistent(go)
                && (go.name.StartsWith("ClanHallDecor_") || go.name.StartsWith("ClanHallFurniture_"))
                && go.name.EndsWith("(edition)"))
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
