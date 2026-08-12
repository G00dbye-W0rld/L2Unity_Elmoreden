#if (UNITY_EDITOR)
using System;
using System.Collections.Generic;
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

        // Unity attache une trace de pile COMPLETE (~30 lignes, capturees via
        // StackWalker) a CHAQUE Debug.Log. Mesure sur 22_19 le 03/08/2026 :
        // 13 469 appels pour une seule region, soit un journal de 204 Mo.
        // On coupe la trace pour les logs informatifs seulement : les
        // avertissements et les erreurs gardent la leur, qui est justement ce
        // qui sert a diagnostiquer.
        StackTraceLogType previousLogTrace =
            Application.GetStackTraceLogType(LogType.Log);
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

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

            CleanPreviousTerrainData(mapName);

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
        finally
        {
            // Indispensable : lance depuis le menu Shnok, l'editeur resterait
            // sinon sans trace de pile sur les logs pour le reste de la
            // session, y compris apres une exception.
            Application.SetStackTraceLogType(LogType.Log, previousLogTrace);
        }
    }

    /// Re-applique les substitutions de textures de terrain (textureMatches /
    /// scaleMatches) sur des regions DEJA importees, SANS reimport.
    ///
    /// POURQUOI C'EST POSSIBLE SANS TOUT REFAIRE
    /// Les substitutions PBR sont appliquees a l'etape 06
    /// (UpdateMicrosplatParams), qui ecrit dans la config MicroSplat - PAS
    /// dans le TerrainData. Or la separation sur disque est nette :
    ///
    ///   TerrainData/
    ///     {region}.asset          <- hauteurs (stitch) + splatmaps (peinture)
    ///     {region}_layer_*.asset  <- couches de terrain
    ///     MicroSplatData/         <- config + texture arrays  (SEUL supprime)
    ///
    /// On ne supprime donc que MicroSplatData/ avant de rejouer 05 et 06.
    /// **Le raccord de terrain, la peinture manuelle et les objets ajoutes a
    /// la main sont preserves.** C'est la difference majeure avec un reimport,
    /// qui recree le TerrainData de zero (AssetDatabase.CreateAsset) et efface
    /// tout ca.
    ///
    /// La suppression prealable est indispensable : MicroSplat cree ses assets
    /// via GenerateUniqueAssetPath(), qui n'ecrase pas mais cree un doublon
    /// suffixe " 1" (cf. CleanPreviousTerrainData).
    /// Vide MicroSplatData de sa config, de son materiau et de son shader, mais
    /// PRESERVE les fichiers .terrainlayer.
    ///
    /// POURQUOI CETTE PRECAUTION
    /// La version precedente supprimait le dossier entier avant de relancer
    /// l'etape 05. Or l'etape 05 reconstruit la config a partir de
    /// terrain.terrainData.terrainLayers - et ces couches sont justement des
    /// assets .terrainlayer stockes DANS MicroSplatData. On detruisait donc la
    /// source de verite, puis on reconstruisait a partir d'elle : la config
    /// ressortait avec le bon nombre d'entrees mais toutes vides
    /// (terrainLayer et diffuse a fileID 0).
    ///
    /// Degats constates le 2026-08-07 : 6 regions videes de leurs textures, dont
    /// les 4 regions de reference de Talking Island. Recuperees par git, mais
    /// les 147 autres ne sont pas suivies - la meme operation aurait ete
    /// definitive.
    ///
    /// La documentation MicroSplat le dit d'ailleurs explicitement : les
    /// fichiers de couches ajoutes au terrain vivent dans MicroSplatData, et il
    /// faut les mettre a l'abri avant de supprimer le dossier.
    ///
    /// Supprimer la config et le materiau suffit a obtenir ce qu'on cherchait au
    /// depart : eviter que MicroSplat n'empile des doublons " 1" via
    /// AssetDatabase.GenerateUniqueAssetPath.
    private static void CleanMicroSplatKeepingLayers(string mapName)
    {
        string folder = $"Assets/Resources/Data/Maps/{mapName}/TerrainData/MicroSplatData";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        int deleted = 0, kept = 0;
        foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // Le seul type a ne jamais toucher : c'est lui que le terrain
            // reference, et donc la source de la reconstruction.
            if (path.EndsWith(".terrainlayer", System.StringComparison.OrdinalIgnoreCase))
            {
                kept++;
                continue;
            }

            if (AssetDatabase.DeleteAsset(path))
            {
                deleted++;
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[Textures] {mapName} : MicroSplatData nettoye - "
                  + $"{deleted} asset(s) supprime(s), {kept} couche(s) preservee(s).");
    }

    public static bool ReapplySubstitutionsFor(string mapName)
    {
        string scenePath = $"{ScenesFolder}/{mapName}.unity";
        if (!File.Exists(scenePath))
        {
            Debug.LogError($"[Textures] Scene introuvable pour {mapName} : {scenePath}");
            return false;
        }

        // Le matcher est un singleton construit une fois par session : sans
        // cette relecture, une modification de l'asset de reglages resterait
        // sans effet jusqu'au redemarrage d'Unity.
        L2TerrainGeneratorTextureMatcher.Reload();

        try
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            CleanMicroSplatKeepingLayers(mapName);

            L2TerrainGeneratorTool.ConvertTerrainFor(mapName);   // etape 05
            L2TerrainGeneratorTool.UpdateMicrosplatFor(mapName); // etape 06

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Textures] {mapName} : substitutions re-appliquees "
                      + "(stitch et peinture preserves).");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Textures] {mapName} : echec - {e}");
            return false;
        }
    }

    /// Version lot : enchaine ReapplySubstitutionsFor sur plusieurs regions
    /// dans le meme processus Unity.
    public static bool ReapplySubstitutionsBatch(string[] mapNames)
    {
        StackTraceLogType previous = Application.GetStackTraceLogType(LogType.Log);
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        int ok = 0, done = 0;
        bool aborted = false;
        try
        {
            for (int i = 0; i < mapNames.Length; i++)
            {
                // Sans ce test, la boucle etait ININTERRUPTIBLE : un simple
                // foreach synchrone, sans barre de progression. Lancee sur 149
                // regions a ~1 min piece, elle bloquait l'editeur pendant des
                // heures et il fallait tuer Unity pour l'arreter. Constate le
                // 2026-08-09.
                //
                // L'interruption est sans risque : chaque region est sauvegardee
                // au fur et a mesure, et le nettoyage preserve les .terrainlayer.
                // Seule la region en cours au moment de l'arret reste sans
                // MicroSplatData - la retraiter seule suffit a la reparer.
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Re-application des substitutions",
                        $"{mapNames[i]} ({i + 1}/{mapNames.Length}) - {ok} reussie(s)",
                        (float)i / mapNames.Length))
                {
                    aborted = true;
                    break;
                }

                done++;
                if (ReapplySubstitutionsFor(mapNames[i])) { ok++; }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Application.SetStackTraceLogType(LogType.Log, previous);
        }

        if (aborted)
        {
            Debug.LogWarning($"[Textures] Interrompu : {ok}/{done} region(s) traitee(s) avec succes "
                             + $"sur {mapNames.Length} prevues. Relancer reprend tout depuis le debut, "
                             + "sans dommage.");
            return false;
        }

        Debug.Log($"[Textures] {ok}/{mapNames.Length} region(s) mise(s) a jour avec succes.");
        return ok == mapNames.Length;
    }

    /// Cree l'asset de reglages des textures, pre-rempli avec les tables
    /// actuellement codees en dur.
    ///
    /// Ecrire ce YAML a la main serait fragile (references de script, GUIDs) :
    /// on laisse Unity le creer, puis on le peuple depuis le matcher. Une fois
    /// l'asset present, c'est LUI qui fait autorite et tout se regle dans
    /// l'Inspector, sans recompilation.
    /// Supprime les .terrainlayer qu'aucun terrain ne reference plus.
    ///
    /// POURQUOI ILS S'ACCUMULENT
    /// Le nettoyage d'avant substitution preserve TOUS les .terrainlayer - c'est
    /// ce qui empeche de detruire la source de verite du terrain (incident du
    /// 2026-08-07, 6 regions videes). Mais chaque passage cree de nouvelles
    /// couches aux noms des nouveaux packs, sans effacer les anciennes.
    ///
    /// Sur 22_21 apres deux passages :
    ///   microsplat_layer_Soil_Sand_pjErQ0_1K_BaseColor_2       <- passage 1
    ///   microsplat_layer_Thai_Beach_Sand_tefnah1q_1K_..._2     <- passage 2
    ///
    /// Le terrain n'en reference qu'une ; l'autre est morte. Mesure du
    /// 2026-08-10 : 211 fichiers pour 186 indices reels, soit ~25 orphelins.
    ///
    /// Aucune scene n'est ouverte : le TerrainData se lit directement comme un
    /// asset, et c'est lui qui fait autorite sur les couches utilisees.
    // ================================================================
    //  LOT PAS A PAS — une region par tick de l'editeur
    //
    //  POURQUOI CE DETOUR
    //  MicroSplat cree les TerrainLayer en DIFFERE (MicroSplatTerrain.cs:405) :
    //
    //      protos[i] = sp;
    //      EditorApplication.delayCall += () => AssetDatabase.CreateAsset(sp, path);
    //      terrain.terrainData.terrainLayers = protos;
    //
    //  Le terrain recoit donc des couches encore EN MEMOIRE, enregistrees
    //  seulement au tick suivant de l'editeur. Une boucle foreach synchrone ne
    //  rend jamais la main : les delayCall ne partent pas, les objets finissent
    //  detruits, et la region suivante trouve un terrain sans couches - d'ou la
    //  NullReferenceException de ConvertTerrains.
    //
    //  C'est ce qui explique le symptome central : une region traitee SEULE
    //  passe (l'editeur tick apres l'entree de menu), un lot de 148 echoue.
    //  Mesure du 2026-08-11 : 114 echecs sur 148, et aucun rattrapage possible
    //  en amont ni par nouvelle tentative.
    //
    //  On traite donc UNE region par tick. L'editeur reprend la main entre
    //  chaque, les delayCall s'executent, et le lot devient exactement ce qui
    //  fonctionne deja : une succession de traitements isoles.
    // ================================================================
    private static string[] _stepMaps;
    private static int _stepIndex, _stepOk;
    private static Func<string, bool> _stepAction;
    private static string _stepTitle, _stepTag;

    private static void StartSteppedBatch(string[] mapNames)
    {
        StartSteppedBatch(mapNames, ReapplySubstitutionsFor,
            "Re-application des substitutions", "[Textures]");
    }

    /// Le traitement applique a chaque region est un parametre : la mecanique
    /// pas-a-pas sert aussi bien a la substitution complete qu'a la re-application
    /// des seules echelles.
    private static void StartSteppedBatch(string[] mapNames, Func<string, bool> action,
                                          string title, string tag)
    {
        _stepMaps = mapNames;
        _stepAction = action;
        _stepTitle = title;
        _stepTag = tag;
        _stepIndex = 0;
        _stepOk = 0;
        EditorApplication.update += StepOneRegion;
    }

    private static void StepOneRegion()
    {
        if (_stepIndex >= _stepMaps.Length)
        {
            FinishSteppedBatch($"{_stepTag} {_stepOk}/{_stepMaps.Length} region(s) mise(s) a jour avec succes.");
            return;
        }

        string mapName = _stepMaps[_stepIndex];

        if (EditorUtility.DisplayCancelableProgressBar(
                _stepTitle,
                $"{mapName} ({_stepIndex + 1}/{_stepMaps.Length}) - {_stepOk} reussie(s)",
                (float)_stepIndex / _stepMaps.Length))
        {
            FinishSteppedBatch($"{_stepTag} Interrompu : {_stepOk}/{_stepIndex} region(s) traitee(s) "
                               + "avec succes. Relancer reprend depuis le debut, sans dommage.");
            return;
        }

        _stepIndex++;

        try
        {
            if (_stepAction(mapName)) { _stepOk++; }
        }
        catch (Exception e)
        {
            // Une region ne doit jamais interrompre le lot ni laisser le
            // callback abonne dans un etat incoherent.
            Debug.LogError($"{_stepTag} {mapName} : echec inattendu - {e}");
        }
    }

    private static void FinishSteppedBatch(string message)
    {
        EditorApplication.update -= StepOneRegion;
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        Debug.Log(message);
    }

    /// Reapplique les echelles UV sans reconstruire MicroSplatData.
    ///
    /// A privilegier chaque fois que SEULE une echelle a change dans l'asset de
    /// reglages : quelques secondes par region au lieu d'une minute, puisqu'on
    /// n'efface rien et qu'aucun texture array n'est recompile.
    /// Voir L2TerrainGeneratorTool.ReapplyScalesFor.
    [MenuItem("Shnok/[Textures] Re-appliquer les ECHELLES seules (TOUTES les regions)")]
    public static void ReapplyScalesAll()
    {
        string[] regions = EnumerateRegionScenes()
            .Where(r => !L2MicroSplatReference.ReferenceRegions.Contains(r))
            .ToArray();

        if (regions.Length == 0)
        {
            Debug.LogWarning("[Echelles] Aucune region trouvee.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Re-appliquer les echelles seules",
                $"{regions.Length} region(s) vont etre traitees.\n\n"
                + "Operation LEGERE : seul le propdata est reecrit.\n"
                + "MicroSplatData, les materiaux et les texture arrays ne sont\n"
                + "ni supprimes ni recompiles.\n\n"
                + "A utiliser quand SEULE une echelle a change dans l'asset de reglages.\n\n"
                + "Continuer ?",
                "Lancer", "Annuler"))
        {
            return;
        }

        // Une seule relecture de l'asset pour tout le lot : sans elle, les
        // modifications faites dans l'Inspector resteraient invisibles.
        L2TerrainGeneratorTextureMatcher.Reload();

        StartSteppedBatch(regions, ScaleStep, "Re-application des echelles", "[Echelles]");
    }

    /// Meme traitement sur la seule region ouverte, pour juger un reglage avant
    /// de le propager.
    [MenuItem("Shnok/[Textures] Re-appliquer les ECHELLES seules (scene ouverte)")]
    public static void ReapplyScalesCurrentScene()
    {
        Scene active = EditorSceneManager.GetActiveScene();
        string mapName = Path.GetFileNameWithoutExtension(active.path);

        if (string.IsNullOrEmpty(mapName)
            || !System.Text.RegularExpressions.Regex.IsMatch(mapName, @"^\d+_\d+$"))
        {
            Debug.LogError("[Echelles] Ouvrez d'abord la scene d'une region (ex. 21_23.unity).");
            return;
        }

        L2TerrainGeneratorTextureMatcher.Reload();

        if (L2TerrainGeneratorTool.ReapplyScalesFor(mapName))
        {
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
            AssetDatabase.SaveAssets();
        }
    }

    private static bool ScaleStep(string mapName)
    {
        Scene scene = EditorSceneManager.OpenScene($"{ScenesFolder}/{mapName}.unity", OpenSceneMode.Single);

        if (!L2TerrainGeneratorTool.ReapplyScalesFor(mapName))
        {
            return false;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    [MenuItem("Shnok/[Textures] Nettoyer les couches orphelines")]
    public static void CleanOrphanTerrainLayers()
    {
        string[] regions = EnumerateRegionScenes();
        if (regions.Length == 0)
        {
            Debug.LogWarning("[Textures] Aucune region trouvee.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Nettoyer les couches orphelines",
                $"{regions.Length} region(s) vont etre examinees.\n\n"
                + "Seuls les fichiers .terrainlayer qu'AUCUN terrain ne reference\n"
                + "seront supprimes. Les couches utilisees ne sont pas touchees.\n\n"
                + "Continuer ?",
                "Analyser et nettoyer", "Annuler"))
        {
            return;
        }

        int deleted = 0, kept = 0, skipped = 0;
        try
        {
            for (int i = 0; i < regions.Length; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Nettoyage des couches",
                        $"{regions[i]} ({i + 1}/{regions.Length}) - {deleted} supprimee(s)",
                        (float)i / regions.Length))
                {
                    Debug.LogWarning("[Textures] Nettoyage interrompu.");
                    break;
                }

                string folder = $"Assets/Resources/Data/Maps/{regions[i]}/TerrainData/MicroSplatData";
                string terrainDataPath = $"Assets/Resources/Data/Maps/{regions[i]}/TerrainData/{regions[i]}.asset";

                var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath);
                if (terrainData == null || !AssetDatabase.IsValidFolder(folder))
                {
                    skipped++;
                    continue;
                }

                // Les couches que le terrain utilise reellement. Tout le reste
                // du dossier est orphelin.
                var used = new HashSet<string>();
                foreach (TerrainLayer layer in terrainData.terrainLayers)
                {
                    if (layer != null)
                    {
                        used.Add(AssetDatabase.GetAssetPath(layer));
                    }
                }

                // Un terrain sans aucune couche est anormal : on s'abstient
                // plutot que de vider le dossier sur une lecture douteuse.
                if (used.Count == 0)
                {
                    Debug.LogWarning($"[Textures] {regions[i]} : aucune couche referencee, region ignoree par prudence.");
                    skipped++;
                    continue;
                }

                foreach (string guid in AssetDatabase.FindAssets("t:TerrainLayer", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (used.Contains(path))
                    {
                        kept++;
                    }
                    else if (AssetDatabase.DeleteAsset(path))
                    {
                        deleted++;
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[Textures] Nettoyage termine : {deleted} couche(s) orpheline(s) supprimee(s), "
                  + $"{kept} conservee(s), {skipped} region(s) ignoree(s).");
    }

    /// Recense les textures L2 du monde et le nombre de regions ou chacune
    /// apparait.
    ///
    /// La source est le nom des fichiers de couche produits a l'import,
    /// "{region}_layer_{index}_{nomL2}.asset" : c'est le seul endroit ou le nom
    /// L2 d'ORIGINE survit. Les textures posees dans la config MicroSplat sont
    /// deja substituees et ne conviendraient pas.
    private static Dictionary<string, int> CountTextureUsage()
    {
        var usage = new Dictionary<string, int>();

        foreach (string guid in AssetDatabase.FindAssets(
                     "t:TerrainLayer", new[] { "Assets/Resources/Data/Maps" }))
        {
            string file = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));

            int marker = file.IndexOf("_layer_", System.StringComparison.Ordinal);
            if (marker < 0)
            {
                continue;
            }

            // "22_21_layer_10_GI_S3" -> on saute l'index pour isoler "GI_S3".
            string rest = file.Substring(marker + "_layer_".Length);
            int sep = rest.IndexOf('_');
            if (sep <= 0 || !int.TryParse(rest.Substring(0, sep), out _))
            {
                continue;
            }

            string l2Texture = rest.Substring(sep + 1);
            usage.TryGetValue(l2Texture, out int count);
            usage[l2Texture] = count + 1;
        }

        return usage;
    }

    [MenuItem("Shnok/[Textures] Creer l'asset de reglages (pre-rempli)")]
    public static void CreateTextureSettingsAsset()
    {
        if (File.Exists(L2TerrainTextureSettings.AssetPath)
            && !EditorUtility.DisplayDialog("L'asset existe deja",
                    "Le recreer ECRASERA tous tes reglages actuels.\n\n"
                    + L2TerrainTextureSettings.AssetPath,
                    "Ecraser", "Annuler"))
        {
            return;
        }

        var settings = ScriptableObject.CreateInstance<L2TerrainTextureSettings>();
        var matcher = L2TerrainGeneratorTextureMatcher.Instance;

        foreach (var kv in matcher.packDefaultScales)
        {
            settings.packDefaults.Add(new L2TerrainTextureSettings.PackDefault
            {
                pbrPack = kv.Key,
                scale = kv.Value
            });
        }

        // TOUTES les textures reellement utilisees par le monde, pas seulement
        // les correspondances ecrites en dur.
        //
        // POURQUOI
        // Les 20 entrees codees en dur ne couvrent que 18% des couches ; le
        // reste est resolu par des REGLES automatiques, invisibles et non
        // editables. Pre-remplir l'asset avec le resultat de ces regles rend
        // chaque texture du monde visible et modifiable dans l'Inspector :
        // c'est la seule facon de reprendre la main texture par texture.
        //
        // Les entrees sont triees par nombre de regions concernees : les plus
        // structurantes apparaissent en tete de liste.
        var usage = CountTextureUsage();

        foreach (var kv in usage.OrderByDescending(u => u.Value).ThenBy(u => u.Key))
        {
            string l2Texture = kv.Key;

            // On n'ecrit que ce qui se resout : une texture sans pack (neige
            // sans equivalent, texture unique a une region) resterait une ligne
            // vide et trompeuse dans l'Inspector.
            if (!matcher.TryGetTextureMatch(null, l2Texture, out string pbrPack)
                || string.IsNullOrEmpty(pbrPack))
            {
                continue;
            }

            // Echelle laissee a 0 si la texture n'en a pas en propre : elle
            // heritera de celle de son pack.
            float scale = matcher.scaleMatches.TryGetValue(l2Texture, out float s) ? s : 0f;

            settings.substitutions.Add(new L2TerrainTextureSettings.Substitution
            {
                l2Texture = l2Texture,
                pbrPack = pbrPack,
                scale = scale
            });
        }

        foreach (var region in matcher.regionTextureMatches)
        {
            foreach (var tex in region.Value)
            {
                float scale = 0f;
                if (matcher.regionScaleMatches.TryGetValue(region.Key, out var scales))
                {
                    scales.TryGetValue(tex.Key, out scale);
                }

                settings.regionOverrides.Add(new L2TerrainTextureSettings.RegionOverride
                {
                    region = region.Key,
                    l2Texture = tex.Key,
                    pbrPack = tex.Value,
                    scale = scale
                });
            }
        }

        if (File.Exists(L2TerrainTextureSettings.AssetPath))
        {
            AssetDatabase.DeleteAsset(L2TerrainTextureSettings.AssetPath);
        }

        AssetDatabase.CreateAsset(settings, L2TerrainTextureSettings.AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        L2TerrainGeneratorTextureMatcher.Reload();
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);

        Debug.Log($"[Textures] Asset cree : {L2TerrainTextureSettings.AssetPath} — "
                  + $"{settings.substitutions.Count} substitution(s), "
                  + $"{settings.packDefaults.Count} pack(s). "
                  + "Tout se regle desormais dans l'Inspector.");
    }

    /// La region de la scene ouverte, pour iterer vite : regarder le rendu,
    /// ajuster le matcher, re-appliquer, regarder de nouveau.
    [MenuItem("Shnok/[Textures] Re-appliquer les substitutions (scene ouverte)")]
    public static void ReapplySubstitutionsCurrentScene()
    {
        Scene active = EditorSceneManager.GetActiveScene();
        string mapName = Path.GetFileNameWithoutExtension(active.path);

        if (string.IsNullOrEmpty(mapName)
            || !System.Text.RegularExpressions.Regex.IsMatch(mapName, @"^\d+_\d+$"))
        {
            Debug.LogError("[Textures] Ouvrez d'abord la scene d'une region (ex. 17_23.unity).");
            return;
        }

        // La scene est rouverte par ReapplySubstitutionsFor : on previent, car
        // toute modification non sauvegardee serait perdue.
        if (active.isDirty && !EditorUtility.DisplayDialog("Modifications non sauvegardees",
                $"La scene {mapName} a des modifications non sauvegardees.\n\n"
                + "Elle va etre rechargee : ces modifications seront PERDUES.",
                "Continuer quand meme", "Annuler"))
        {
            return;
        }

        ReapplySubstitutionsFor(mapName);
    }

    /// Aligne la region ouverte sur les regions de reference : active les
    /// reglages par texture et sement des valeurs saines, ce qui corrige au
    /// passage le terrain blanc et miroitant.
    ///
    /// Operation non destructrice : MicroSplatData/ n'est pas regenere, la
    /// peinture et le raccord sont intacts.
    [MenuItem("Shnok/[Textures] Aligner MicroSplat sur les references (scene ouverte)")]
    public static void MattifyCurrentScene()
    {
        Scene active = EditorSceneManager.GetActiveScene();
        string mapName = Path.GetFileNameWithoutExtension(active.path);

        if (string.IsNullOrEmpty(mapName)
            || !System.Text.RegularExpressions.Regex.IsMatch(mapName, @"^\d+_\d+$"))
        {
            Debug.LogError("[Textures] Ouvrez d'abord la scene d'une region (ex. 18_19.unity).");
            return;
        }

        if (L2MicroSplatReference.AlignFor(mapName))
        {
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Textures] {mapName} : aligne sur les references et sauvegarde.");
        }
    }

    /// Le meme alignement sur toutes les regions ayant une scene.
    [MenuItem("Shnok/[Textures] Aligner MicroSplat sur les references (TOUTES les regions)")]
    public static void MattifyAll()
    {
        string[] regions = EnumerateRegionScenes();
        if (regions.Length == 0)
        {
            Debug.LogWarning("[Textures] Aucune region trouvee.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Aligner MicroSplat sur les references",
                $"{regions.Length} region(s) vont etre traitees.\n\n"
                + $"Les regions de reference ({string.Join(", ", L2MicroSplatReference.ReferenceRegions)})\n"
                + "servent de modele et ne sont PAS modifiees.\n\n"
                + "Operation NON destructrice : MicroSplatData/ n'est pas regenere,\n"
                + "la peinture et le raccord sont conserves.\n\n"
                + "Continuer ?",
                "Lancer", "Annuler"))
        {
            return;
        }

        int done = 0, failed = 0;
        try
        {
            for (int i = 0; i < regions.Length; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Alignement MicroSplat",
                        $"{regions[i]} ({i + 1}/{regions.Length})", (float)i / regions.Length))
                {
                    Debug.LogWarning($"[Textures] Interrompu apres {done} region(s).");
                    break;
                }

                Scene scene = EditorSceneManager.OpenScene($"{ScenesFolder}/{regions[i]}.unity", OpenSceneMode.Single);
                if (L2MicroSplatReference.AlignFor(regions[i]))
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    done++;
                }
                else
                {
                    failed++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[Textures] Correction de brillance terminee : {done} region(s) traitee(s), {failed} en echec.");
    }

    /// Les regions possedant une scene, triees. Partage par les operations de
    /// masse pour qu'elles couvrent toutes exactement le meme ensemble.
    private static string[] EnumerateRegionScenes()
    {
        if (!Directory.Exists(ScenesFolder))
        {
            Debug.LogError($"[Textures] Dossier de scenes introuvable : {ScenesFolder}");
            return new string[0];
        }

        return Directory.GetFiles(ScenesFolder, "*.unity")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => System.Text.RegularExpressions.Regex.IsMatch(n, @"^\d+_\d+$"))
            .OrderBy(n => n)
            .ToArray();
    }

    /// Toutes les regions ayant une scene, d'un coup.
    [MenuItem("Shnok/[Textures] Re-appliquer les substitutions (TOUTES les regions)")]
    public static void ReapplySubstitutionsAll()
    {
        // Les regions de reference sont ECARTEES du traitement de masse.
        //
        // Ce sont elles qui servent de modele a l'alignement : leur rendu a ete
        // valide a la main et doit le rester. Les regenerer les ferait repasser
        // par la substitution automatique, qui ne reproduirait pas fidelement ce
        // reglage manuel - et on perdrait la reference elle-meme.
        //
        // Elles restent traitables une par une via l'entree "scene ouverte",
        // pour le jour ou on voudra deliberement les refaire.
        string[] regions = EnumerateRegionScenes()
            .Where(r => !L2MicroSplatReference.ReferenceRegions.Contains(r))
            .ToArray();

        if (regions.Length == 0)
        {
            Debug.LogWarning("[Textures] Aucune region trouvee.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Re-appliquer les substitutions",
                $"{regions.Length} region(s) vont etre traitees.\n\n"
                + $"Les regions de reference ({string.Join(", ", L2MicroSplatReference.ReferenceRegions)})\n"
                + "sont ECARTEES et ne seront pas modifiees.\n\n"
                + "Le raccord de terrain, la peinture manuelle et les objets ajoutes\n"
                + "a la main sont PRESERVES (les .terrainlayer aussi).\n\n"
                + "L'operation peut durer longtemps. Continuer ?",
                "Lancer", "Annuler"))
        {
            return;
        }

        // Pas a pas, pour que l'editeur reprenne la main entre chaque region.
        // Voir StartSteppedBatch : sans ca, MicroSplat n'enregistre jamais les
        // TerrainLayer qu'il vient de creer.
        StartSteppedBatch(regions);
    }

    /// Point d'entree -batchmode. Lit -mapNames (separees par des virgules),
    /// ou traite toutes les regions si l'argument est absent.
    public static void BatchReapplySubstitutions()
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

        if (mapNames == null || mapNames.Length == 0)
        {
            // Meme protection que l'entree de menu : sans -mapNames explicite,
            // les regions de reference sont ecartees. Les nommer explicitement
            // reste possible, c'est alors un choix delibere.
            mapNames = EnumerateRegionScenes()
                .Where(r => !L2MicroSplatReference.ReferenceRegions.Contains(r))
                .ToArray();
        }

        bool ok = ReapplySubstitutionsBatch(mapNames);
        EditorApplication.Exit(ok ? 0 : 1);
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

        // Donnees carrement absentes du client, signalees AVANT les ~4 min
        // d'import plutot qu'a mi-parcours. Les deux cas ci-dessous ont chacun
        // fait echouer des regions entieres avant d'etre rendus tolerables
        // (19_11 sans heightmap, 18_17/19_16/19_17 avec une couche morte) ;
        // ils restent signales, car sur une region qui n'est PAS une tuile
        // d'ocean ils trahiraient une extraction incomplete.
        if (terrainInfo != null)
        {
            if (string.IsNullOrEmpty(terrainInfo.terrainMapPath)
                || !File.Exists(terrainInfo.terrainMapPath))
            {
                Debug.LogWarning($"[Import] {mapName} : AUCUNE heightmap dans le client "
                                 + "-> terrain plat. Attendu sur une tuile d'ocean seulement.");
            }

            if (terrainInfo.uvLayers != null && terrainInfo.uvLayers.Count == 0)
            {
                Debug.LogWarning($"[Import] {mapName} : AUCUNE couche de terrain exploitable "
                                 + "-> terrain sans texture.");
            }
        }

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
    /// Supprime les donnees de terrain d'un import precedent avant d'en
    /// regenerer de nouvelles.
    ///
    /// POURQUOI C'EST NECESSAIRE
    /// MicroSplat cree ses assets via AssetDatabase.GenerateUniqueAssetPath()
    /// (verifie dans MicroSplatShaderGUI_Compiler.cs et
    /// TextureArrayConfigEditor.cs du plugin). Cette API n'ECRASE PAS un
    /// fichier existant : elle en cree un a cote suffixe " 1", " 2"...
    /// Reimporter une region par-dessus elle-meme produisait donc un jeu
    /// complet de doublons - "MicroSplat 1.mat", "MicroSplatConfig 1.asset",
    /// et surtout "MicroSplatConfig 1_*_tarray.asset" a 27 Mo piece, orphelins
    /// mais bien presents sur le disque. Constate sur l'ancien 17_23, qui
    /// portait 16 assets MicroSplat au lieu de 9.
    ///
    /// On ne supprime QUE TerrainData/ : entierement regeneree par les etapes
    /// 04 a 06. Les .prefab de la region sont volontairement preserves, car
    /// certains portent du travail manuel que le pipeline ne sait pas
    /// reproduire (MusicArea, BoxVolumes, Marker des regions de reference).
    private static void CleanPreviousTerrainData(string mapName)
    {
        string terrainDataFolder = $"Assets/Resources/Data/Maps/{mapName}/TerrainData";
        if (!AssetDatabase.IsValidFolder(terrainDataFolder))
        {
            return;
        }

        if (AssetDatabase.DeleteAsset(terrainDataFolder))
        {
            AssetDatabase.Refresh();
            Debug.Log($"[Import] {mapName} : TerrainData precedent supprime "
                      + "(evite les doublons MicroSplat ' 1').");
        }
        else
        {
            Debug.LogWarning($"[Import] {mapName} : echec de suppression de {terrainDataFolder}. "
                             + "Des doublons MicroSplat ' 1' peuvent apparaitre.");
        }
    }

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
