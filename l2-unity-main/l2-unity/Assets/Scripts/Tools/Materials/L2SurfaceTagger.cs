#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Le son de pas depend du TAG de l'objet sous le joueur : SurfaceDetector lance
// un rayon vers le bas et AudioManager.PlayStepSound aiguille sur dirt / stone /
// wood, tout le reste tombant sur default_run.
//
// Shnok a tague ses quatre regions a la main - 331 Stone et 103 Wood pour 17_25.
// Les regions importees n'ont aucun tag : 4210 objets Untagged pour 17_22, d'ou
// un son unique partout. On deduit donc le tag du nom de materiau.
public static class L2SurfaceTagger
{
    // Regions de reference, taguees a la main : on n'y touche pas.
    private static readonly HashSet<string> Excluded = new HashSet<string>
    {
        "16_24", "16_25", "17_24", "17_25",
    };

    // GroundMask du composant World : Terrain, StaticMesh, Brush.
    private const int GroundMask = (1 << 3) | (1 << 7) | (1 << 8);

    // Ordre significatif : le bois se nomme, la pierre se deduit. Un
    // "wood_floor" doit donc etre lu comme bois, pas comme sol.
    private static readonly (string[] Words, string Tag)[] Rules =
    {
        (new[] { "wood", "plank", "deck", "timber" }, "Wood"),
        (new[] { "grass", "leaf", "moss" }, "Grass"),
        (new[] { "sand", "beach", "desert" }, "Sand"),
        (new[] { "dirt", "soil", "mud", "road", "path" }, "Dirt"),
        (new[] { "floor", "stair", "step", "tile", "pave", "brick",
                 "marble", "stone", "rock", "bottom", "carpet" }, "Stone"),
    };

    [MenuItem("L2/Outils/Surfaces - rapport (ne modifie rien)", false, 560)]
    private static void Report()
    {
        Run(false);
    }

    [MenuItem("L2/Outils/Surfaces - appliquer", false, 561)]
    private static void Apply()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (EditorUtility.DisplayDialog("Taguer les surfaces",
                "Les prefabs et scenes de region vont etre reecrits.\n\n"
                + "Talking Island (16_24, 16_25, 17_24, 17_25) est exclue.",
                "Appliquer", "Annuler"))
        {
            Run(true);
        }
    }

    private static void Run(bool write)
    {
        var counts = new Dictionary<string, int>();
        int terrains = 0, skipped = 0, assets = 0;

        try
        {
            assets += RunPrefabs(write, counts, ref terrains, ref skipped);
            assets += RunScenes(write, counts, ref terrains, ref skipped);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        var sb = new StringBuilder($"[Surfaces] {(write ? "APPLIQUE" : "RAPPORT")} - "
                                   + $"{assets} asset(s) touche(s)\n");

        foreach (KeyValuePair<string, int> c in counts)
        {
            sb.AppendLine($"   {c.Value,6}  {c.Key}");
        }

        sb.AppendLine($"   {skipped,6}  aucune regle - laisses Untagged");
        sb.AppendLine($"   {terrains,6}  terrains ignores (le sol naturel demande la couche MicroSplat)");

        Debug.Log(sb.ToString());
    }

    private static int RunPrefabs(bool write, Dictionary<string, int> counts,
        ref int terrains, ref int skipped)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Data/Maps" });
        int touched = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            if (IsExcluded(path))
            {
                continue;
            }

            EditorUtility.DisplayProgressBar("Surfaces - prefabs", path, (float)i / guids.Length);

            GameObject root = write
                ? PrefabUtility.LoadPrefabContents(path)
                : AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (root == null)
            {
                continue;
            }

            int n = TagHierarchy(root, write, counts, ref terrains, ref skipped);

            if (write)
            {
                if (n > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }

                PrefabUtility.UnloadPrefabContents(root);
            }

            if (n > 0)
            {
                touched++;
            }
        }

        return touched;
    }

    // 17_22 porte ses 515 brushes directement dans sa scene : les prefabs ne
    // suffisent pas.
    private static int RunScenes(bool write, Dictionary<string, int> counts,
        ref int terrains, ref int skipped)
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Resources/Scenes" });
        int touched = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            if (IsExcluded(path))
            {
                continue;
            }

            EditorUtility.DisplayProgressBar("Surfaces - scenes", path, (float)i / guids.Length);

            Scene scene = write
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            int n = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                n += TagHierarchy(root, write, counts, ref terrains, ref skipped);
            }

            if (write && n > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (!write)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            if (n > 0)
            {
                touched++;
            }
        }

        return touched;
    }

    private static int TagHierarchy(GameObject root, bool write, Dictionary<string, int> counts,
        ref int terrains, ref int skipped)
    {
        int n = 0;

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            GameObject go = r.gameObject;

            if ((GroundMask & (1 << go.layer)) == 0 || go.GetComponent<Collider>() == null)
            {
                continue;
            }

            // Deja tague a la main : on ne recouvre pas une decision d'auteur.
            if (go.CompareTag("Stone") || go.CompareTag("Wood") || go.CompareTag("Grass")
                || go.CompareTag("Dirt") || go.CompareTag("Sand"))
            {
                continue;
            }

            string tag = Resolve(r);

            if (tag == null)
            {
                skipped++;
                continue;
            }

            counts.TryGetValue(tag, out int c);
            counts[tag] = c + 1;

            n++;

            if (write)
            {
                go.tag = tag;
            }
        }

        foreach (Terrain t in root.GetComponentsInChildren<Terrain>(true))
        {
            terrains++;
        }

        return n;
    }

    private static string Resolve(Renderer r)
    {
        foreach (Material m in r.sharedMaterials)
        {
            if (m == null)
            {
                continue;
            }

            string name = m.name.ToLowerInvariant();

            foreach ((string[] words, string tag) in Rules)
            {
                foreach (string w in words)
                {
                    if (name.Contains(w))
                    {
                        return tag;
                    }
                }
            }
        }

        return null;
    }

    private static bool IsExcluded(string path)
    {
        foreach (string region in Excluded)
        {
            if (path.Contains("/" + region + "/") || path.Contains("/" + region + "."))
            {
                return true;
            }
        }

        return false;
    }
}
#endif
