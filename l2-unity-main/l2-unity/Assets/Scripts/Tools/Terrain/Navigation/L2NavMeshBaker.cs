#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

/// Cuit un NavMesh par region, en remplacement de la geodata client.
///
/// POURQUOI
/// La geodata client coute 136 Mo par region en memoire, contre 8,7 Mo sur
/// disque : Node est une CLASSE (un objet sur le tas par noeud) rangee dans un
/// tableau dense 1249x4x1249 occupe a 23%. A comparer aux 22 Mo de texture
/// arrays - la geodata coute six fois le terrain qu'elle decrit.
///
/// Or le moteur connait deja cette geometrie : c'est precisement ce que
/// GeodataGenerator interroge par lancer de rayons pour produire ses fichiers.
///
/// LE NAVMESH SE STREAME NATIVEMENT
/// Chaque region recoit un NavMeshSurface dont les donnees sont un asset a
/// part. Unity l'ajoute et le retire automatiquement avec la scene, sans code
/// de notre part - contrairement a Geodata, qui garde tout en memoire jusqu'a
/// la fin de la session.
///
/// LE VOLUME EST BORNE A LA REGION
/// On utilise CollectObjects.Volume plutot que All : sans cela, une cuisson
/// lancee avec plusieurs regions chargees les engloberait toutes, et le meme
/// terrain serait cuit plusieurs fois.
public static class L2NavMeshBaker
{
    private const string ScenesFolder = "Assets/Resources/Scenes";

    /// Hauteur du volume de cuisson, en unites Unity. Le relief de L2 depasse
    /// rarement 300 unites d'amplitude ; 1000 laisse de la marge sans gonfler
    /// le temps de cuisson.
    private const float VolumeHeight = 1000f;

    [MenuItem("L2/Navigation/Cuire le NavMesh (scene ouverte)", false, 200)]
    public static void BakeCurrentScene()
    {
        Scene active = EditorSceneManager.GetActiveScene();
        string mapName = Path.GetFileNameWithoutExtension(active.path);

        if (string.IsNullOrEmpty(mapName)
            || !System.Text.RegularExpressions.Regex.IsMatch(mapName, @"^\d+_\d+$"))
        {
            Debug.LogError("[NavMesh] Ouvrez d'abord la scene d'une region (ex. 17_23.unity).");
            return;
        }

        if (BakeFor(mapName))
        {
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
            AssetDatabase.SaveAssets();
        }
    }

    /// Cuit les 153 regions d'affilee.
    ///
    /// Reutilise le lot pas-a-pas de L2MapBatchImporter : une region par tick
    /// de l'editeur. Ici ce n'est pas MicroSplat qui l'impose - BuildNavMesh
    /// est synchrone - mais la barre de progression annulable et l'isolation
    /// des erreurs, sur une operation qui dure une vingtaine de minutes.
    ///
    /// Les quatre regions de reference NE sont PAS exclues, contrairement aux
    /// passes de texture : Talking Island est la zone de depart, elle a besoin
    /// de navigation. La cuisson n'ajoute qu'un NavMeshSurface et ne touche ni
    /// aux couches, ni aux materiaux, ni au relief.
    [MenuItem("L2/Navigation/Cuire le NavMesh (TOUTES les regions)", false, 201)]
    public static void BakeAll()
    {
        string[] regions = L2MapBatchImporter.EnumerateRegionScenes();

        if (regions.Length == 0)
        {
            Debug.LogWarning("[NavMesh] Aucune region trouvee.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Cuire le NavMesh de toutes les regions",
                $"{regions.Length} region(s) vont etre traitees.\n\n"
                + "Chaque scene est ouverte, cuite, puis enregistree.\n"
                + "Compter ~10 s par region, soit ~25 min au total.\n\n"
                + "Une region deja cuite est recuite : son asset est remplace.\n"
                + "L'operation est annulable a tout moment, sans dommage.\n\n"
                + "Continuer ?",
                "Lancer", "Annuler"))
        {
            return;
        }

        // OpenScene n'invite PAS a enregistrer : le travail non sauvegarde de la
        // scene courante serait perdu des la premiere region, sans un mot.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("[NavMesh] Annule : la scene ouverte a des modifications non enregistrees.");
            return;
        }

        L2MapBatchImporter.StartSteppedBatch(regions, BakeStep, "Cuisson des NavMesh", "[NavMesh]");
    }

    private static bool BakeStep(string mapName)
    {
        Scene scene = EditorSceneManager.OpenScene($"{ScenesFolder}/{mapName}.unity", OpenSceneMode.Single);

        if (!BakeFor(mapName))
        {
            return false;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    /// Cuit la region ouverte dans la scene courante.
    /// Retourne false si la region n'a pas pu etre traitee.
    public static bool BakeFor(string mapName)
    {
        Terrain terrain = FindTerrain(mapName);
        if (terrain == null)
        {
            // Avertissement et non erreur : certaines tuiles sont vides (24_22
            // n'a aucun terrain). Sur un lot de 153, autant d'erreurs rouges
            // noieraient les vrais echecs. Le decompte final fait foi.
            Debug.LogWarning($"[NavMesh] {mapName} : aucun terrain dans la scene, region ignoree.");
            return false;
        }

        // Le surface vit sur le terrain lui-meme : il suit donc la region quand
        // celle-ci est chargee ou dechargee par le streaming.
        NavMeshSurface surface = terrain.GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            surface = terrain.gameObject.AddComponent<NavMeshSurface>();
        }

        Vector3 size = terrain.terrainData.size;

        surface.collectObjects = CollectObjects.Volume;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.center = new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        surface.size = new Vector3(size.x, VolumeHeight, size.z);

        var watch = Stopwatch.StartNew();
        surface.BuildNavMesh();
        watch.Stop();

        if (surface.navMeshData == null)
        {
            Debug.LogError($"[NavMesh] {mapName} : la cuisson n'a produit aucune donnee. "
                           + "Verifiez que le terrain a un collider et que le volume le couvre.");
            return false;
        }

        // Les donnees doivent etre persistees : un NavMeshData non sauvegarde
        // serait perdu au rechargement, exactement comme les TerrainLayer que
        // MicroSplat cree en differe.
        string folder = $"Assets/Resources/Data/Maps/{mapName}/TerrainData";
        string assetPath = $"{folder}/{mapName}_NavMesh.asset";

        // RECUISSON : BuildNavMesh cree un NOUVEL objet NavMeshData a chaque
        // appel (AI Navigation, NavMeshSurface.BuildNavMesh). Marquer l'ancien
        // asset dirty n'ecrirait donc rien : la scene referencerait un objet
        // resté en memoire, perdu au rechargement, et le fichier sur disque
        // resterait celui de la cuisson precedente.
        //
        // On supprime puis on recree, ce que fait Unity elle-meme dans
        // NavMeshAssetManager.ClearSurface avant chaque cuisson.
        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath) != null)
        {
            AssetDatabase.DeleteAsset(assetPath);
        }

        AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
        AssetDatabase.SaveAssets();

        long bytes = new FileInfo(assetPath).Length;
        Debug.Log($"[NavMesh] {mapName} cuit en {watch.ElapsedMilliseconds} ms, "
                  + $"{bytes / 1024} Ko sur disque. "
                  + $"(La geodata equivalente pese ~136 Mo en memoire.)");

        return true;
    }

    private static Terrain FindTerrain(string mapName)
    {
        GameObject go = GameObject.Find(L2TerrainGenerator.TerrainObjectName(mapName))
                        ?? GameObject.Find(mapName);

        return go != null ? go.GetComponentInChildren<Terrain>(true) : null;
    }
}
#endif
