#if (UNITY_EDITOR)
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Enchaine d'un seul tenant les etapes Unity de l'import d'une region.
///
/// POURQUOI CE FICHIER
/// Les entrees du menu Shnok ouvrent chacune un EditorUtility.OpenFilePanel.
/// C'est acceptable pour une region isolee, mais pas pour en importer
/// plusieurs : sept dialogues par region, un ordre a respecter de tete, et
/// une seule erreur de selection suffit a produire une scene silencieusement
/// fausse (deja arrive avec un .t3d pris dans le dossier de travail au lieu
/// du projet). OpenFilePanel gele en outre Unity en -batchmode, donc aucun
/// automatisme ne pouvait passer par ces entrees.
///
/// Chaque etape a donc ete scindee en un worker sans dialogue, appele ici
/// dans le bon ordre. L'ordre n'est plus une consigne a suivre : il est ecrit
/// dans le code.
///
/// L'ORDRE EST CRITIQUE, en particulier 01 -> 02 -> 03 :
/// a l'import d'un FBX, Unity cherche un materiau du meme nom dans tout le
/// projet et, faute de le trouver, en cree un vide. Les materiaux textures
/// n'existant qu'apres l'etape 02, l'etape 01 lie donc les modeles a des
/// coquilles vides. L'etape 02 les remplace (via RebindModelMaterials) et
/// reimporte les modeles, mais les objets deja poses en scene gardent leurs
/// anciennes references : il faut rejouer 03 APRES 02, sans quoi la scene
/// vire au magenta au premier rechargement.
///
/// APPEL EN LOT
///   Unity.exe -batchmode -quit -projectPath &lt;projet&gt; \
///     -executeMethod L2MapBatchImporter.BatchImportMap -mapName 17_22
public static class L2MapBatchImporter
{
    private const string ScenesFolder = "Assets/Resources/Scenes";

    [MenuItem("Shnok/Import complet d'une region (01 a 07)")]
    static void ImportCompleteRegionFromMenu()
    {
        string fileToProcess = EditorUtility.OpenFilePanel(
            "Select terrain t3d",
            Path.Combine(Application.dataPath, "Resources/Data/Maps"),
            "t3d");

        if (string.IsNullOrEmpty(fileToProcess))
        {
            return;
        }

        string mapName = Path.GetFileNameWithoutExtension(fileToProcess);

        bool ok = EditorUtility.DisplayDialog(
            "Import complet de " + mapName,
            "Va enchainer les etapes 01 a 07, creer la scene "
            + mapName + ".unity et la sauvegarder.\n\n"
            + "La scene ouverte sera remplacee. Comptez plusieurs minutes.",
            "Lancer", "Annuler");

        if (ok)
        {
            RunImport(mapName, saveScene: true);
        }
    }

    /// Point d'entree -batchmode. Lit -mapName sur la ligne de commande.
    public static void BatchImportMap()
    {
        string mapName = GetCommandLineArg("-mapName");
        if (string.IsNullOrEmpty(mapName))
        {
            Debug.LogError("[Import] Argument -mapName manquant.");
            EditorApplication.Exit(1);
            return;
        }

        if (!RunImport(mapName, saveScene: true))
        {
            EditorApplication.Exit(1);
            return;
        }

        EditorApplication.Exit(0);
    }

    /// Enchaine les sept etapes. Rend false au premier echec bloquant.
    public static bool RunImport(string mapName, bool saveScene)
    {
        return RunImport(mapName, saveScene, packageAsPrefabs: true);
    }

    /// <param name="packageAsPrefabs">
    /// Sauvegarde le Terrain, les StaticMeshes et les Brushes generes en
    /// prefabs sous Data/Maps/{region}/, comme le font 16_24/16_25/17_24/17_25
    /// (convention manuelle chez Shnok, jamais outillee jusqu'ici). Pas requis
    /// au runtime - SceneLoader charge chaque region comme une scene additive,
    /// jamais en instanciant un prefab - mais aligne la structure sur les
    /// regions existantes et permet d'ouvrir/editer un Terrain seul en mode
    /// Prefab sans charger toute la scene.
    /// </param>
    public static bool RunImport(string mapName, bool saveScene, bool packageAsPrefabs)
    {
        string t3d = T3DPathFor(mapName);
        if (!File.Exists(t3d))
        {
            Debug.LogError($"[Import] .t3d introuvable : {t3d}. "
                           + "Lancez d'abord import-map.ps1 pour cette region.");
            return false;
        }

        DateTime started = DateTime.Now;
        Debug.Log($"[Import] === {mapName} : debut ===");

        try
        {
            // Verification en tete de log : une texture de couche sans entree
            // dans textureMatches ne produit aucun .terrainlayer, et le
            // terrain rend rose a cet endroit - constate sur 17_22
            // (GUG102/GUS110) sans aucun signal avant l'inspection visuelle.
            // On le signale ici, avant de generer quoi que ce soit.
            WarnAboutTextureCoverage(mapName);

            // La scene doit exister AVANT l'etape 03 : les etapes 03, 04 et 07
            // deposent leurs objets dans la scene ouverte. Sans cela ils
            // atterrissent dans celle qui se trouvait ouverte - le terrain
            // s'etait deja retrouve dans la scene de menu.
            //
            // EmptyScene, pas DefaultGameObjects : aucune des 4 regions de
            // reference (16_24, 16_25, 17_24, 17_25) n'a de Main Camera ni de
            // Directional Light dans sa scene - DefaultGameObjects en ajoutait
            // une, ce qui a introduit un eclairage parasite absent de la
            // convention (repere sur 17_23 : reflet bleute residuel).
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                                                      NewSceneMode.Single);
            Debug.Log($"[Import] {mapName} : scene vierge creee.");

            Step(1, "import des modeles et textures");
            L2T3DStaticMeshImporter.ImportStaticMeshesFrom(t3d);
            AssetDatabase.Refresh();

            Step(2, "generation des materiaux (+ rebranchement)");
            L2MaterialBuilder.SetupMaterials();
            AssetDatabase.Refresh();

            // Apres 02 seulement : les modeles viennent d'etre reimportes sur
            // les materiaux textures, les objets poses ici seront corrects.
            Step(3, "placement des static meshes");
            L2TerrainGeneratorTool.GenerateStaticMeshesFor(mapName);

            Step(4, "generation du terrain");
            L2TerrainGeneratorTool.GenerateTerrainFor(mapName);

            Step(5, "conversion MicroSplat");
            L2TerrainGeneratorTool.ConvertTerrainFor(mapName);

            Step(6, "parametres MicroSplat");
            L2TerrainGeneratorTool.UpdateMicrosplatFor(mapName);

            Step(7, "construction des brushes");
            L2BrushBuilder.BuildBrushesFrom(t3d);

            // Avant l'empaquetage : les troncs doivent etre dans les objets
            // sauvegardes en prefab. Sans cette passe, le collider du
            // feuillage bloque le joueur en hauteur et la geodata marque
            // toute la couronne comme infranchissable.
            Step(8, "troncs des arbres (collider + layer Unwalkable)");
            int trunks = AddTrunks.AddTrunksToTrees();
            Debug.Log($"[Import] {trunks} arbre(s) pourvu(s) d'un tronc.");

            // Phase 2 : le .t3d contient desormais aussi les AmbientSoundObject
            // (jusqu'a ~1500 par region) - construit ici l'objet "AmbientSounds"
            // que SaveGeneratedPrefabs empaquette juste apres, comme le fait deja
            // Shnok 09/10 a la main.
            Step(9, "sons d'ambiance");
            L2AmbientSoundBuilder.BuildAmbientSoundsFrom(t3d);

            // Certaines regions (17_23 par exemple) n'ont tout simplement aucun
            // acteur Light dans le .unr d'origine - un conteneur "Lights" vide
            // est alors cree, sans que ce soit une erreur.
            Step(10, "eclairages ponctuels");
            L2LightBuilder.BuildLightsFrom(t3d);

            // Water et Safenet sont des objets FIXES (meme echelle, meme
            // position locale sur les 4 regions de reference verifiees :
            // 16_24, 16_25, 17_24, 17_25) - on les clone tels quels plutot
            // que de recalculer une taille/hauteur par region. Une premiere
            // version mettait l'eau a l'echelle du terrain de la region
            // cible, ce qui la rendait bien trop grande sur 17_23.
            Step(11, "plan d'eau");
            L2WaterBuilder.BuildWaterFrom(mapName);

            Step(12, "filet de securite");
            L2SafenetBuilder.BuildSafenetFor(mapName);

            // Pas de grille de sondes de reflexion ici : verifie sur les 4
            // regions de reference, 3 sur 4 (16_24, 16_25, 17_24) n'ont NI
            // Light NI ReflectionProbe du tout. Seule 17_25 en a (5 sondes
            // posees a la main pres de points d'interet precis). Une grille
            // automatique ne correspond donc pas a la convention majoritaire -
            // disponible en manuel via Shnok/[Debug][Light] si besoin au cas
            // par cas, mais plus enchainee par defaut.

            if (packageAsPrefabs)
            {
                Step(13, "empaquetage en prefabs (Terrain+Water+Safenet/StaticMeshes/Brushes/AmbientSounds/Lights)");
                SaveGeneratedPrefabs(mapName);
            }

            if (saveScene)
            {
                Directory.CreateDirectory(ScenesFolder);
                string scenePath = $"{ScenesFolder}/{mapName}.unity";
                EditorSceneManager.SaveScene(scene, scenePath);
                Debug.Log($"[Import] {mapName} : scene sauvegardee dans {scenePath}");

                // Une scene non declaree ne peut pas etre chargee en jeu :
                // SceneLoader la demande par son nom. C'est le dernier maillon
                // entre "region importee" et "region jouable".
                Step(14, "declaration de la region (Build Settings + SceneLoader)");
                L2MapSceneRegistrar.RegisterRegion(mapName);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ReportRegionHealth(mapName);

            Debug.Log($"[Import] === {mapName} : termine en "
                      + $"{(DateTime.Now - started).TotalSeconds:F0}s ===");
            return true;
        }
        catch (Exception e)
        {
            // Sans ce filet, une exception en -batchmode laisse Unity rendre 0
            // et le script appelant croirait a une reussite.
            Debug.LogError($"[Import] {mapName} : echec - {e}");
            return false;
        }
    }

    /// Ajoute l'eau et le filet de securite a une region DEJA importee et
    /// empaquetee (ex. 17_23, importee avant que ces etapes n'existent).
    /// Rejouer RunImport en entier serait a la fois inutile et risque : ca
    /// regenererait terrain/static meshes/materiaux deja valides. Ne touche
    /// donc que le Terrain (qui recoit Water et Safenet en enfants) et
    /// resauvegarde uniquement son prefab.
    ///
    /// Ecrase tout objet "Water"/"Safenet" deja present sous le meme nom -
    /// si un essai manuel anterieur porte un autre nom ou n'est pas enfant du
    /// Terrain, il ne sera pas supprime automatiquement, a nettoyer a la main.
    ///
    /// Ne pose PAS de sondes de reflexion : verifie sur les 4 regions de
    /// reference, 3 sur 4 n'en ont aucune (cf. L2ReflectionProbeBuilder).
    ///
    /// Prerequis : la scene de la region doit etre ouverte (le Terrain doit
    /// etre trouvable par GameObject.Find(mapName) dans la scene active).
    [MenuItem("Shnok/[Retrofit] Ajouter eau + safenet")]
    public static void AddWaterAndSafenetToOpenScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        string mapName = Path.GetFileNameWithoutExtension(scene.path);
        if (string.IsNullOrEmpty(mapName))
        {
            Debug.LogError("[Retrofit] Aucune scene de region active/sauvegardee.");
            return;
        }

        L2WaterBuilder.BuildWaterFrom(mapName);
        L2SafenetBuilder.BuildSafenetFor(mapName);

        string mapFolder = $"Assets/Resources/Data/Maps/{mapName}";
        SaveObjectAsPrefab(mapName, $"{mapFolder}/{mapName}.prefab");

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Retrofit] {mapName} : eau + safenet ajoutes et sauvegardes.");
    }

    /// Enchaine RunImport pour plusieurs regions dans le MEME processus Unity.
    ///
    /// Chaque lancement d'Unity paie l'ouverture et la recompilation du
    /// projet (plusieurs minutes) avant meme de commencer le travail utile.
    /// Sur un grand nombre de regions, ce cout fixe domine largement le temps
    /// reel de traitement - le payer une seule fois pour N regions au lieu de
    /// N fois change l'echelle de temps du travail par lot.
    public static bool RunImportBatch(string[] mapNames)
    {
        var results = new System.Collections.Generic.List<(string map, bool ok)>();

        foreach (string mapName in mapNames)
        {
            bool ok = RunImport(mapName, saveScene: true);
            results.Add((mapName, ok));
        }

        Debug.Log("[Import] === Resume du lot ===");
        int failures = 0;
        foreach (var (map, ok) in results)
        {
            Debug.Log($"[Import]   {map,-12} {(ok ? "OK" : "ECHEC")}");
            if (!ok)
            {
                failures++;
            }
        }
        Debug.Log($"[Import] {results.Count - failures}/{results.Count} region(s) importee(s) avec succes.");

        return failures == 0;
    }

    /// Point d'entree -batchmode pour plusieurs regions. Lit -mapNames
    /// (separees par des virgules) ou -mapListFile (un fichier, une region
    /// par ligne, lignes vides et commencant par # ignorees).
    public static void BatchImportMaps()
    {
        string[] mapNames = null;

        string namesArg = GetCommandLineArg("-mapNames");
        if (!string.IsNullOrEmpty(namesArg))
        {
            mapNames = namesArg.Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();
        }

        string listFileArg = GetCommandLineArg("-mapListFile");
        if (mapNames == null && !string.IsNullOrEmpty(listFileArg) && File.Exists(listFileArg))
        {
            mapNames = File.ReadAllLines(listFileArg)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0 && !s.StartsWith("#"))
                .ToArray();
        }

        if (mapNames == null || mapNames.Length == 0)
        {
            Debug.LogError("[Import] Argument -mapNames ou -mapListFile manquant ou vide.");
            EditorApplication.Exit(1);
            return;
        }

        bool ok = RunImportBatch(mapNames);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    /// Bilan chiffre de fin d'import.
    ///
    /// Chaque ligne correspond a un bug rencontre en conditions reelles et
    /// decouvert seulement a l'inspection visuelle, parfois plusieurs seances
    /// plus tard : objets sans collider (on traverse tout), layer 0 (la
    /// geodata client ne voit rien), materiaux magenta (references mortes).
    /// Les compter ici transforme une inspection oculaire en une ligne de log.
    private static void ReportRegionHealth(string mapName)
    {
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        int renderers = 0, colliders = 0, defaultLayer = 0, missingMaterial = 0;

        foreach (GameObject root in roots)
        {
            foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;

                if (mr.gameObject.layer == 0)
                {
                    defaultLayer++;
                }

                foreach (Material mat in mr.sharedMaterials)
                {
                    if (mat == null)
                    {
                        missingMaterial++;
                        break;
                    }
                }
            }

            colliders += root.GetComponentsInChildren<MeshCollider>(true).Length;
        }

        Debug.Log($"[Bilan] {mapName} : {renderers} renderer(s), {colliders} collider(s), "
                  + $"{defaultLayer} sur le layer 0, {missingMaterial} sans materiau.");

        if (renderers > 0 && colliders == 0)
        {
            Debug.LogWarning($"[Bilan] {mapName} : AUCUN collider - la region sera entierement "
                             + "traversable. Verifier addCollider a l'import des FBX (etape 01).");
        }

        if (defaultLayer > 0)
        {
            Debug.LogWarning($"[Bilan] {mapName} : {defaultLayer} objet(s) sur le layer 0 (Default) - "
                             + "invisibles pour le GeodataGenerator, qui filtre par layer.");
        }

        if (missingMaterial > 0)
        {
            Debug.LogWarning($"[Bilan] {mapName} : {missingMaterial} renderer(s) sans materiau - "
                             + "rendu magenta. Rejouer l'etape 02 puis l'etape 03.");
        }
    }

    private static void WarnAboutTextureCoverage(string mapName)
    {
        L2TerrainInfo terrainInfo = L2T3DInfoParser.LoadMetadata(mapName);

        var missingCritical = L2TerrainGeneratorTextureMatcher.FindMissingTextureMatches(terrainInfo);
        if (missingCritical.Count > 0)
        {
            Debug.LogWarning($"[Import] {mapName} : {missingCritical.Count} texture(s) sans entree dans "
                             + "textureMatches (terrain ROSE a cet endroit sans correction) : "
                             + string.Join(", ", missingCritical));
        }

        var missingScale = L2TerrainGeneratorTextureMatcher.FindMissingScaleMatches(terrainInfo);
        if (missingScale.Count > 0)
        {
            Debug.LogWarning($"[Import] {mapName} : {missingScale.Count} texture(s) sans entree dans "
                             + "scaleMatches (echelle par defaut, tuiles probablement mal calees) : "
                             + string.Join(", ", missingScale));
        }
    }

    /// Sauvegarde Terrain, StaticMeshes et Brushes en prefabs sous
    /// Data/Maps/{region}/, et reconnecte les objets de la scene a ces
    /// prefabs (au lieu de laisser des copies detachees).
    private static void SaveGeneratedPrefabs(string mapName)
    {
        string mapFolder = $"Assets/Resources/Data/Maps/{mapName}";
        Directory.CreateDirectory(mapFolder);

        // Le Terrain est cree sous "terrain_<region>", mais L2TerrainGenerator
        // renomme son Transform en "<region>" tout a la fin de
        // InstantiateTerrain - c'est donc ce dernier nom qui existe reellement
        // dans la scene au moment ou cette methode s'execute.
        SaveObjectAsPrefab(mapName, $"{mapFolder}/{mapName}.prefab");
        SaveObjectAsPrefab(L2TerrainGenerator.StaticMeshContainerName(mapName),
                          $"{mapFolder}/StaticMeshes.prefab");
        SaveObjectAsPrefab("Brushes", $"{mapFolder}/Brushes.prefab");
        SaveObjectAsPrefab("AmbientSounds", $"{mapFolder}/{mapName}_AmbientSounds.prefab");
        SaveObjectAsPrefab("Lights", $"{mapFolder}/Lights.prefab");
    }

    private static void SaveObjectAsPrefab(string objectName, string prefabPath)
    {
        GameObject sceneObject = GameObject.Find(objectName);
        if (sceneObject == null)
        {
            Debug.LogWarning($"[Import] '{objectName}' introuvable dans la scene, prefab non cree : {prefabPath}");
            return;
        }

        // SaveAsPrefabAsset (deja utilise ailleurs dans le projet) cree
        // l'asset mais laisse l'objet de scene detache. On le remplace donc
        // par une instance du prefab fraichement cree, a la meme position et
        // sous le meme parent - c'est la structure "PrefabInstance" qu'ont
        // deja 16_24/16_25/17_24/17_25.
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(sceneObject, prefabPath);
        if (prefabAsset == null)
        {
            Debug.LogError($"[Import] Echec de creation du prefab {prefabPath}.");
            return;
        }

        Transform originalParent = sceneObject.transform.parent;
        Vector3 position = sceneObject.transform.position;
        Quaternion rotation = sceneObject.transform.rotation;
        Vector3 scale = sceneObject.transform.localScale;

        UnityEngine.Object.DestroyImmediate(sceneObject);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
        instance.name = objectName;
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = scale;
        if (originalParent != null)
        {
            instance.transform.SetParent(originalParent, worldPositionStays: true);
        }

        Debug.Log($"[Import] '{objectName}' -> {prefabPath}");
    }

    private static void Step(int n, string label)
    {
        Debug.Log($"[Import] etape {n:00} : {label}");
    }

    /// Le .t3d de reference est celui DU PROJET, jamais celui du dossier de
    /// travail : c'est le seul voisin de Brushes.json, dont l'etape 07 a
    /// besoin quand le .t3d ne porte pas les polygones.
    public static string T3DPathFor(string mapName)
    {
        return Path.Combine(Application.dataPath,
                            "Resources/Data/Maps", mapName, "Meta", mapName + ".t3d");
    }

    private static string GetCommandLineArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
#endif
