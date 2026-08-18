#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Execution de la mutualisation MicroSplat.
///
/// L'analyse (L2MicroSplatMutualizer) a etabli la table de correspondance ;
/// ce fichier l'applique. Il est separe parce qu'il ECRIT dans les splatmaps,
/// seule donnee non regenerable du projet - le travail des level designers de
/// 2006. Sauvegarde faite le 2026-08-14 dans _backup_terraindata_20260814/,
/// 4,5 Go, 154 regions.
///
/// LE PRINCIPE, EN TROIS ETAPES
///
///   1. Creer 32 TerrainLayer partages, un par pack PBR, dans l'ordre optimise.
///      Purement additif : aucune region n'est touchee.
///
///   2. Batir le materiau maitre. On assigne les 32 couches a UNE region, on
///      laisse MicroSplat generer sa config, et ce materiau devient le
///      templateMaterial de toutes les autres. C'est le mecanisme prevu par
///      MicroSplat (MicroSplatTerrain.cs:209 instancie depuis le template) :
///      meme shader, memes arrays, un seul pipeline state.
///
///   3. Reindexer chaque region. C'EST L'ETAPE IRREVERSIBLE.
///
/// POURQUOI LA REINDEXATION EST UNE SOMME, PAS UNE PERMUTATION
/// Plusieurs couches L2 se substituent vers le meme pack : dans 18_23, GUC02
/// et deux GUC07 aboutissent tous a Rock_Cliff_xccibbi. Leurs poids doivent
/// donc etre ADDITIONNES sur la tranche cible. C'est sans perte visuelle -
/// ces pixels rendaient deja la meme texture - mais deplacer au lieu
/// d'additionner effacerait les deux tiers de la matiere.
///
/// TALKING ISLAND RESTE A PART
/// 16_24, 16_25, 17_24, 17_25 gardent leur config, leurs 20 fonctionnalites et
/// leur triplanar. Elles servent de reference visuelle et ne doivent pas
/// changer.
public static class L2MicroSplatMutualizerApply
{
    private const string MapsFolder = "Assets/Resources/Data/Maps";
    private const string SharedFolder = "Assets/Resources/Data/Terrain/SharedMicroSplat";
    private const string PacksFolder = "Assets/Resources/Data/External/Textures";

    /// Resolution des tableaux partages : 1024, comme Talking Island.
    ///
    /// ERREUR CORRIGEE LE 2026-08-17 - le 512 etait un faux calcul.
    /// Le raisonnement d'origine ("gain invisible a la distance ou le basemap
    /// prend le relais") ne tenait pas : les regions mutualisees ont
    /// basemapDistance = 99999, donc le basemap ne prend JAMAIS le relais et
    /// c'est le shader detaille qui dessine tout le terrain, de pres comme de
    /// loin. Diviser la resolution par deux dans chaque dimension enlevait
    /// donc les trois quarts des pixels sur l'integralite du monde.
    ///
    /// L'economie ne justifiait rien : ces tableaux sont PARTAGES, charges une
    /// seule fois pour les 148 regions. Passer de 512 a 1024 les fait monter
    /// de ~39 Mo a ~156 Mo au total - a comparer aux 2,4 Go de tableaux par
    /// region que la mutualisation a justement supprimes. Le 512 economisait
    /// 117 Mo au prix de la definition du sol du jeu entier.
    ///
    /// Talking Island, restee en 1024, est la seule zone que l'utilisateur
    /// jugeait nette - c'est lui qui a identifie la piste.
    private const JBooth.MicroSplat.TextureArrayConfig.TextureSize ArrayResolution =
        JBooth.MicroSplat.TextureArrayConfig.TextureSize.k1024;

    private static readonly string[] ReferenceRegions = { "16_24", "16_25", "17_24", "17_25" };
    private static readonly string[] TestRegions = { "17_22", "17_23", "18_22", "18_23" };

    /// Nombre de tranches que chaque region DOIT montrer, mesure sur les poids
    /// juste avant leur ecriture. Sert a detecter un effondrement survenu
    /// APRES coup - le seul controle qui aurait attrape les 36 regions
    /// detruites le 2026-08-17.
    private static readonly Dictionary<string, int> _expectedVisible = new Dictionary<string, int>();

    // ================================================================
    //  ETAPE 1 - les 32 couches partagees
    // ================================================================

    [MenuItem("L2/Terrain/Mutualisation/3. Creer les couches partagees", false, 182)]
    public static void CreateSharedLayers()
    {
        List<string> order = ResolveSharedOrder();
        if (order == null)
        {
            return;
        }

        Directory.CreateDirectory(SharedFolder);

        int created = 0;
        for (int i = 0; i < order.Count; i++)
        {
            string path = $"{SharedFolder}/shared_{i:D2}_{order[i]}.terrainlayer";

            if (AssetDatabase.LoadAssetAtPath<TerrainLayer>(path) != null)
            {
                continue;
            }

            Texture2D diffuse = FindPackTexture(order[i], "BaseColor");
            if (diffuse == null)
            {
                Debug.LogError($"[Mutualisation] Pack '{order[i]}' : BaseColor introuvable sous {PacksFolder}. "
                               + "Couche non creee - l'ordre serait decale.");
                return;
            }

            var layer = new TerrainLayer
            {
                diffuseTexture = diffuse,
                normalMapTexture = FindPackTexture(order[i], "Normal"),
                tileSize = new Vector2(RegionTileSize, RegionTileSize)
            };

            AssetDatabase.CreateAsset(layer, path);
            created++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Mutualisation] {created} couche(s) creee(s) sur {order.Count} dans {SharedFolder}.");
    }

    /// Cote d'une region en unites monde. Doit rester aligne sur
    /// Geodata._mapSize : c'est la meme grille.
    private const float RegionWorldSize = 624.1524f;

    /// Taille de tuile des couches partagees, en unites monde.
    ///
    /// L'echelle reelle est portee par le propdata de chaque region (_UVScale
    /// par texture) ; cette valeur ne sert que d'ancrage pour l'apercu editeur.
    private const float RegionTileSize = RegionWorldSize / 256f;

    // ================================================================
    //  ETAPE 2 - la config partagee
    // ================================================================

    /// Batit la TextureArrayConfig unique : 32 packs dans l'ordre partage.
    ///
    /// C'est la moitie deterministe du materiau maitre - celle qui ne depend
    /// que de nos donnees. La seconde moitie (generer le materiau lui-meme)
    /// passe par l'interface MicroSplat : elle appelle du code de generation de
    /// shader que je ne peux pas piloter de facon fiable sans le voir tourner.
    ///
    /// Les chemins de textures suivent la meme convention que l'etape 05 du
    /// pipeline : {pack}_BaseColor.jpg, _AO, _Bump, _Normal, _Gloss,
    /// _Roughness, _Specular.
    [MenuItem("L2/Terrain/Mutualisation/3b. Batir la config partagee", false, 183)]
    public static void BuildSharedConfig()
    {
        List<string> order = ResolveSharedOrder();
        if (order == null)
        {
            return;
        }

        Directory.CreateDirectory(SharedFolder);

        string configPath = $"{SharedFolder}/MicroSplatConfig.asset";
        var cfg = AssetDatabase.LoadAssetAtPath<JBooth.MicroSplat.TextureArrayConfig>(configPath);

        if (cfg == null)
        {
            cfg = JBooth.MicroSplat.TextureArrayConfigEditor.CreateConfig(SharedFolder);
            if (cfg == null)
            {
                Debug.LogError("[Mutualisation] Creation de la config partagee impossible.");
                return;
            }
        }

        cfg.sourceTextures.Clear();

        int missing = 0;

        foreach (string pack in order)
        {
            string path = TextureUtils.GetSplatTexturePath(pack);

            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "_BaseColor.jpg");
            if (diffuse == null)
            {
                Debug.LogError($"[Mutualisation] Pack '{pack}' : BaseColor introuvable. "
                               + "Abandon - un trou decalerait tout l'ordre.");
                missing++;
                continue;
            }

            var entry = new JBooth.MicroSplat.TextureArrayConfig.TextureEntry
            {
                diffuse = diffuse,
                ao = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "_AO.jpg"),
                height = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "_Bump.jpg"),
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "_Normal.jpg"),
                specular = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "_Specular.jpg")
            };

            // BRILLANCE : le gloss d'abord, la rugosite en repli et INVERSEE.
            // Poser _Roughness.jpg comme brillance etait la cause du terrain
            // miroitant corrige le 2026-08-11. Voir L2TerrainGeneratorTool.
            Texture2D gloss = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "_Gloss.jpg");
            if (gloss != null)
            {
                entry.smoothness = gloss;
                entry.isRoughness = false;
            }
            else
            {
                Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "_Roughness.jpg");
                if (roughness != null)
                {
                    entry.smoothness = roughness;
                    entry.isRoughness = true;
                }
            }

            cfg.sourceTextures.Add(entry);
        }

        if (missing > 0)
        {
            Debug.LogError($"[Mutualisation] {missing} pack(s) sans texture : config NON compilee.");
            return;
        }

        // RESOLUTION DES ARRAYS.
        //
        // Sans ce reglage la config part sur 1024 non compresse : mesure du
        // 2026-08-14, 150 Mo pour 28 textures, contre 15 Mo pour les 9 textures
        // d'une region en 512. Les reglages ne sont pas sur cfg mais dans la
        // classe imbriquee TextureArrayGroup, via defaultTextureSettings -
        // les adresser sur cfg ne compile pas (CS1061).
        var settings = cfg.defaultTextureSettings;

        settings.diffuseSettings.textureSize = ArrayResolution;
        settings.normalSettings.textureSize = ArrayResolution;
        settings.smoothSettings.textureSize = ArrayResolution;
        settings.specularSettings.textureSize = ArrayResolution;
        settings.antiTileSettings.textureSize = ArrayResolution;
        settings.emissiveSettings.textureSize = ArrayResolution;

        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();

        JBooth.MicroSplat.TextureArrayConfigEditor.CompileConfig(cfg);

        Debug.Log($"[Mutualisation] Config partagee : {cfg.sourceTextures.Count} textures compilees "
                  + $"dans {SharedFolder}. Assignez-la maintenant a un terrain via l'inspecteur "
                  + "MicroSplat pour produire le materiau maitre.");
    }

    // ================================================================
    //  ETAPE 2b - le terrain jetable qui produit le materiau maitre
    // ================================================================

    /// Cree un terrain temporaire portant les 28 couches partagees, pour que
    /// MicroSplat genere lui-meme le shader et le materiau.
    ///
    /// POURQUOI PASSER PAR UN TERRAIN
    /// MicroSplat ne cree de materiau qu'au cours d'une conversion
    /// (MicroSplatTerrainEditor_TerrainDesc.cs:157). L'inspecteur de config
    /// n'offre aucun bouton pour cela, et le composant MicroSplatTerrain ne
    /// reference pas la config : il ne connait qu'un Template Material, qui EST
    /// le mecanisme de partage.
    ///
    /// POURQUOI UN TERRAIN JETABLE PLUTOT QU'UNE REGION
    /// Assigner 28 couches a une region redimensionnerait ses splatmaps et
    /// detruirait sa peinture avant meme la reindexation. Le terrain temporaire
    /// est minuscule - 33x33 - et n'existe que le temps de la generation.
    [MenuItem("L2/Terrain/Mutualisation/3c. Creer le terrain maitre (temporaire)", false, 184)]
    public static void CreateMasterTerrain()
    {
        List<string> order = ResolveSharedOrder();
        if (order == null)
        {
            return;
        }

        TerrainLayer[] layers = LoadSharedLayers(order);
        if (layers == null)
        {
            return;
        }

        string dataPath = $"{SharedFolder}/MasterTerrain.asset";
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);

        if (data == null)
        {
            // LA TAILLE DOIT ETRE CELLE D'UNE VRAIE REGION.
            //
            // ConvertTerrains calcule des proprietes du materiau A PARTIR DE LA
            // TAILLE du terrain sur lequel il travaille
            // (MicroSplatTerrainEditor_Convert.cs:179) :
            //
            //     _UVScale          = size.x / tileSize de la couche
            //     _TriplanarUVScale = 10 / size.x
            //
            // Avec un terrain temporaire de 64 unites, ces valeurs sont
            // calibrees pour 64 unites puis appliquees a des regions de 624 :
            // les textures se repetent alors deux fois moins souvent que prevu.
            //
            // Constate le 2026-08-17 : le materiau maitre portait
            // _TriplanarUVScale = 0,15625, soit exactement 10/64 - la signature
            // du terrain temporaire. Et _UVScale = 128 au lieu de 256.
            //
            // La resolution des splatmaps, elle, n'entre dans aucun calcul de
            // materiau : on la garde a 32 pour que l'asset reste minuscule.
            data = new TerrainData
            {
                heightmapResolution = 33,
                size = new Vector3(RegionWorldSize, 10f, RegionWorldSize)
            };

            AssetDatabase.CreateAsset(data, dataPath);
        }

        // L'ordre compte : la resolution des splatmaps doit etre posee avant
        // les couches, sinon Unity realloue derriere nous.
        data.alphamapResolution = 32;
        data.terrainLayers = layers;

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        GameObject existing = GameObject.Find("__MicroSplatMaster");
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }

        GameObject go = UnityEngine.Terrain.CreateTerrainGameObject(data);
        go.name = "__MicroSplatMaster";

        UnityEngine.Terrain terrain = go.GetComponent<UnityEngine.Terrain>();
        if (terrain == null)
        {
            Debug.LogError("[Mutualisation] Terrain temporaire sans composant Terrain.");
            return;
        }

        // L'etape 05 du pipeline ne convient pas ici : elle demande un .t3d et
        // deduit le nom de region du fichier choisi. On appelle donc MicroSplat
        // directement, avec le meme filet que L2TerrainGeneratorTool - la
        // conversion vide parfois les couches puis echoue en les relisant.
        if (go.GetComponent<JBooth.MicroSplat.MicroSplatTerrain>() == null)
        {
            go.AddComponent<JBooth.MicroSplat.MicroSplatTerrain>();
        }

        TerrainLayer[] snapshot = terrain.terrainData.terrainLayers;

        try
        {
            JBooth.MicroSplat.MicroSplatTerrainEditor.ConvertTerrains(
                new UnityEngine.Terrain[] { terrain }, snapshot);
        }
        catch (Exception first)
        {
            terrain.terrainData.terrainLayers = snapshot;
            EditorUtility.SetDirty(terrain.terrainData);

            try
            {
                JBooth.MicroSplat.MicroSplatTerrainEditor.ConvertTerrains(
                    new UnityEngine.Terrain[] { terrain }, snapshot);
                Debug.LogWarning("[Mutualisation] Conversion reussie au second essai.");
            }
            catch (Exception second)
            {
                terrain.terrainData.terrainLayers = snapshot;
                Debug.LogError($"[Mutualisation] Conversion echouee deux fois.\n"
                               + $"1er : {first.Message}\n2e : {second.Message}");
                return;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Material master = terrain.materialTemplate;
        string materialPath = master != null ? AssetDatabase.GetAssetPath(master) : "(introuvable)";

        Selection.activeGameObject = go;

        Debug.Log($"[Mutualisation] MATERIAU MAITRE : {materialPath}\n"
                  + $"Genere depuis un terrain de {layers.Length} couches. "
                  + "Supprimez maintenant l'objet __MicroSplatMaster de la scene : "
                  + "le materiau, le shader et la config restent comme assets.");
    }

    /// Chemin du materiau maitre, produit par l'etape 3c.
    public const string MasterMaterialPath =
        SharedFolder + "/MicroSplatData/MicroSplat.mat";

    private const string MasterConfigPath =
        SharedFolder + "/MicroSplatData/MicroSplatConfig.asset";

    /// Aligne la resolution des tableaux du maitre sur ArrayResolution.
    ///
    /// ConvertTerrains cree SA PROPRE config et ignore celle que l'etape 3b
    /// avait reglee : on corrige donc apres coup, puisque c'est la conversion
    /// qui decide ou vit la config.
    ///
    /// Cette etape a d'abord servi a DESCENDRE en 512 - une erreur, voir le
    /// commentaire d'ArrayResolution. Elle sert desormais a remonter en 1024.
    [MenuItem("L2/Terrain/Mutualisation/3d. Aligner la resolution de la config maitre", false, 185)]
    public static void FixMasterResolution()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<JBooth.MicroSplat.TextureArrayConfig>(MasterConfigPath);

        if (cfg == null)
        {
            Debug.LogError($"[Mutualisation] Config maitre introuvable ({MasterConfigPath}). "
                           + "Lancez d'abord l'etape 3c.");
            return;
        }

        var settings = cfg.defaultTextureSettings;

        settings.diffuseSettings.textureSize = ArrayResolution;
        settings.normalSettings.textureSize = ArrayResolution;
        settings.smoothSettings.textureSize = ArrayResolution;
        settings.specularSettings.textureSize = ArrayResolution;
        settings.antiTileSettings.textureSize = ArrayResolution;
        settings.emissiveSettings.textureSize = ArrayResolution;

        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();

        JBooth.MicroSplat.TextureArrayConfigEditor.CompileConfig(cfg);

        Debug.Log($"[Mutualisation] Config maitre recompilee en {(int)ArrayResolution} : "
                  + $"{cfg.sourceTextures.Count} textures.\n"
                  + "En 1024, chaque tableau doit peser ~78 Mo (contre ~19 Mo en 512).");
    }

    /// Distance de bascule vers la basemap, en unites monde.
    ///
    /// Au-dela, Unity cesse d'evaluer le shader de splat et affiche une
    /// basemap pre-cuite : une seule texture composite, sans grille de texels
    /// de carte de controle. C'est ce qui adoucit le lointain.
    ///
    /// 512 est la valeur de Talking Island, la seule zone que l'utilisateur
    /// juge correcte. Les regions mutualisees etaient a 99999, donc en shader
    /// complet jusqu'a l'horizon - d'ou des carres visibles a toute distance.
    private const float BaseMapDistance = 512f;

    /// Aligne la distance de basemap de toutes les regions sur Talking Island.
    ///
    /// Talking Island est exclue : elle porte deja la bonne valeur et sert de
    /// reference.
    [MenuItem("L2/Terrain/Mutualisation/13. Aligner la distance de basemap sur Talking Island", false, 197)]
    public static void SetBaseMapDistance()
    {
        string[] regions = EnumerateRegions()
            .Where(r => !ReferenceRegions.Contains(r))
            .ToArray();

        int done = 0, skipped = 0;

        for (int i = 0; i < regions.Length; i++)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Distance de basemap",
                    $"{regions[i]} ({i + 1}/{regions.Length})", (float)i / regions.Length))
            {
                break;
            }

            string prefabPath = $"{MapsFolder}/{regions[i]}/{regions[i]}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                skipped++;
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                var terrain = root.GetComponentInChildren<UnityEngine.Terrain>(true);
                if (terrain == null)
                {
                    skipped++;
                    continue;
                }

                if (Mathf.Approximately(terrain.basemapDistance, BaseMapDistance))
                {
                    skipped++;
                    continue;
                }

                terrain.basemapDistance = BaseMapDistance;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                done++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();

        Debug.Log($"[Mutualisation] Distance de basemap portee a {BaseMapDistance} sur {done} region(s), "
                  + $"{skipped} inchangee(s).\n"
                  + "ATTENTION : si une scene porte une surcharge d'instance sur ce champ, "
                  + "elle gagnera sur le prefab - c'est la scene que charge le streaming.");
    }

    /// Force la resynchronisation de toutes les regions dont la scene est
    /// OUVERTE avec le materiau maitre actuel.
    ///
    /// POURQUOI CET OUTIL EXISTE
    /// MicroSplatTerrain.Sync() ne recopie les proprietes du templateMaterial
    /// vers l'instance reellement affichee (matInstance) qu'a l'activation du
    /// composant, a la sauvegarde de la scene, ou a une recompilation de
    /// script (MicroSplatTerrain.cs:194-218). Editer le fichier .mat a la main
    /// ne declenche AUCUN de ces trois evenements : la scene ouverte continue
    /// d'afficher l'ancienne copie tant qu'aucun de ces trois n'est survenu.
    ///
    /// Constate le 2026-08-17 : correction de _Contrast et
    /// _HybridHeightBlendDistance sur le materiau maitre sans effet visible -
    /// tres probablement parce que la scene ouverte n'avait jamais ete
    /// rechargee depuis l'edition du fichier.
    [MenuItem("L2/Terrain/Mutualisation/12. Forcer la resynchronisation (scenes ouvertes)", false, 196)]
    public static void ForceSyncOpenScenes()
    {
        JBooth.MicroSplat.MicroSplatObject.SyncAll();
        Debug.Log("[Mutualisation] Resynchronisation forcee sur toutes les regions actuellement chargees. "
                 + "Ne touche que les scenes OUVERTES - sans effet sur le reste.");
    }

    /// Complete la config MAITRE avec hauteur, brillance, occlusion et
    /// speculaire, puis recompile les tableaux.
    ///
    /// LE DEFAUT CORRIGE (2026-08-17)
    /// CreateSharedLayers ne renseigne que la diffuse et la normale d'un
    /// TerrainLayer - ce sont les seuls champs qu'expose la classe. Or c'est a
    /// partir de ces couches que ConvertTerrains a bati la config du maitre :
    /// ses tableaux ne contenaient donc AUCUNE hauteur.
    ///
    /// Le materiau maitre a pourtant _HYBRIDHEIGHTBLEND actif. Il calculait un
    /// melange par hauteur sans donnee de hauteur : incapable de dessiner une
    /// frontiere organique entre deux textures, le shader retombait sur la
    /// grille brute des texels de la carte de controle. C'est l'aspect
    /// "peinture au pinceau carre" signale par l'utilisateur.
    ///
    /// Talking Island, jamais mutualisee, garde ses cartes de hauteur et ne
    /// montre pas le defaut - a resolution de controle pourtant identique
    /// (1024, 0,61 unite/texel). C'est ce qui a permis de l'isoler : la mesure
    /// de douceur des donnees (etape 11) donne les memes valeurs des deux
    /// cotes, donc le probleme ne pouvait etre que dans le RENDU.
    ///
    /// L'etape 3b ecrivait bien ces cartes, mais dans un autre fichier que
    /// celui qu'utilise le maitre - d'ou l'impression qu'elle etait redondante.
    /// Celle-ci vise la config du maitre et retrouve le pack de chaque entree
    /// par le nom de son TerrainLayer ("shared_NN_<pack>"), donc sans dependre
    /// d'un ordre recalcule.
    [MenuItem("L2/Terrain/Mutualisation/3f. Completer la config maitre (hauteur, AO, brillance)", false, 187)]
    public static void CompleteMasterConfig()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<JBooth.MicroSplat.TextureArrayConfig>(MasterConfigPath);

        if (cfg == null)
        {
            Debug.LogError($"[Mutualisation] Config maitre introuvable ({MasterConfigPath}).");
            return;
        }

        var pattern = new Regex(@"^shared_\d+_(.+)$");
        int filled = 0, missing = 0;
        var noPack = new List<string>();

        foreach (var entry in cfg.sourceTextures)
        {
            if (entry.terrainLayer == null)
            {
                missing++;
                continue;
            }

            string layerPath = AssetDatabase.GetAssetPath(entry.terrainLayer);
            Match m = pattern.Match(Path.GetFileNameWithoutExtension(layerPath));

            if (!m.Success)
            {
                missing++;
                noPack.Add(Path.GetFileNameWithoutExtension(layerPath));
                continue;
            }

            string pack = m.Groups[1].Value;

            entry.height = FindPackTexture(pack, "Bump");
            entry.ao = FindPackTexture(pack, "AO");
            entry.specular = FindPackTexture(pack, "Specular");

            // BRILLANCE : le gloss d'abord, la rugosite en repli et INVERSEE.
            // Poser _Roughness comme brillance etait la cause du terrain
            // miroitant corrige le 2026-08-11.
            Texture2D gloss = FindPackTexture(pack, "Gloss");
            if (gloss != null)
            {
                entry.smoothness = gloss;
                entry.isRoughness = false;
            }
            else
            {
                Texture2D roughness = FindPackTexture(pack, "Roughness");
                if (roughness != null)
                {
                    entry.smoothness = roughness;
                    entry.isRoughness = true;
                }
            }

            if (entry.height != null) { filled++; }
        }

        if (missing > 0)
        {
            Debug.LogWarning($"[Mutualisation] {missing} entree(s) sans pack identifiable"
                             + (noPack.Count > 0 ? " : " + string.Join(", ", noPack.Take(6)) : "."));
        }

        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();

        JBooth.MicroSplat.TextureArrayConfigEditor.CompileConfig(cfg);

        Debug.Log($"[Mutualisation] Config maitre completee : {filled}/{cfg.sourceTextures.Count} "
                  + "entree(s) avec carte de hauteur. Tableaux recompiles.\n"
                  + "Le tableau normSAO doit avoir grossi : il porte desormais la hauteur.");
    }

    /// Rapporte la resolution des splatmaps, actuelle contre sauvegarde.
    ///
    /// Symptome du 2026-08-16 : peindre produit des carres au lieu de suivre le
    /// pinceau. C'est la signature d'une resolution de carte de controle
    /// effondree - chaque texel couvre alors plusieurs unites de terrain.
    /// La reindexation ne devrait pas y toucher, d'ou cette mesure.
    [MenuItem("L2/Terrain/Mutualisation/6. Diagnostic : resolution des splatmaps", false, 190)]
    public static void ReportAlphamapResolution()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("[Mutualisation] Resolution des cartes de controle :");
        report.AppendLine();

        var counts = new Dictionary<int, int>();
        var resolutions = new Dictionary<int, int>();
        var outliers = new List<string>();
        var perTexel = new Dictionary<float, int>();
        var coarse = new List<string>();

        foreach (string region in EnumerateRegions())
        {
            string p = $"{MapsFolder}/{region}/TerrainData/{region}.asset";
            var d = AssetDatabase.LoadAssetAtPath<TerrainData>(p);
            if (d == null)
            {
                continue;
            }

            int n = d.terrainLayers.Length;
            counts[n] = counts.TryGetValue(n, out int c) ? c + 1 : 1;

            int res = d.alphamapResolution;
            resolutions[res] = resolutions.TryGetValue(res, out int rc) ? rc + 1 : 1;

            if (res < 512)
            {
                outliers.Add($"{region} (controle {res})");
            }

            // Arrondi au centieme pour regrouper les valeurs identiques.
            float upt = Mathf.Round(d.size.x / Mathf.Max(1, res) * 100f) / 100f;
            perTexel[upt] = perTexel.TryGetValue(upt, out int pc) ? pc + 1 : 1;

            // Deux anomalies opposees, toutes deux fautives :
            //   - au-dela de 1,5 u/texel le pinceau produit des carres ;
            //   - en dessous de 0,1 la taille du terrain est degeneree, ce qui
            //     trahit un terrain casse plutot qu'un simple reglage.
            if (upt > 1.5f || upt < 0.1f)
            {
                coarse.Add($"{region} : terrain {d.size.x:F2} x {d.size.z:F2} u, "
                           + $"controle {res}, {upt:F3} u/texel, "
                           + $"heightmap {d.heightmapResolution}");
            }
        }

        report.AppendLine("REPARTITION DU NOMBRE DE COUCHES SUR TOUTES LES REGIONS :");
        foreach (var kv in counts.OrderBy(k => k.Key))
        {
            report.AppendLine($"  {kv.Value,4} region(s) a {kv.Key,3} couche(s)"
                              + (kv.Key == 28 ? "   <- reindexee" : ""));
        }
        report.AppendLine();

        report.AppendLine("REPARTITION DE LA RESOLUTION DE CONTROLE SUR TOUTES LES REGIONS :");
        foreach (var kv in resolutions.OrderBy(k => k.Key))
        {
            report.AppendLine($"  {kv.Value,4} region(s) a une resolution de {kv.Key,5}");
        }
        if (outliers.Count > 0)
        {
            report.AppendLine($"  ATTENTION - {outliers.Count} region(s) sous 512 : "
                              + string.Join(", ", outliers.Take(20))
                              + (outliers.Count > 20 ? $", ... et {outliers.Count - 20} autre(s)" : ""));
        }
        report.AppendLine();

        // FINESSE REELLE DU PINCEAU.
        //
        // Ce qui compte n'est pas la resolution seule mais le rapport
        // taille du terrain / resolution : deux regions a 1024 texels peuvent
        // avoir des texels de tailles tres differentes si leurs terrains ne
        // font pas la meme largeur. Le fichier, lui, pese pareil - d'ou une
        // difference invisible a l'inspection du disque.
        //
        // Constat du 2026-08-17 : 17_23 se peint proprement, 22_22 produit des
        // carres, alors que les deux fichiers font 39 Mo et portent 28 couches.
        // La taille du terrain est la seule variable restante.
        report.AppendLine("FINESSE REELLE (taille du terrain / resolution) :");
        foreach (var kv in perTexel.OrderBy(k => k.Key))
        {
            report.AppendLine($"  {kv.Value,4} region(s) a {kv.Key,7:F2} unite(s)/texel"
                              + (kv.Key > 1.5f ? "   <- PINCEAU GROSSIER" : ""));
        }
        if (coarse.Count > 0)
        {
            report.AppendLine();
            report.AppendLine($"  REGIONS ANORMALES ({coarse.Count}) :");
            foreach (string c in coarse.Take(40)) { report.AppendLine($"    {c}"); }
            if (coarse.Count > 40) { report.AppendLine($"    ... et {coarse.Count - 40} autre(s)"); }
        }
        report.AppendLine();
        report.AppendLine("DETAIL DES REGIONS TEST ET DE REFERENCE :");

        foreach (string region in TestRegions.Concat(ReferenceRegions))
        {
            string path = $"{MapsFolder}/{region}/TerrainData/{region}.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);

            if (data == null)
            {
                report.AppendLine($"  {region,-8} TerrainData introuvable");
                continue;
            }

            float unitsPerTexel = data.size.x / Mathf.Max(1, data.alphamapResolution);

            report.AppendLine($"  {region,-8} controle {data.alphamapResolution,5}"
                              + $" | base {data.baseMapResolution,5}"
                              + $" | couches {data.terrainLayers.Length,3}"
                              + $" | {unitsPerTexel,6:F2} unites/texel");
        }

        report.AppendLine();
        report.AppendLine("Une valeur saine tourne autour de 0,6 unite/texel (1024 pour 624 unites).");
        report.AppendLine("Au-dela de 2, le pinceau produit des carres visibles.");

        Debug.Log(report.ToString());
    }

    /// Rapporte le poids moyen de chaque tranche sur la region ouverte.
    ///
    /// Symptome du 2026-08-16 : apres reindexation, une seule texture recouvre
    /// toute la carte alors que la somme des poids est conservee a 100 %.
    /// Conserver la somme ne garantit pas qu'elle soit repartie sur les bonnes
    /// tranches - cette mesure le dit, la vue de scene ne le dit pas.
    [MenuItem("L2/Terrain/Mutualisation/7. Diagnostic : poids par tranche (scene ouverte)", false, 191)]
    public static void ReportWeights()
    {
        UnityEngine.Terrain terrain = UnityEngine.Terrain.activeTerrain;

        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogError("[Mutualisation] Aucun terrain actif dans la scene ouverte.");
            return;
        }

        TerrainData data = terrain.terrainData;
        int w = data.alphamapWidth;
        int h = data.alphamapHeight;
        int layers = data.terrainLayers.Length;

        float[,,] a = data.GetAlphamaps(0, 0, w, h);

        var sums = new double[layers];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                for (int i = 0; i < layers; i++)
                {
                    sums[i] += a[y, x, i];
                }
            }
        }

        double total = sums.Sum();

        var report = new System.Text.StringBuilder();
        report.AppendLine($"[Mutualisation] {terrain.name} : {layers} couches, "
                          + $"controle {data.alphamapResolution}, "
                          + $"{data.size.x / Mathf.Max(1, data.alphamapResolution):F2} unites/texel");
        report.AppendLine();

        for (int i = 0; i < layers; i++)
        {
            double share = total > 0 ? sums[i] / total : 0;
            if (share < 0.0005)
            {
                continue;
            }

            TerrainLayer layer = data.terrainLayers[i];
            string name = layer != null && layer.diffuseTexture != null
                ? layer.diffuseTexture.name
                : "(sans texture)";

            report.AppendLine($"  tranche {i,2} : {share,7:P2}  {name}");
        }

        report.AppendLine();
        report.AppendLine("Les tranches sous 0,05 % sont omises.");

        Debug.Log(report.ToString());
    }

    /// Repartition des poids sur TOUTES les regions, lue directement dans les
    /// assets - sans ouvrir la moindre scene.
    ///
    /// POURQUOI CE DIAGNOSTIC EXISTE
    /// Constat du 2026-08-17 : toutes les regions sauf Talking Island et les
    /// regions test paraissent uniformement couvertes d'herbe. Deux causes
    /// possibles, opposees et impossibles a distinguer a l'oeil :
    ///
    ///   a) les POIDS sont fautifs - une seule tranche porte tout ;
    ///   b) les poids sont bons et c'est le RENDU qui ecrase (materiau,
    ///      propdata, echelle d'UV).
    ///
    /// L'etape 7 ne repond pas : elle exige une scene ouverte, ce qui est
    /// justement interdit ici. Celle-ci lit l'asset, donc la verite du disque.
    ///
    /// On sous-echantillonne un pixel sur huit : a 1024x1024x28 sur 150
    /// regions, tout lire serait inutilement long pour une mesure statistique.
    [MenuItem("L2/Terrain/Mutualisation/10. Diagnostic : repartition des poids (toutes regions)", false, 194)]
    public static void ReportWeightSpread()
    {
        const int Step = 8;
        const double Dominant = 0.90;

        var report = new System.Text.StringBuilder();
        report.AppendLine("[Mutualisation] Repartition des poids, lue dans les assets.");
        report.AppendLine();

        var dominated = new List<string>();
        var healthy = new List<string>();
        string[] regions = EnumerateRegions();

        for (int r = 0; r < regions.Length; r++)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Diagnostic des poids",
                    $"{regions[r]} ({r + 1}/{regions.Length})", (float)r / regions.Length))
            {
                break;
            }

            string dataPath = $"{MapsFolder}/{regions[r]}/TerrainData/{regions[r]}.asset";
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (data == null)
            {
                continue;
            }

            int layers = data.terrainLayers != null ? data.terrainLayers.Length : 0;
            if (layers == 0)
            {
                continue;
            }

            int w = data.alphamapWidth;
            int h = data.alphamapHeight;
            float[,,] a = data.GetAlphamaps(0, 0, w, h);

            var sums = new double[layers];
            for (int y = 0; y < h; y += Step)
            {
                for (int x = 0; x < w; x += Step)
                {
                    for (int i = 0; i < layers; i++)
                    {
                        sums[i] += a[y, x, i];
                    }
                }
            }

            double total = sums.Sum();
            if (total <= 0)
            {
                continue;
            }

            int top = 0;
            for (int i = 1; i < layers; i++)
            {
                if (sums[i] > sums[top]) { top = i; }
            }

            double topShare = sums[top] / total;
            int visible = sums.Count(s => s / total >= 0.01);

            string topName = data.terrainLayers[top] != null
                             && data.terrainLayers[top].diffuseTexture != null
                ? data.terrainLayers[top].diffuseTexture.name
                : "(sans texture)";

            string line = $"  {regions[r]} : {layers,2} couches, {visible,2} visible(s) >1%, "
                          + $"dominante {topShare,7:P2} tranche {top,2} {topName}";

            if (topShare >= Dominant) { dominated.Add(line); } else { healthy.Add(line); }
        }

        EditorUtility.ClearProgressBar();

        report.AppendLine($"REGIONS DOMINEES PAR UNE SEULE TRANCHE (>= {Dominant:P0}) : {dominated.Count}");
        foreach (string l in dominated.Take(40)) { report.AppendLine(l); }
        if (dominated.Count > 40) { report.AppendLine($"  ... et {dominated.Count - 40} autre(s)"); }

        report.AppendLine();
        report.AppendLine($"REGIONS AVEC DU RELIEF DE TEXTURES : {healthy.Count}");
        foreach (string l in healthy.Take(40)) { report.AppendLine(l); }
        if (healthy.Count > 40) { report.AppendLine($"  ... et {healthy.Count - 40} autre(s)"); }

        Debug.Log(report.ToString());
    }

    /// Mesure la DOUCEUR des transitions entre textures.
    ///
    /// POURQUOI
    /// Constat du 2026-08-17 : la peinture parait faite au pinceau carre. Le
    /// test du _UVScale a montre que les carres ne suivent PAS le carrelage des
    /// textures - ce sont donc les texels de la carte de controle. Or sa
    /// resolution est identique (1024, 0,61 unite/texel) sur Talking Island,
    /// que l'utilisateur juge correcte, et sur les regions mutualisees.
    ///
    /// Deux explications restent, et elles n'appellent pas le meme correctif :
    ///
    ///   a) les carres preexistaient et n'etaient pas visibles tant qu'une
    ///      seule texture couvrait tout ; il n'y a alors rien de casse, et
    ///      adoucir demanderait de lisser les cartes de controle ;
    ///   b) la reindexation les a durcis - additionner plusieurs couches sur
    ///      une meme tranche concentre le poids et peut transformer un degrade
    ///      en marche d'escalier.
    ///
    /// On compare donc la douceur de Talking Island (jamais reindexee, donc
    /// temoin) a celle des regions mutualisees. Un texel "franc" ne porte
    /// qu'une seule texture : plus il y en a, plus la transition est carree.
    [MenuItem("L2/Terrain/Mutualisation/11. Diagnostic : douceur des transitions", false, 195)]
    public static void ReportBlendSoftness()
    {
        const int Step = 4;

        string[] sample = ReferenceRegions
            .Concat(TestRegions)
            .Concat(new[] { "19_20", "22_14", "21_20", "20_20" })
            .ToArray();

        var report = new System.Text.StringBuilder();
        report.AppendLine("[Mutualisation] Douceur des transitions entre textures.");
        report.AppendLine();
        report.AppendLine("  region    couches   texels francs   textures/texel");

        foreach (string region in sample)
        {
            string path = $"{MapsFolder}/{region}/TerrainData/{region}.asset";
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null || data.terrainLayers == null || data.terrainLayers.Length == 0)
            {
                continue;
            }

            int layers = data.terrainLayers.Length;
            int w = data.alphamapWidth, h = data.alphamapHeight;
            float[,,] a = data.GetAlphamaps(0, 0, w, h);

            long texels = 0, pure = 0, contributions = 0;

            for (int y = 0; y < h; y += Step)
            {
                for (int x = 0; x < w; x += Step)
                {
                    float max = 0f;
                    int active = 0;

                    for (int i = 0; i < layers; i++)
                    {
                        float v = a[y, x, i];
                        if (v > max) { max = v; }
                        if (v > 0.01f) { active++; }
                    }

                    texels++;
                    contributions += active;
                    if (max >= 0.99f) { pure++; }
                }
            }

            bool witness = ReferenceRegions.Contains(region);

            report.AppendLine($"  {region,-8} {layers,6}   {(double)pure / texels,12:P1}   "
                              + $"{(double)contributions / texels,8:F2}"
                              + (witness ? "   <- temoin (non reindexee)" : ""));
        }

        report.AppendLine();
        report.AppendLine("Un texel franc ne porte qu'une texture : plus il y en a, plus la");
        report.AppendLine("transition est abrupte. Si le temoin affiche les memes valeurs que");
        report.AppendLine("les regions reindexees, les carres preexistaient a la mutualisation.");

        Debug.Log(report.ToString());
    }

    /// Mesure si les splatmaps sont definies sur une grille PLUS GROSSIERE que
    /// leur resolution declaree.
    ///
    /// POURQUOI
    /// Toutes les regions annoncent 1024 texels pour 624 unites, soit 0,61
    /// unite/texel. Pourtant les regions test se peignent proprement et les
    /// autres produisent des carres, alors que materiau, propdata, taille de
    /// terrain et nombre de couches sont rigoureusement identiques.
    ///
    /// Il reste une possibilite : que la DONNEE soit grossiere meme si le
    /// conteneur est fin. Une source basse resolution etiree au plus proche
    /// voisin remplit un texel sur N puis recopie - le fichier fait bien
    /// 1024, mais la peinture n'a que 1024/N de finesse reelle.
    ///
    /// On teste donc la constance par blocs : pour N = 2, 4, 8, 16, quelle
    /// proportion des texels vaut exactement celui du coin de son bloc NxN ?
    /// Une donnee vraiment fine tombe vite ; une donnee etiree d'un facteur 8
    /// restera a 100 % jusqu'a N = 8.
    [MenuItem("L2/Terrain/Mutualisation/14. Diagnostic : finesse reelle de la donnee", false, 198)]
    public static void ReportDataGranularity()
    {
        int[] blocks = { 2, 4, 8, 16 };

        string[] sample = TestRegions
            .Concat(new[] { "22_22", "22_23", "22_14", "25_11", "19_20" })
            .Concat(ReferenceRegions)
            .ToArray();

        var report = new System.Text.StringBuilder();
        report.AppendLine("[Mutualisation] Finesse reelle de la donnee de splatmap.");
        report.AppendLine();
        report.AppendLine("  Proportion de texels identiques au coin de leur bloc NxN.");
        report.AppendLine("  Une valeur proche de 100 % signifie que la donnee est CONSTANTE");
        report.AppendLine("  sur ce bloc, donc que sa finesse reelle est N fois moindre.");
        report.AppendLine();
        report.AppendLine("  region     N=2      N=4      N=8     N=16   role");

        foreach (string region in sample)
        {
            string path = $"{MapsFolder}/{region}/TerrainData/{region}.asset";
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null || data.terrainLayers == null || data.terrainLayers.Length == 0)
            {
                continue;
            }

            int layers = data.terrainLayers.Length;
            int w = data.alphamapWidth, h = data.alphamapHeight;
            float[,,] a = data.GetAlphamaps(0, 0, w, h);

            var line = new System.Text.StringBuilder();
            line.Append($"  {region,-8}");

            foreach (int n in blocks)
            {
                long same = 0, total = 0;

                for (int y = 0; y + n <= h; y += n)
                {
                    for (int x = 0; x + n <= w; x += n)
                    {
                        for (int dy = 0; dy < n; dy++)
                        {
                            for (int dx = 0; dx < n; dx++)
                            {
                                total++;
                                bool identical = true;

                                for (int i = 0; i < layers; i++)
                                {
                                    if (Mathf.Abs(a[y + dy, x + dx, i] - a[y, x, i]) > 0.002f)
                                    {
                                        identical = false;
                                        break;
                                    }
                                }

                                if (identical) { same++; }
                            }
                        }
                    }
                }

                line.Append($" {(double)same / total,7:P1}");
            }

            string role = TestRegions.Contains(region) ? "   <- TEST (bonne)"
                        : ReferenceRegions.Contains(region) ? "   <- Talking Island"
                        : "";

            report.AppendLine(line + role);
        }

        report.AppendLine();
        report.AppendLine("Si les regions TEST chutent vite et les autres restent hautes,");
        report.AppendLine("la donnee des autres est etiree : le probleme vient de l'import,");
        report.AppendLine("pas du materiau ni de la mutualisation.");

        Debug.Log(report.ToString());
    }

    /// Recense les regions dont la splatmap est DEGENEREE : une seule texture
    /// partout, alors que leurs couches d'origine visaient plusieurs packs.
    ///
    /// POURQUOI
    /// 22_22 porte huit couches L2 qui se substituent vers SIX tranches
    /// distinctes (08, 09, 10, 11, 15, 16). Sa splatmap devrait donc etre
    /// variee. Elle est pourtant constante a 100 % a toutes les echelles :
    /// une seule texture recouvre tout.
    ///
    /// Ce n'est pas un defaut de rendu mais de DONNEE, et il est invisible aux
    /// controles habituels : le poids total est conserve a 100 %, le nombre de
    /// couches est correct, la resolution aussi. Seule la comparaison entre ce
    /// que la region DEVRAIT montrer et ce qu'elle montre le revele.
    ///
    /// Une region legitimement uniforme (tuile d'ocean, une seule couche
    /// d'origine) n'est PAS signalee : on n'accuse que celles qui perdent de la
    /// variete en cours de route.
    [MenuItem("L2/Terrain/Mutualisation/15. Diagnostic : splatmaps degenerees", false, 199)]
    public static void ReportDegenerateSplatmaps()
    {
        const int Step = 4;

        Dictionary<string, string> packOf = BuildSubstitutionMap();
        List<string> order = ResolveSharedOrder();
        if (packOf == null || order == null)
        {
            return;
        }

        var indexOf = new Dictionary<string, int>();
        for (int i = 0; i < order.Count; i++) { indexOf[order[i]] = i; }

        var broken = new List<string>();
        var partial = new List<string>();
        var healthy = 0;
        var legitimatelyUniform = 0;

        string[] regions = EnumerateRegions();

        for (int r = 0; r < regions.Length; r++)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Splatmaps degenerees",
                    $"{regions[r]} ({r + 1}/{regions.Length})", (float)r / regions.Length))
            {
                break;
            }

            // Ce que la region DEVRAIT montrer, d'apres ses noms de couches.
            var expected = new HashSet<int>();
            foreach (string name in ReadL2Names(regions[r]))
            {
                if (packOf.TryGetValue(name, out string pack) && indexOf.TryGetValue(pack, out int idx))
                {
                    expected.Add(idx);
                }
            }

            if (expected.Count <= 1)
            {
                legitimatelyUniform++;
                continue;
            }

            string path = $"{MapsFolder}/{regions[r]}/TerrainData/{regions[r]}.asset";
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null || data.terrainLayers == null || data.terrainLayers.Length == 0)
            {
                continue;
            }

            int layers = data.terrainLayers.Length;
            int w = data.alphamapWidth, h = data.alphamapHeight;
            float[,,] a = data.GetAlphamaps(0, 0, w, h);

            // Ce qu'elle montre reellement.
            var sums = new double[layers];
            long texels = 0;

            for (int y = 0; y < h; y += Step)
            {
                for (int x = 0; x < w; x += Step)
                {
                    texels++;
                    for (int i = 0; i < layers; i++) { sums[i] += a[y, x, i]; }
                }
            }

            double total = sums.Sum();
            int actual = total > 0 ? sums.Count(s => s / total >= 0.01) : 0;

            // On signale TOUTE perte, pas seulement l'effondrement total.
            //
            // Le seuil d'origine (actual <= 1) laissait passer une region
            // tombee de huit tranches a trois : elle etait comptee saine alors
            // qu'elle avait perdu les cinq autres. Constate le 2026-08-17 sur
            // les regions a carres, toutes declarees saines.
            //
            // Un ecart n'est pas toujours fautif - une texture peut n'occuper
            // aucune surface reelle - d'ou la separation entre effondrement
            // (grave) et perte partielle (a examiner).
            if (actual <= 1)
            {
                broken.Add($"{regions[r]} : {expected.Count} attendue(s), {actual} affichee(s)  <- EFFONDREE");
            }
            else if (actual < expected.Count)
            {
                partial.Add($"{regions[r]} : {expected.Count} attendue(s), {actual} affichee(s)  "
                            + $"(-{expected.Count - actual})");
            }
            else
            {
                healthy++;
            }
        }

        EditorUtility.ClearProgressBar();

        var report = new System.Text.StringBuilder();
        report.AppendLine("[Mutualisation] Splatmaps degenerees (variete perdue).");
        report.AppendLine();
        report.AppendLine($"  {broken.Count} region(s) EFFONDREE(S)");
        report.AppendLine($"  {partial.Count} region(s) en PERTE PARTIELLE");
        report.AppendLine($"  {healthy} region(s) saine(s)");
        report.AppendLine($"  {legitimatelyUniform} region(s) uniformes a juste titre (une seule couche d'origine)");

        if (broken.Count > 0)
        {
            report.AppendLine();
            foreach (string b in broken.Take(60)) { report.AppendLine($"    {b}"); }
            if (broken.Count > 60) { report.AppendLine($"    ... et {broken.Count - 60} autre(s)"); }
        }

        if (partial.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("  PERTE PARTIELLE :");
            foreach (string p in partial.Take(60)) { report.AppendLine($"    {p}"); }
            if (partial.Count > 60) { report.AppendLine($"    ... et {partial.Count - 60} autre(s)"); }
        }

        Debug.Log(report.ToString());
    }

    /// Les regions degenerees l'etaient-elles DEJA avant la mutualisation ?
    ///
    /// POURQUOI CETTE QUESTION EST LA BONNE
    /// La garde anti-perte de poids de ReindexOne ne se declenche pas si le
    /// poids d'origine valait zero :
    ///
    ///     double loss = before > 0 ? (before - after) / before : 0;
    ///
    /// Une region dont la splatmap source etait deja vide, ou entierement
    /// couverte par une seule couche, passe donc le controle en annoncant
    /// "poids conserve a 100 %" - et ressort degeneree sans que rien ne
    /// signale quoi que ce soit.
    ///
    /// On ne peut pas trancher depuis l'etat actuel : il faut relire la
    /// sauvegarde PRE-mutualisation. AssetDatabase ne sait pas lire hors de
    /// Assets/, donc on recopie chaque asset le temps de la mesure, un par un,
    /// puis on efface.
    ///
    /// Verdict :
    ///   - deja degeneree avant  -> defaut d'IMPORT, anterieur a nos travaux ;
    ///   - saine avant           -> c'est la reindexation qui l'a detruite.
    [MenuItem("L2/Terrain/Mutualisation/16. Les degenerees l'etaient-elles avant ?", false, 200)]
    public static void CompareDegenerateWithBackup()
    {
        const string BackupRoot = @"D:\Jeux\PROJET_L2UNITY\_backup_terraindata_20260814";
        const string TempFolder = "Assets/__TempBackupCheck";

        if (!Directory.Exists(BackupRoot))
        {
            Debug.LogError($"[Mutualisation] Sauvegarde introuvable : {BackupRoot}");
            return;
        }

        Dictionary<string, string> packOf = BuildSubstitutionMap();
        List<string> order = ResolveSharedOrder();
        if (packOf == null || order == null)
        {
            return;
        }

        var indexOf = new Dictionary<string, int>();
        for (int i = 0; i < order.Count; i++) { indexOf[order[i]] = i; }

        Directory.CreateDirectory(TempFolder);
        AssetDatabase.Refresh();

        var alreadyBroken = new List<string>();
        var brokenByUs = new List<string>();

        string[] regions = EnumerateRegions()
            .Where(r => !ReferenceRegions.Contains(r))
            .ToArray();

        try
        {
            for (int r = 0; r < regions.Length; r++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Comparaison avec la sauvegarde",
                        $"{regions[r]} ({r + 1}/{regions.Length})", (float)r / regions.Length))
                {
                    break;
                }

                // Combien de tranches distinctes cette region DEVRAIT montrer ?
                var expected = new HashSet<int>();
                foreach (string name in ReadL2Names(regions[r]))
                {
                    if (packOf.TryGetValue(name, out string pack) && indexOf.TryGetValue(pack, out int idx))
                    {
                        expected.Add(idx);
                    }
                }

                if (expected.Count <= 1)
                {
                    continue;
                }

                // Combien en montre-t-elle aujourd'hui ?
                int actualNow = CountVisibleLayers(
                    AssetDatabase.LoadAssetAtPath<TerrainData>(
                        $"{MapsFolder}/{regions[r]}/TerrainData/{regions[r]}.asset"));

                // Ce que sa PROPRE sauvegarde produirait si on la reindexait.
                //
                // C'est la seule comparaison honnete. Compter les tranches
                // "attendues" d'apres les noms de couches sur-signale : une
                // texture peinte sur moins de 1 % de la surface n'apparait
                // legitimement pas. Les regions test 17_22 et 17_23, jugees
                // bonnes par l'utilisateur, ressortaient ainsi a -1.
                //
                // On rejoue donc la sommation sur la donnee d'origine et on
                // compte les tranches qui depassent reellement 1 %.
                string source = Path.Combine(BackupRoot, regions[r], regions[r] + ".asset");
                if (!File.Exists(source))
                {
                    continue;
                }

                string temp = $"{TempFolder}/{regions[r]}.asset";
                File.Copy(source, temp.Replace('/', Path.DirectorySeparatorChar), true);
                AssetDatabase.ImportAsset(temp, ImportAssetOptions.ForceSynchronousImport);

                int shouldShow = CountVisibleAfterRemap(
                    AssetDatabase.LoadAssetAtPath<TerrainData>(temp),
                    ReadL2Names(regions[r]), packOf, indexOf);

                AssetDatabase.DeleteAsset(temp);

                if (shouldShow < 0 || actualNow >= shouldShow)
                {
                    continue;
                }

                string line = $"{regions[r]} : devrait montrer {shouldShow}, "
                              + $"en montre {actualNow}  (-{shouldShow - actualNow})";

                if (actualNow <= 1 && shouldShow > 1) { brokenByUs.Add(line); }
                else { brokenByUs.Add(line); }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.DeleteAsset(TempFolder);
            AssetDatabase.Refresh();
        }

        var report = new System.Text.StringBuilder();
        report.AppendLine("[Mutualisation] Origine des splatmaps degenerees.");
        report.AppendLine();
        report.AppendLine($"  {brokenByUs.Count} region(s) montrant MOINS que ce que leur sauvegarde donnerait.");
        report.AppendLine("  (comparaison faite en rejouant la sommation sur la donnee d'origine,");
        report.AppendLine("   donc a l'abri du sur-signalement du diagnostic 15)");

        if (brokenByUs.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("  A RESTAURER PUIS REINDEXER :");
            foreach (string b in brokenByUs.Take(80)) { report.AppendLine($"    {b}"); }
            if (brokenByUs.Count > 80) { report.AppendLine($"    ... et {brokenByUs.Count - 80} autre(s)"); }
            report.AppendLine();
            report.AppendLine("  Liste brute pour restauration :");
            report.AppendLine("    " + string.Join(" ", brokenByUs.Select(b => b.Split(' ')[0])));
        }
        else
        {
            report.AppendLine();
            report.AppendLine("  Aucune region ne montre moins que sa sauvegarde : rien a reparer.");
        }

        Debug.Log(report.ToString());
    }

    /// Rejoue la sommation de la reindexation sur une donnee d'ORIGINE et
    /// compte les tranches partagees qui depasseraient reellement 1 %.
    ///
    /// C'est la reference honnete : elle dit ce que la region devrait montrer
    /// une fois mutualisee, en tenant compte du fait que plusieurs couches L2
    /// retombent souvent sur le meme pack, et qu'une texture a peine peinte ne
    /// doit pas etre comptee.
    private static int CountVisibleAfterRemap(TerrainData data, List<string> l2Names,
                                              Dictionary<string, string> packOf,
                                              Dictionary<string, int> indexOf)
    {
        if (data == null || l2Names == null || l2Names.Count == 0)
        {
            return -1;
        }

        const int Step = 8;
        int w = data.alphamapWidth, h = data.alphamapHeight;
        float[,,] a = data.GetAlphamaps(0, 0, w, h);
        int oldLayers = a.GetLength(2);

        if (oldLayers != l2Names.Count)
        {
            return -1;
        }

        var target = new int[oldLayers];
        for (int i = 0; i < oldLayers; i++)
        {
            target[i] = packOf.TryGetValue(l2Names[i], out string pack)
                        && indexOf.TryGetValue(pack, out int idx) ? idx : -1;
        }

        var sums = new double[32];
        for (int y = 0; y < h; y += Step)
        {
            for (int x = 0; x < w; x += Step)
            {
                for (int i = 0; i < oldLayers; i++)
                {
                    if (target[i] >= 0) { sums[target[i]] += a[y, x, i]; }
                }
            }
        }

        double total = sums.Sum();
        return total > 0 ? sums.Count(s => s / total >= 0.01) : 0;
    }

    /// Nombre de couches portant au moins 1 % du poids total.
    private static int CountVisibleLayers(TerrainData data)
    {
        if (data == null || data.terrainLayers == null || data.terrainLayers.Length == 0)
        {
            return -1;
        }

        const int Step = 8;
        int layers = data.terrainLayers.Length;
        int w = data.alphamapWidth, h = data.alphamapHeight;
        float[,,] a = data.GetAlphamaps(0, 0, w, h);

        var sums = new double[layers];
        for (int y = 0; y < h; y += Step)
        {
            for (int x = 0; x < w; x += Step)
            {
                for (int i = 0; i < layers; i++) { sums[i] += a[y, x, i]; }
            }
        }

        double total = sums.Sum();
        return total > 0 ? sums.Count(s => s / total >= 0.01) : 0;
    }

    // ================================================================
    //  ETAPE 3b - la surcharge de scene
    // ================================================================

    /// Fait pointer le templateMaterial des SCENES sur le maitre.
    ///
    /// POURQUOI CETTE ETAPE EXISTE
    /// Corriger le prefab ne suffit pas : chaque scene de region porte une
    /// surcharge d'instance sur templateMaterial, heritee de l'epoque ou les
    /// regions ont ete montees a la main. Une surcharge gagne toujours sur la
    /// valeur du prefab.
    ///
    /// Mesure du 2026-08-16 sur 18_22 : prefab pointant sur le maitre
    /// (e06cfc28), scene pointant sur l'ancien materiau (e6987199). MicroSplat
    /// resynchronisait alors les couches du terrain sur l'ancienne config a 9
    /// textures, annulant la reindexation a chaque ouverture.
    ///
    /// A LANCER AVANT LA REINDEXATION : une fois le maitre en place, MicroSplat
    /// synchronise le terrain sur 28 couches de lui-meme, et la reindexation
    /// ecrit dans un terrain deja correctement dimensionne.
    [MenuItem("L2/Terrain/Mutualisation/3e. Basculer les SCENES sur le maitre", false, 186)]
    public static void RetargetScenes()
    {
        Material master = AssetDatabase.LoadAssetAtPath<Material>(MasterMaterialPath);

        if (master == null)
        {
            Debug.LogError($"[Mutualisation] Materiau maitre introuvable ({MasterMaterialPath}).");
            return;
        }

        string[] regions = EnumerateRegions()
            .Where(r => !ReferenceRegions.Contains(r))
            .ToArray();

        if (!EditorUtility.DisplayDialog("Basculer les scenes sur le maitre",
                $"{regions.Length} scene(s) vont etre ouvertes, modifiees et enregistrees.\n\n"
                + "Talking Island est exclue.\n\n"
                + "Fermez vos scenes en cours avant de lancer.",
                "Lancer", "Annuler"))
        {
            return;
        }

        int done = 0, skipped = 0;

        for (int i = 0; i < regions.Length; i++)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Bascule des scenes",
                    $"{regions[i]} ({i + 1}/{regions.Length})", (float)i / regions.Length))
            {
                break;
            }

            string scenePath = $"{ScenesFolder}/{regions[i]}.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                skipped++;
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool changed = false;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (var mst in root.GetComponentsInChildren<JBooth.MicroSplat.MicroSplatTerrain>(true))
                {
                    if (mst.templateMaterial != master)
                    {
                        mst.templateMaterial = master;
                        EditorUtility.SetDirty(mst);
                        changed = true;
                    }
                }

                foreach (var t in root.GetComponentsInChildren<UnityEngine.Terrain>(true))
                {
                    if (t.materialTemplate != master)
                    {
                        t.materialTemplate = master;
                        EditorUtility.SetDirty(t);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                done++;
            }
            else
            {
                skipped++;
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();

        Debug.Log($"[Mutualisation] Scenes basculees : {done} modifiee(s), {skipped} inchangee(s).");
    }

    private const string ScenesFolder = "Assets/Resources/Scenes";

    // ================================================================
    //  ETAPE 3 - reindexation
    // ================================================================

    [MenuItem("L2/Terrain/Mutualisation/4. Reindexer les regions TEST", false, 188)]
    public static void ReindexTestRegions()
    {
        Reindex(TestRegions, "regions test");
    }

    [MenuItem("L2/Terrain/Mutualisation/5. Reindexer TOUTES les regions", false, 189)]
    public static void ReindexAllRegions()
    {
        string[] regions = EnumerateRegions()
            .Where(r => !ReferenceRegions.Contains(r))
            .ToArray();

        if (!EditorUtility.DisplayDialog("Reindexer toutes les regions",
                $"{regions.Length} region(s) vont voir leurs SPLATMAPS REECRITES.\n\n"
                + "Talking Island (16_24, 16_25, 17_24, 17_25) est exclue.\n\n"
                + "Cette operation est IRREVERSIBLE sans la sauvegarde\n"
                + "_backup_terraindata_20260814/.\n\n"
                + "Avez-vous verifie le rendu des 4 regions test ?",
                "Lancer", "Annuler"))
        {
            return;
        }

        Reindex(regions, "toutes les regions");
    }

    private static void Reindex(string[] regions, string label)
    {
        List<string> order = ResolveSharedOrder();
        if (order == null)
        {
            return;
        }

        var indexOf = new Dictionary<string, int>();
        for (int i = 0; i < order.Count; i++)
        {
            indexOf[order[i]] = i;
        }

        TerrainLayer[] sharedLayers = LoadSharedLayers(order);
        if (sharedLayers == null)
        {
            return;
        }

        Dictionary<string, string> packOf = BuildSubstitutionMap();
        if (packOf == null)
        {
            return;
        }

        int done = 0, failed = 0;

        // Taille du fichier AVANT, pour les regions qu'on croit avoir traitees.
        //
        // POURQUOI VERIFIER LE DISQUE
        // Rapporter un succes ne prouve pas que l'ecriture a eu lieu : Unity
        // peut recharger l'asset depuis le disque et jeter nos modifications
        // sans un mot. Constate deux fois - d'abord via une scene ouverte
        // (garde-fou ajoute plus haut), puis le 2026-08-17 sur douze regions
        // qui annoncaient toutes "N -> 28 couches, poids conserve a 100%" alors
        // que leur fichier n'avait pas bouge d'un octet.
        //
        // Changer le nombre de couches change forcement le nombre de cartes de
        // controle, donc la taille du fichier - a la hausse comme a la baisse.
        // Une taille rigoureusement INCHANGEE est la preuve mecanique que rien
        // n'a ete ecrit.
        var sizeBefore = new Dictionary<string, long>();

        for (int i = 0; i < regions.Length; i++)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Reindexation des splatmaps",
                    $"{regions[i]} ({i + 1}/{regions.Length})", (float)i / regions.Length))
            {
                break;
            }

            string assetPath = $"{MapsFolder}/{regions[i]}/TerrainData/{regions[i]}.asset";
            long before = File.Exists(assetPath) ? new FileInfo(assetPath).Length : 0;

            try
            {
                if (ReindexOne(regions[i], packOf, indexOf, sharedLayers))
                {
                    done++;
                    sizeBefore[regions[i]] = before;
                }
                else { failed++; }
            }
            catch (Exception e)
            {
                failed++;
                Debug.LogError($"[Mutualisation] {regions[i]} : {e.Message}");
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();

        var notWritten = new List<string>();
        var collapsed = new List<string>();

        foreach (var kv in sizeBefore)
        {
            string assetPath = $"{MapsFolder}/{kv.Key}/TerrainData/{kv.Key}.asset";
            long after = File.Exists(assetPath) ? new FileInfo(assetPath).Length : 0;

            if (after == kv.Value)
            {
                notWritten.Add($"{kv.Key} ({kv.Value / (1024 * 1024)} Mo, inchange)");
                continue;
            }

            // La taille a bouge : reste a verifier le CONTENU.
            if (!_expectedVisible.TryGetValue(kv.Key, out int expected) || expected <= 1)
            {
                continue;
            }

            int actual = CountVisibleLayers(AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath));
            if (actual >= 0 && actual < expected)
            {
                collapsed.Add($"{kv.Key} : {expected} tranche(s) ecrite(s), {actual} relue(s)");
            }
        }

        var summary = new System.Text.StringBuilder();
        summary.AppendLine($"[Mutualisation] {label} : {done} region(s) reindexee(s), {failed} echec(s).");

        if (notWritten.Count > 0 || collapsed.Count > 0)
        {
            if (notWritten.Count > 0)
            {
                summary.AppendLine();
                summary.AppendLine($"ATTENTION - {notWritten.Count} region(s) annoncee(s) reussie(s) mais NON ECRITE(S) "
                                   + "sur le disque (taille inchangee) :");
                foreach (string n in notWritten.Take(30)) { summary.AppendLine($"  {n}"); }
                if (notWritten.Count > 30) { summary.AppendLine($"  ... et {notWritten.Count - 30} autre(s)"); }
            }

            if (collapsed.Count > 0)
            {
                summary.AppendLine();
                summary.AppendLine($"ATTENTION - {collapsed.Count} region(s) ECRITE(S) puis EFFONDREE(S) : "
                                   + "elles relisent moins de tranches qu'on leur en a ecrit.");
                foreach (string c in collapsed.Take(30)) { summary.AppendLine($"  {c}"); }
                if (collapsed.Count > 30) { summary.AppendLine($"  ... et {collapsed.Count - 30} autre(s)"); }
            }

            summary.AppendLine("Le compte de reussites ci-dessus est donc trompeur.");
            Debug.LogError(summary.ToString());
            return;
        }

        summary.AppendLine("Toutes les reussites sont confirmees sur le disque, contenu verifie.");
        Debug.Log(summary.ToString());
    }

    private static bool ReindexOne(string mapName, Dictionary<string, string> packOf,
                                   Dictionary<string, int> indexOf, TerrainLayer[] sharedLayers)
    {
        // UNE SCENE OUVERTE ANNULE LE TRAVAIL.
        //
        // Quand la scene d'une region est chargee, Unity tient son TerrainData
        // en memoire et le reserialise depuis celle-ci. Les modifications
        // faites au niveau de l'asset sont alors ecrasees sans un mot : la
        // passe rapporte un succes et le fichier ne bouge pas.
        //
        // Constate le 2026-08-16 sur 18_23, dont la scene etait ouverte : 28
        // couches annoncees, 11 sur le disque. Sur 148 regions, ce silence
        // serait invisible.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene open = SceneManager.GetSceneAt(i);
            if (open.isLoaded && open.name == mapName)
            {
                Debug.LogError($"[Mutualisation] {mapName} : sa scene est OUVERTE. "
                               + "Fermez-la avant de relancer, sinon Unity annulera l'ecriture.");
                return false;
            }
        }

        string dataPath = $"{MapsFolder}/{mapName}/TerrainData/{mapName}.asset";
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);

        if (data == null)
        {
            Debug.LogWarning($"[Mutualisation] {mapName} : TerrainData introuvable, region ignoree.");
            return false;
        }

        List<string> l2Names = ReadL2Names(mapName);
        if (l2Names.Count == 0)
        {
            Debug.LogWarning($"[Mutualisation] {mapName} : aucune couche L2 lisible, region ignoree.");
            return false;
        }

        int w = data.alphamapWidth;
        int h = data.alphamapHeight;
        float[,,] old = data.GetAlphamaps(0, 0, w, h);
        int oldLayers = old.GetLength(2);

        if (oldLayers != l2Names.Count)
        {
            Debug.LogWarning($"[Mutualisation] {mapName} : {oldLayers} splatmaps pour {l2Names.Count} noms L2. "
                             + "Incoherence - region ignoree pour ne pas melanger les couches.");
            return false;
        }

        // Table ancien index -> tranche partagee.
        var target = new int[oldLayers];
        int maxIndex = -1;

        for (int i = 0; i < oldLayers; i++)
        {
            target[i] = -1;

            if (packOf.TryGetValue(l2Names[i], out string pack)
                && !string.IsNullOrEmpty(pack)
                && indexOf.TryGetValue(pack, out int idx))
            {
                target[i] = idx;
                maxIndex = Mathf.Max(maxIndex, idx);
            }
        }

        if (maxIndex < 0)
        {
            Debug.LogWarning($"[Mutualisation] {mapName} : aucune couche resolue, region ignoree.");
            return false;
        }

        // On ne garde que les tranches jusqu'au plus grand index utilise.
        //
        // POURQUOI C'EST SUR - correction du 2026-08-17
        // J'avais impose les 28 tranches a toutes les regions, en croyant
        // qu'une carte de controle non liee etait lue en BLANC et donnait donc
        // un poids parasite aux tranches absentes. C'etait faux. Le shader
        // genere declare lui-meme ses defauts :
        //
        //     _Control0 ("Control0", 2D) = "red"     <- plein poids tranche 0
        //     _Control1..6              = "black"    <- poids ZERO
        //
        // Une carte non liee vaut donc noir, soit exactement le meme resultat
        // qu'une carte liee et vide. Seul _Control0 doit etre fourni, ce qui
        // est toujours le cas.
        //
        // Le cout de mon erreur : sept cartes de controle par region au lieu
        // de deux ou trois, soit Maps qui passe de 7 a 11 Go pour un rendu
        // identique.
        //
        // ATTENTION - ce n'est PAS un remappage : une region qui utilise la
        // tranche 26 a bien besoin de 27 couches. Le gain depend donc de la
        // region, il n'est pas uniforme.
        int newLayers = maxIndex + 1;
        var fresh = new float[h, w, newLayers];

        // SOMME et non affectation : plusieurs anciennes couches peuvent viser
        // la meme tranche.
        double before = 0, after = 0;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                for (int i = 0; i < oldLayers; i++)
                {
                    float v = old[y, x, i];
                    before += v;

                    if (target[i] >= 0)
                    {
                        fresh[y, x, target[i]] += v;
                    }
                }

                for (int i = 0; i < newLayers; i++)
                {
                    after += fresh[y, x, i];
                }
            }
        }

        // Un ecart trahirait des couches non resolues, dont le poids serait
        // perdu : le terrain deviendrait transparent par endroits.
        double loss = before > 0 ? (before - after) / before : 0;
        if (loss > 0.001)
        {
            Debug.LogError($"[Mutualisation] {mapName} : {loss:P2} du poids perdu "
                           + "(couches sans substitution). Region NON modifiee.");
            return false;
        }

        // COMBIEN DE TRANCHES CETTE REGION DOIT-ELLE MONTRER ?
        //
        // Mesure a partir des poids qu'on s'apprete a ecrire, donc sans
        // supposition. Sert de reference a la verification d'apres passe :
        // une region qui en montre moins ensuite a ete detruite APRES
        // l'ecriture, ce que ni le controle de poids ni celui de taille de
        // fichier ne savent voir.
        //
        // Constate le 2026-08-17 : 36 regions saines avant la mutualisation
        // ressorties a 0 ou 1 tranche, alors que chaque passe annoncait
        // "poids conserve a 100 %".
        {
            var sliceSums = new double[newLayers];
            for (int y = 0; y < h; y += 8)
            {
                for (int x = 0; x < w; x += 8)
                {
                    for (int i = 0; i < newLayers; i++) { sliceSums[i] += fresh[y, x, i]; }
                }
            }

            double sliceTotal = sliceSums.Sum();
            _expectedVisible[mapName] = sliceTotal > 0
                ? sliceSums.Count(s => s / sliceTotal >= 0.01)
                : 0;
        }

        // L'ordre compte : changer les couches redimensionne les splatmaps,
        // il faut donc ecrire les poids APRES.
        data.terrainLayers = sharedLayers.Take(newLayers).ToArray();
        data.SetAlphamaps(0, 0, fresh);

        EditorUtility.SetDirty(data);

        bool retargeted = RetargetPrefabMaterial(mapName);

        Debug.Log($"[Mutualisation] {mapName} : {oldLayers} -> {newLayers} couches, "
                  + $"poids conserve a {(1 - loss):P2}"
                  + (retargeted ? ", materiau maitre assigne." : ", MATERIAU NON ASSIGNE."));

        return true;
    }

    /// Fait pointer le MicroSplatTerrain de la region sur le materiau maitre.
    ///
    /// C'est ce qui mutualise reellement : le templateMaterial est le mecanisme
    /// prevu par MicroSplat (MicroSplatTerrain.cs:209 instancie depuis lui).
    /// Toutes les regions partagent alors un shader, donc un pipeline state,
    /// tout en gardant chacune leur instance de materiau.
    ///
    /// On edite le PREFAB et non la scene : c'est lui qui fait autorite pour le
    /// streaming, et cela evite d'ouvrir 148 scenes.
    private static bool RetargetPrefabMaterial(string mapName)
    {
        Material master = AssetDatabase.LoadAssetAtPath<Material>(MasterMaterialPath);

        if (master == null)
        {
            Debug.LogError($"[Mutualisation] Materiau maitre introuvable ({MasterMaterialPath}). "
                           + "Lancez les etapes 3c et 3d.");
            return false;
        }

        string prefabPath = $"{MapsFolder}/{mapName}/{mapName}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            var mst = root.GetComponentInChildren<JBooth.MicroSplat.MicroSplatTerrain>(true);
            if (mst == null)
            {
                return false;
            }

            mst.templateMaterial = master;

            // Le terrain garde une reference directe au materiau : la laisser
            // pointer sur l'ancien shader annulerait la mutualisation.
            var terrain = root.GetComponentInChildren<UnityEngine.Terrain>(true);
            if (terrain != null)
            {
                terrain.materialTemplate = master;
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ================================================================
    //  Outillage commun
    // ================================================================

    private static TerrainLayer[] LoadSharedLayers(List<string> order)
    {
        var layers = new TerrainLayer[order.Count];

        for (int i = 0; i < order.Count; i++)
        {
            string path = $"{SharedFolder}/shared_{i:D2}_{order[i]}.terrainlayer";
            layers[i] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);

            if (layers[i] == null)
            {
                Debug.LogError($"[Mutualisation] Couche partagee manquante : {path}. "
                               + "Lancez d'abord l'etape 3.");
                return null;
            }
        }

        return layers;
    }

    private static Texture2D FindPackTexture(string pack, string suffix)
    {
        string folder = $"{PacksFolder}/{pack.ToLowerInvariant()}";
        string path = $"{folder}/{pack}_{suffix}.jpg";

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null)
        {
            return tex;
        }

        if (!Directory.Exists(folder))
        {
            return null;
        }

        // Les suffixes varient d'un pack a l'autre (jpg/png, casse) : on
        // retombe sur une recherche par motif plutot que d'echouer.
        foreach (string f in Directory.GetFiles(folder))
        {
            if (f.EndsWith(".meta") || f.IndexOf(suffix, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(f.Replace('\\', '/'));
            if (tex != null)
            {
                return tex;
            }
        }

        return null;
    }

    private static Dictionary<string, string> BuildSubstitutionMap()
    {
        var settings = AssetDatabase.LoadAssetAtPath<L2TerrainTextureSettings>(
            L2TerrainTextureSettings.AssetPath);

        if (settings == null)
        {
            Debug.LogError($"[Mutualisation] Asset de reglages introuvable ({L2TerrainTextureSettings.AssetPath}).");
            return null;
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in settings.substitutions)
        {
            if (!string.IsNullOrEmpty(s.l2Texture) && !string.IsNullOrEmpty(s.pbrPack))
            {
                map[s.l2Texture] = s.pbrPack;
            }
        }

        return map;
    }

    /// Ordre partage : c'est lui qui definit ce que signifie chaque tranche
    /// (shared_00_..., shared_01_..., etc.) dans le materiau maitre deja
    /// compile. Une fois ces fichiers crees (etape 3), leur nom EST la
    /// verite : on le relit tel quel plutot que de le recalculer.
    ///
    /// Recalculer via BuildOrderForApply serait fragile - toute modification
    /// ulterieure de la table de substitution (nouvelle texture classee)
    /// change les frequences et peut reordonner le resultat, alors que les
    /// 32 fichiers .terrainlayer et le materiau maitre restent, eux, figes a
    /// l'ordre d'origine. Constate le 2026-08-17 : l'ajout de 14 substitutions
    /// a deplace Wild_Grass de la tranche 09 a 08, provoquant un
    /// "Couche partagee manquante" a la reindexation - echec propre (rien
    /// n'a ete touche), mais qui aurait pu passer inaperçu avec un algorithme
    /// moins strict.
    ///
    /// Le calcul via BuildOrderForApply ne sert donc plus qu'a la toute
    /// premiere creation (etape 3, quand aucun fichier n'existe encore).
    private static List<string> ResolveSharedOrder()
    {
        List<string> onDisk = ReadOrderFromDisk();
        if (onDisk != null)
        {
            return onDisk;
        }

        Dictionary<string, string> packOf = BuildSubstitutionMap();
        if (packOf == null)
        {
            return null;
        }

        var layersOf = new Dictionary<string, List<string>>();
        foreach (string region in EnumerateRegions())
        {
            List<string> names = ReadL2Names(region);
            if (names.Count > 0)
            {
                layersOf[region] = names;
            }
        }

        return L2MicroSplatMutualizer.BuildOrderForApply(layersOf, packOf);
    }

    /// Relit l'ordre depuis les noms de fichiers "shared_NN_<pack>.terrainlayer"
    /// deja presents sur le disque. Renvoie null si le dossier est vide ou
    /// absent (premiere creation), pour laisser ResolveSharedOrder calculer
    /// un ordre initial.
    private static List<string> ReadOrderFromDisk()
    {
        if (!Directory.Exists(SharedFolder))
        {
            return null;
        }

        var pattern = new Regex(@"^shared_(\d+)_(.+)$");
        var found = new List<(int index, string pack)>();

        foreach (string path in Directory.GetFiles(SharedFolder, "shared_*.terrainlayer"))
        {
            Match m = pattern.Match(Path.GetFileNameWithoutExtension(path));
            if (m.Success && int.TryParse(m.Groups[1].Value, out int idx))
            {
                found.Add((idx, m.Groups[2].Value));
            }
        }

        if (found.Count == 0)
        {
            return null;
        }

        return found.OrderBy(f => f.index).Select(f => f.pack).ToList();
    }

    private static List<string> ReadL2Names(string region)
    {
        string folder = $"{MapsFolder}/{region}/TerrainData";
        var found = new List<(int index, string name)>();

        if (!Directory.Exists(folder))
        {
            return new List<string>();
        }

        var pattern = new Regex($@"^{Regex.Escape(region)}_layer_(\d+)_(.+)$");

        foreach (string path in Directory.GetFiles(folder, $"{region}_layer_*.asset"))
        {
            Match m = pattern.Match(Path.GetFileNameWithoutExtension(path));
            if (m.Success && int.TryParse(m.Groups[1].Value, out int idx))
            {
                found.Add((idx, m.Groups[2].Value));
            }
        }

        return found.OrderBy(f => f.index).Select(f => f.name).ToList();
    }

    private static string[] EnumerateRegions()
    {
        if (!Directory.Exists(MapsFolder))
        {
            return new string[0];
        }

        return Directory.GetDirectories(MapsFolder)
            .Select(Path.GetFileName)
            .Where(n => Regex.IsMatch(n, @"^\d+_\d+$"))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
    }
}
#endif
