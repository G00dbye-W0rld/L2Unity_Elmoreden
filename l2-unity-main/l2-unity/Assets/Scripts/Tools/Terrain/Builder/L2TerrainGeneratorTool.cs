#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JBooth.MicroSplat;
using UnityEditor;
using UnityEngine;
using static JBooth.MicroSplat.MicroSplatPropData;
using static L2TerrainGeneratorTextureMatcher;

public class L2TerrainGeneratorTool : MonoBehaviour
{
    /// Echelle appliquee a une texture absente des tables de correspondance.
    ///
    /// Valait 3, ce qui etirait le motif sur 208 unites - un sol flou et
    /// gigantesque. La quasi-totalite des packs PBR du projet sont regles entre
    /// 32 et 64 ; 32 est la valeur retenue le 2026-08-09 comme point de depart
    /// raisonnable pour une texture inconnue.
    ///
    /// Ne concerne QUE les textures sans entree explicite : les valeurs basses
    /// heritees de Talking Island (1 a 7) restent celles des tables et ne
    /// bougent pas.
    private const float DefaultSplatUvScale = 32f;


    public static int UV_TEXTURE_SIZE = 256;
    public static int UV_LAYER_ALPHAMAP_SIZE = 1024;
    public static int DECO_LAYER_ALPHAMAP_SIZE = 512;
    public static float UV_TILE_SIZE = 5f;
    public static float MAP_SCALE = 1f;

    public int uvTextureSize = 256;
    public int uvLayerAlphaMapSize = 1024;
    public int decoLayerAlphaMapSize = 512;
    public float uvTileSize = 5f;
    public float mapScale = 1f;

    public bool generateMap = true;
    public static List<L2TerrainInfo> terrainInfos;
    public List<L2StaticMeshActor> meshActors;
    public static List<Terrain> terrains;
    public static Dictionary<string, Terrain> terrainsDict;
    public List<MapGenerationData> maps;

    // ================================================================
    //  MODE BATCH — import sans interface
    //
    //  Les entrees de menu ci-dessous ouvrent toutes un OpenFilePanel,
    //  qui GELE le processus en -batchmode. Ce point d'entree lit donc
    //  le nom de la map sur la ligne de commande et appelle directement
    //  les memes workers (GenerateMap / ConvertTerrainToMicroplat /
    //  UpdateMicrosplatParams), sans dialogue.
    //
    //  Appel :
    //    Unity.exe -batchmode -quit -projectPath <projet> \
    //      -executeMethod L2TerrainGeneratorTool.BatchGenerateTerrain \
    //      -mapName 17_23
    //
    //  Prerequis : le .t3d doit deja etre a
    //  Assets/Resources/Data/Maps/<mapName>/Meta/<mapName>.t3d
    //  (produit par l'outil l2-map-export).
    // ================================================================
    public static void BatchGenerateTerrain()
    {
        string mapName = GetCommandLineArg("-mapName");
        if (string.IsNullOrEmpty(mapName))
        {
            Debug.LogError("[Batch] Argument -mapName manquant.");
            EditorApplication.Exit(1);
            return;
        }

        string t3d = Path.Combine(Application.dataPath, "Resources/Data/Maps", mapName, "Meta", mapName + ".t3d");
        if (!File.Exists(t3d))
        {
            Debug.LogError($"[Batch] .t3d introuvable : {t3d}");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"[Batch] Generation du terrain pour '{mapName}'");

        try
        {
            MapGenerationData data = new MapGenerationData();
            data.mapName = mapName;
            data.generateDecoLayers = true;
            data.generateUVLayers = true;
            data.generateHeightmaps = true;
            data.generateStaticMeshes = false;
            data.convertToMicrosplat = true;
            data.generationMode = GenerationMode.Generate;

            GenerateMap(new List<MapGenerationData> { data });
            Debug.Log("[Batch] Etape 04 (terrain) terminee.");

            ConvertTerrainToMicroplat(data);
            Debug.Log("[Batch] Etape 05 (microsplat) terminee.");

            UpdateMicrosplatParams(data);
            Debug.Log("[Batch] Etape 06 (parametres microsplat) terminee.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Batch] '{mapName}' : import terrain OK.");
        }
        catch (System.Exception e)
        {
            // En batch, une exception non rattrapee laisse Unity rendre 0 :
            // le script appelant croirait a une reussite.
            Debug.LogError($"[Batch] Echec sur '{mapName}' : {e}");
            EditorApplication.Exit(1);
        }
    }

    private static string GetCommandLineArg(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }
        return null;
    }

    // Rattrapage pour une scene deja encombree par d'anciennes passes : les
    // etapes 03/04 nettoient desormais derriere elles, mais ce qui a ete
    // accumule avant reste en place. Vide tous les objets generes de la region
    // choisie, sans toucher au reste de la scene.
    [MenuItem("L2/Import/00 Scene - Nettoyer les objets generes", false, 20)]
    static void CleanGeneratedObjects()
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

        int removed = L2TerrainGenerator.DestroyContainers(
            L2TerrainGenerator.TerrainObjectName(mapName),
            mapName,
            L2TerrainGenerator.StaticMeshContainerName(mapName),
            "StaticMeshes",
            "Brushes");

        Debug.Log($"[Scene] '{mapName}' : {removed} objet(s) genere(s) supprime(s). " +
                  "Relancez les etapes 03 a 07, puis sauvegardez la scene.");
    }

    [MenuItem("L2/Import/04 Terrain - Generate terrain", false, 25)]
    static void GenerateTerrain()
    {
        string title = "Select terrain t3d";
        string directory = Path.Combine(Application.dataPath, "Resources/Data/Maps");
        string extension = "t3d";

        string fileToProcess = EditorUtility.OpenFilePanel(title, directory, extension);

        if (!string.IsNullOrEmpty(fileToProcess))
        {
            Debug.Log("Selected file: " + fileToProcess);
            GenerateTerrainFor(Path.GetFileNameWithoutExtension(fileToProcess));
        }
    }

    /// Etape 04 sans dialogue. Voir L2MapBatchImporter.
    public static void GenerateTerrainFor(string mapName)
    {
        List<MapGenerationData> mapsToGenerate = new List<MapGenerationData>();
        MapGenerationData data = new MapGenerationData();

        data.mapName = mapName;
        data.generateDecoLayers = true;
        data.generateUVLayers = true;
        data.generateHeightmaps = true;
        data.generateStaticMeshes = false;
        data.convertToMicrosplat = true;
        data.generationMode = GenerationMode.Generate;
        mapsToGenerate.Add(data);
        GenerateMap(mapsToGenerate);
    }


    [MenuItem("L2/Import/05 Terrain - Convert terrain to microsplat", false, 26)]
    static void ConvertTerrain()
    {
        string title = "Select terrain t3d";
        string directory = Path.Combine(Application.dataPath, "Resources/Data/Maps");
        string extension = "t3d";

        string fileToProcess = EditorUtility.OpenFilePanel(title, directory, extension);

        if (!string.IsNullOrEmpty(fileToProcess))
        {
            Debug.Log("Selected file: " + fileToProcess);
            ConvertTerrainFor(Path.GetFileNameWithoutExtension(fileToProcess));
        }
    }

    /// Etape 05 sans dialogue. Voir L2MapBatchImporter.
    public static void ConvertTerrainFor(string mapName)
    {
        MapGenerationData data = new MapGenerationData();
        data.mapName = mapName;
        data.convertToMicrosplat = true;
        data.generationMode = GenerationMode.Generate;
        ConvertTerrainToMicroplat(data);
    }


    [MenuItem("L2/Import/06 Terrain - Update microsplat params", false, 27)]
    static void Update()
    {
        string title = "Select terrain t3d";
        string directory = Path.Combine(Application.dataPath, "Resources/Data/Maps");
        string extension = "t3d";

        string fileToProcess = EditorUtility.OpenFilePanel(title, directory, extension);

        if (!string.IsNullOrEmpty(fileToProcess))
        {
            Debug.Log("Selected file: " + fileToProcess);
            UpdateMicrosplatFor(Path.GetFileNameWithoutExtension(fileToProcess));
        }
    }

    /// Etape 06 sans dialogue. Voir L2MapBatchImporter.
    public static void UpdateMicrosplatFor(string mapName)
    {
        MapGenerationData data = new MapGenerationData();
        data.mapName = mapName;
        data.convertToMicrosplat = true;
        data.generationMode = GenerationMode.Generate;
        UpdateMicrosplatParams(data);
    }

    [MenuItem("L2/Import/03 StaticMeshes - Generate staticmeshes", false, 24)]
    static void GenerateStaticMeshes()
    {
        // Demande la map, comme les etapes 04/05/06. Avant, le nom etait
        // ecrit en dur ("l2_lobby") : quelle que soit la region sur laquelle
        // on travaillait, cette etape regenerait les meshes du lobby, sans
        // aucun message - elle faisait simplement autre chose que demande.
        string title = "Select terrain t3d";
        string directory = Path.Combine(Application.dataPath, "Resources/Data/Maps");
        string extension = "t3d";

        string fileToProcess = EditorUtility.OpenFilePanel(title, directory, extension);
        if (string.IsNullOrEmpty(fileToProcess))
        {
            return;
        }

        GenerateStaticMeshesFor(Path.GetFileNameWithoutExtension(fileToProcess));
    }

    /// Etape 03 sans dialogue. Voir L2MapBatchImporter.
    public static void GenerateStaticMeshesFor(string mapName)
    {
        Debug.Log("[StaticMeshes] Map selectionnee : " + mapName);

        List<MapGenerationData> mapsToGenerate = new List<MapGenerationData>();
        MapGenerationData data = new MapGenerationData();

        data.mapName = mapName;
        data.generateDecoLayers = false;
        data.generateUVLayers = false;
        data.generateHeightmaps = false;
        data.generateStaticMeshes = true;
        data.generationMode = GenerationMode.Generate;
        mapsToGenerate.Add(data);

        GenerateMap(mapsToGenerate);
    }


    /// Regions candidates au raccord (etape 11).
    ///
    /// Decouverte automatique : tout dossier de Data/Maps dont le nom suit la
    /// convention "NN_NN" (l'identifiant de region) est candidat, qu'il soit
    /// present ou non dans la scene ouverte au moment de l'appel - c'est
    /// StitchTerrainSeams qui filtre selon la scene. Avant, cette liste etait
    /// un tableau code en dur : chaque region importee demandait d'y ajouter
    /// une ligne et de recompiler, une etape facile a oublier une fois qu'on
    /// importe beaucoup de regions.
    private static string[] DiscoverStitchableRegions()
    {
        string mapsRoot = Path.Combine(Application.dataPath, "Resources/Data/Maps");
        if (!Directory.Exists(mapsRoot))
        {
            return new string[0];
        }

        System.Text.RegularExpressions.Regex regionName =
            new System.Text.RegularExpressions.Regex(@"^\d+_\d+$");

        return Directory.GetDirectories(mapsRoot)
            .Select(Path.GetFileName)
            .Where(name => regionName.IsMatch(name))
            .OrderBy(name => name)
            .ToArray();
    }

    [MenuItem("L2/Import/11 Terrain - Stitch terrain seams", false, 32)]
    static void StitchTerrainSeams()
    {
        Dictionary<string, Terrain> mapTerrains = new Dictionary<string, Terrain>();

        foreach (string mapId in DiscoverStitchableRegions())
        {
            // Region absente de la scene ouverte : on l'ignore avec un message
            // clair. L'ancienne version enchainait des GameObject.Find() sans
            // controle et levait une NullReferenceException des qu'une seule
            // region manquait - ce qui arrive forcement pendant un import,
            // quand toutes ne sont pas encore montees.
            // Meme correction que pour les etapes 05/06 : le terrain est cree
            // sous "terrain_<region>", pas sous "<region>".
            GameObject go = GameObject.Find(L2TerrainGenerator.TerrainObjectName(mapId))
                            ?? GameObject.Find(mapId);
            if (go == null)
            {
                Debug.Log($"[Stitch] Region '{mapId}' absente de la scene, ignoree.");
                continue;
            }

            Terrain terrain = go.GetComponent<Terrain>();
            if (terrain == null)
            {
                Debug.LogWarning($"[Stitch] '{mapId}' trouve mais sans composant Terrain, ignore.");
                continue;
            }

            mapTerrains.Add(mapId, terrain);
        }

        if (mapTerrains.Count < 2)
        {
            Debug.LogWarning($"[Stitch] {mapTerrains.Count} region(s) trouvee(s) - il en faut au moins 2 pour raccorder quoi que ce soit.");
            return;
        }

        Debug.Log($"[Stitch] Raccord de {mapTerrains.Count} region(s) : {string.Join(", ", mapTerrains.Keys)}");

        L2TerrainGenerator generator = new L2TerrainGenerator();
        generator.StitchTerrainSeams(mapTerrains);
    }

    private static void GenerateMap(List<MapGenerationData> mapsToGenerate)
    {
        L2TerrainGenerator generator = new L2TerrainGenerator();
        terrainInfos = new List<L2TerrainInfo>();
        terrains = new List<Terrain>();
        terrainsDict = new Dictionary<string, Terrain>();

        for (int i = 0; i < mapsToGenerate.Count; i++)
        {

            if (!mapsToGenerate[i].enabled)
            {
                continue;
            }

            L2TerrainInfo terrainInfo = L2T3DInfoParser.LoadMetadata(mapsToGenerate[i].mapName);
            terrainInfos.Add(terrainInfo);

            Terrain terrain = generator.InstantiateTerrain(mapsToGenerate[i], terrainInfo);

            terrains.Add(terrain);

            terrainsDict.Add(mapsToGenerate[i].mapName, terrain);
        }

        // generator.StitchTerrainSeams(terrainsDict);
        //   generator.StitchTerrainSeams(terrainsDict);
    }

    /// Retrouve dans la scene ouverte le terrain produit par l'etape 04.
    ///
    /// Les etapes 05 et 06 le cherchaient sous le seul identifiant de region
    /// ("17_23"), alors que L2TerrainGenerator le cree prefixe
    /// ("terrain_17_23"). Elles ne le trouvaient donc jamais et retombaient
    /// sur un Instantiate depuis Resources : a chaque lancement un terrain de
    /// plus dans la scene, et des reglages MicroSplat appliques a cette copie
    /// pendant que le terrain reellement affiche restait sans echelle - d'ou
    /// l'impression d'une tuile unique a l'echelle de la region.
    ///
    /// On echoue franchement plutot que d'instancier : si le terrain n'est pas
    /// la, c'est que l'etape 04 n'a pas ete lancee, et le dire vaut mieux que
    /// de travailler en silence sur un objet que l'utilisateur ne voit pas.
    private static Terrain ResolveTerrain(string mapName)
    {
        GameObject terrainGo = GameObject.Find(L2TerrainGenerator.TerrainObjectName(mapName))
                               ?? GameObject.Find(mapName);

        if (terrainGo == null)
        {
            Debug.LogError($"[Terrain] '{mapName}' introuvable dans la scene ouverte. " +
                           "Lancez l'etape 04 avant les etapes 05 et 06.");
            return null;
        }

        Terrain terrain = terrainGo.GetComponent<Terrain>();
        if (terrain == null)
        {
            Debug.LogError($"[Terrain] '{terrainGo.name}' n'a pas de composant Terrain.");
        }

        return terrain;
    }

    private static void ConvertTerrainToMicroplat(MapGenerationData mapToGenerate)
    {
        Terrain terrain = ResolveTerrain(mapToGenerate.mapName);
        if (terrain == null)
        {
            return;
        }

        // AddComponent etait appele sans condition : relancer l'etape 05
        // empilait un MicroSplatTerrain de plus sur le meme objet.
        MicroSplatTerrain mst = terrain.GetComponent<MicroSplatTerrain>();
        if (mst == null)
        {
            mst = terrain.gameObject.AddComponent<MicroSplatTerrain>();
        }

        PruneBrokenTerrainLayers(terrain, mapToGenerate.mapName);

        // CONVERSION RESILIENTE.
        //
        // ConvertTerrains appelle CreateConfig(terrain), qui REECRIT les couches
        // du terrain. Sur certaines regions il les vide, puis se plante lui-meme
        // en relisant terrainLayers[0] (MicroSplatTerrainEditor_Convert.cs:171).
        // Le degat est donc interne a MicroSplat : aucune preparation en amont
        // ne peut l'empecher, on ne peut que le rattraper.
        //
        // Constate le 2026-08-11 : 114 regions en echec sur 148, alors que la
        // meme operation passe sur une region isolee.
        //
        // On garde donc une copie des couches, et si l'appel echoue on la
        // restaure avant de retenter UNE fois - la config et le materiau ayant
        // ete crees au premier passage, le second trouve un terrain complet.
        TerrainLayer[] snapshot = terrain.terrainData.terrainLayers;

        try
        {
            MicroSplatTerrainEditor.ConvertTerrains(new Terrain[] { terrain }, snapshot);
        }
        catch (System.Exception first)
        {
            terrain.terrainData.terrainLayers = snapshot;
            EditorUtility.SetDirty(terrain.terrainData);

            try
            {
                MicroSplatTerrainEditor.ConvertTerrains(new Terrain[] { terrain }, snapshot);
                Debug.LogWarning($"[Terrain] {mapToGenerate.mapName} : conversion MicroSplat reussie "
                                 + "au second essai, apres restauration des couches.");
            }
            catch (System.Exception second)
            {
                // On restaure une derniere fois pour ne pas laisser le terrain
                // sans couche, puis on laisse remonter : la region sera comptee
                // en echec, mais le lot continue.
                terrain.terrainData.terrainLayers = snapshot;
                EditorUtility.SetDirty(terrain.terrainData);
                Debug.LogError($"[Terrain] {mapToGenerate.mapName} : conversion MicroSplat impossible "
                               + $"apres deux essais. Couches restaurees.\n1er : {first.Message}\n2e : {second}");
                throw;
            }
        }

        mst.Sync();
    }

    /// Reapplique UNIQUEMENT les echelles UV, sans rien reconstruire.
    ///
    /// POURQUOI UNE PASSE DEDIEE
    /// Changer une echelle dans l'asset de reglages n'affecte que le propdata :
    /// une ligne par couche, ecrite via PerTexVector2.SplatUVScale. La config
    /// MicroSplat, le materiau, le shader et les texture arrays sont totalement
    /// etrangers a cette valeur.
    ///
    /// Passer par la re-substitution complete pour ca revient a supprimer
    /// MicroSplatData, relancer la conversion et recompiler trois texture
    /// arrays - environ une minute par region, soit plusieurs heures sur 148,
    /// pour ecrire quelques dizaines d'octets.
    ///
    /// Cette passe fait le strict necessaire : relire le nom L2 de chaque
    /// couche dans les metadonnees, resoudre l'echelle, l'ecrire. Quelques
    /// secondes par region.
    ///
    /// Retourne false si la region n'a pas pu etre traitee.
    public static bool ReapplyScalesFor(string mapName)
    {
        Terrain terrain = ResolveTerrain(mapName);
        if (terrain == null)
        {
            return false;
        }

        MicroSplatTerrain mst = terrain.GetComponent<MicroSplatTerrain>();
        if (mst == null || mst.propData == null)
        {
            Debug.LogError($"[Echelles] '{mapName}' n'a pas de MicroSplatTerrain exploitable.");
            return false;
        }

        L2TerrainInfo terrainInfo = L2T3DInfoParser.LoadMetadata(mapName);
        if (terrainInfo == null || terrainInfo.uvLayers == null)
        {
            Debug.LogError($"[Echelles] '{mapName}' : metadonnees introuvables.");
            return false;
        }

        int applied = 0, defaulted = 0;
        for (int layer = 0; layer < terrainInfo.uvLayers.Count; layer++)
        {
            if (terrainInfo.uvLayers[layer]?.texture == null)
            {
                continue;
            }

            string texName = terrainInfo.uvLayers[layer].texture.name;

            if (L2TerrainGeneratorTextureMatcher.Instance.TryGetScaleMatch(mapName, texName, out float scale))
            {
                applied++;
            }
            else
            {
                scale = DefaultSplatUvScale;
                defaulted++;
            }

            mst.propData.SetValue(layer, PerTexVector2.SplatUVScale, new Vector2(scale, scale));
        }

        EditorUtility.SetDirty(mst.propData);
        mst.Sync();

        Debug.Log($"[Echelles] {mapName} : {applied} couche(s) reglee(s), "
                  + $"{defaulted} sur la valeur par defaut ({DefaultSplatUvScale}).");
        return true;
    }

    /// Resolution des texture arrays MicroSplat.
    ///
    /// POURQUOI 512 ET PAS 1024
    /// Les arrays representent 6,6 Go sur les 10,1 Go du monde, soit 65% -
    /// de tres loin le premier poste. Chaque region en porte trois (diffuse,
    /// normSAO, specular) a 30 Mo piece en 1024.
    ///
    /// Diviser la resolution par deux divise le poids par QUATRE : environ
    /// 5 Go recuperes, sur le build comme sur la memoire au chargement.
    ///
    /// La compression n'est pas un levier : elle est deja active. Le
    /// "compression: 0" de la config est la premiere valeur de l'enumeration,
    /// AutomaticCompressed - et non "non compresse". Les 30 Mo pour 33 couches
    /// le confirment, sans compression ce serait 132 Mo.
    ///
    /// LA QUALITE RESTE LARGEMENT SUPERIEURE A L'ORIGINE
    /// Les textures du client de 2006 sont majoritairement en 256x256 (128 sur
    /// un echantillon de 200, plus 39 en 128 et 15 en 64). A 512 on garde donc
    /// quatre fois plus de pixels - sans compter les cartes de normales, de
    /// rugosite et d'occlusion que l'original n'a pas du tout.
    ///
    /// A l'echelle UV de 32, une texture 512 se repete tous les 19,5 unites,
    /// soit 26 pixels par unite : bien au-dela de ce qu'un sol vu de dessus
    /// demande.
    private const TextureArrayConfig.TextureSize ArrayResolution =
        TextureArrayConfig.TextureSize.k512;

    private static void ApplyArrayResolution(TextureArrayConfig cfg)
    {
        // Les reglages ne sont PAS directement sur la config : ils vivent dans
        // une classe imbriquee TextureArrayGroup, exposee via
        // defaultTextureSettings. Les adresser directement sur cfg ne compile
        // pas (CS1061).
        var s = cfg.defaultTextureSettings;

        s.diffuseSettings.textureSize = ArrayResolution;
        s.normalSettings.textureSize = ArrayResolution;
        s.smoothSettings.textureSize = ArrayResolution;
        s.specularSettings.textureSize = ArrayResolution;
        s.antiTileSettings.textureSize = ArrayResolution;
        s.emissiveSettings.textureSize = ArrayResolution;
    }

    /// Retire du terrain les couches dont l'asset n'existe plus.
    ///
    /// POURQUOI
    /// MicroSplatTerrainEditor.ConvertTerrains dereference chaque couche sans
    /// verification (MicroSplatTerrainEditor_Convert.cs:145,
    /// "terrainLayers[x].tileSize"). Une seule reference morte fait donc echouer
    /// TOUTE la region par NullReferenceException.
    ///
    /// Constate le 2026-08-10 : 116 regions en echec sur un traitement de masse,
    /// alors que la meme operation passait sur une region isolee. Les regions
    /// saines n'ont pas le probleme ; ce sont les sequelles des passages ou le
    /// dossier MicroSplatData etait supprime en entier - le terrain a garde des
    /// references vers des .terrainlayer disparus.
    ///
    /// Unity surcharge l'operateur == : une couche detruite se compare bien a
    /// null, meme si la case du tableau n'est pas techniquement vide.
    ///
    /// ON REPARE SANS RIEN RETIRER
    /// Supprimer les cases mortes serait le reflexe evident, mais il faut s'en
    /// garder : les splatmaps sont indexees sur la POSITION des couches. Passer
    /// de 11 a 9 couches decalerait tous les canaux au-dela du trou et
    /// detruirait la peinture des level designers.
    ///
    /// On remplace donc chaque case morte, en place, par la couche d'import
    /// correspondante ("{region}_layer_{index}_{nomL2}.asset"), qui existe
    /// toujours et porte le bon nom L2. A defaut, une couche neuve et vide -
    /// moins bien, mais l'index reste correct et la region passe.
    private static void PruneBrokenTerrainLayers(Terrain terrain, string mapName)
    {
        string folder = $"Assets/Resources/Data/Maps/{mapName}/TerrainData";

        // Les couches d'import, rangees par index. Ce sont elles qui font foi :
        // produites a l'etape 04, elles ne sont jamais reecrites.
        var imported = new Dictionary<int, TerrainLayer>();
        foreach (string guid in AssetDatabase.FindAssets("t:TerrainLayer", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(path);
            string prefix = $"{mapName}_layer_";

            if (!file.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                continue;
            }

            string rest = file.Substring(prefix.Length);
            int sep = rest.IndexOf('_');
            if (sep > 0 && int.TryParse(rest.Substring(0, sep), out int idx))
            {
                imported[idx] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            }
        }

        TerrainLayer[] layers = terrain.terrainData.terrainLayers;

        // CAS 1 : le terrain n'a plus AUCUNE couche.
        // MicroSplat lit terrainLayers[0] sans condition (ligne 171) : un
        // tableau nul ou vide le fait planter aussitot. On reconstruit alors
        // depuis les couches d'import, ce qui restitue le nombre d'origine -
        // donc le nombre de canaux qu'attendent les splatmaps.
        if (layers == null || layers.Length == 0)
        {
            if (imported.Count == 0)
            {
                Debug.LogError($"[Terrain] {mapName} : aucune couche, ni sur le terrain ni a l'import. "
                               + "Region ignoree - elle demande un reimport complet.");
                return;
            }

            int count = imported.Keys.Max() + 1;
            var rebuilt = new TerrainLayer[count];
            for (int i = 0; i < count; i++)
            {
                rebuilt[i] = imported.TryGetValue(i, out TerrainLayer l) && l != null
                    ? l
                    : CreatePlaceholderLayer(folder, mapName, i);
            }

            // Meme precaution que dans le cas 2 : une couche reconstituee mais
            // sans texture ferait vider le terrain par CreateConfig, et on
            // reviendrait au point de depart.
            EnsureLayersArePersisted(rebuilt, mapName);
            EnsureLayersHaveDiffuse(rebuilt, mapName);

            terrain.terrainData.terrainLayers = rebuilt;
            EditorUtility.SetDirty(terrain.terrainData);
            Debug.LogWarning($"[Terrain] {mapName} : terrain sans couche, {count} couche(s) reconstituee(s) "
                             + "depuis l'import.");
            return;
        }

        // CAS 2 : des cases mortes dans un tableau par ailleurs valide.
        //
        // ON REPARE SANS RIEN RETIRER : les splatmaps sont indexees sur la
        // POSITION des couches. Supprimer une case decalerait tous les canaux
        // au-dela du trou et detruirait la peinture des level designers.
        int broken = 0, repaired = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null)
            {
                continue;
            }

            broken++;
            if (imported.TryGetValue(i, out TerrainLayer replacement) && replacement != null)
            {
                repaired++;
            }
            else
            {
                replacement = CreatePlaceholderLayer(folder, mapName, i);
            }

            layers[i] = replacement;
        }

        // Repointer AVANT de doter : interroger la texture d'une couche morte
        // ne donnerait rien d'exploitable.
        int persisted = EnsureLayersArePersisted(layers, mapName);
        int textured = EnsureLayersHaveDiffuse(layers, mapName);

        if (broken == 0 && textured == 0 && persisted == 0)
        {
            return;
        }

        terrain.terrainData.terrainLayers = layers;
        EditorUtility.SetDirty(terrain.terrainData);

        if (broken > 0)
        {
            Debug.LogWarning($"[Terrain] {mapName} : {broken} couche(s) morte(s) reparee(s) en place "
                             + $"({repaired} depuis la couche d'import, {broken - repaired} recreee(s)). "
                             + "Les index sont preserves, la peinture est intacte.");
        }
    }

    /// Donne une texture aux couches qui n'en ont pas, AVANT de laisser
    /// MicroSplat travailler.
    ///
    /// LE VRAI POINT DE RUPTURE
    /// ConvertTerrains appelle TextureArrayConfigEditor.CreateConfig(terrain),
    /// qui REECRIT les couches du terrain a partir de leurs textures. Quand
    /// aucune couche n'a de texture, la config ressort vide et le terrain se
    /// retrouve sans couche - puis la ligne 171 accede a terrainLayers[0] sans
    /// condition et leve une NullReferenceException.
    ///
    /// C'est pour ca qu'un nettoyage en amont ne suffisait pas : le degat se
    /// produit A L'INTERIEUR de ConvertTerrains, entre le parametre (encore
    /// valide) et le tableau du terrain (deja vide).
    ///
    /// Cas typique : la colonne 15 et ses tuiles d'ocean, dont l'unique couche
    /// "Base" n'a jamais eu de texture dans le projet. C'est la meme cause que
    /// les calques gris - 63 regions concernees.
    ///
    /// On resout donc le nom L2 de la couche vers son pack PBR et on pose la
    /// BaseColor directement. La substitution qui suit fera le reste.
    private static int EnsureLayersHaveDiffuse(TerrainLayer[] layers, string mapName)
    {
        int textured = 0;

        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null || layers[i].diffuseTexture != null)
            {
                continue;
            }

            string l2Name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(layers[i]));
            int sep = l2Name.IndexOf($"_layer_{i}_", System.StringComparison.Ordinal);
            if (sep >= 0)
            {
                l2Name = l2Name.Substring(sep + $"_layer_{i}_".Length);
            }

            if (!L2TerrainGeneratorTextureMatcher.Instance.TryGetTextureMatch(mapName, l2Name, out string pack)
                || string.IsNullOrEmpty(pack))
            {
                continue;
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                TextureUtils.GetSplatTexturePath(pack) + "_BaseColor.jpg");

            if (tex != null)
            {
                layers[i].diffuseTexture = tex;
                EditorUtility.SetDirty(layers[i]);
                textured++;
            }
        }

        if (textured > 0)
        {
            Debug.Log($"[Terrain] {mapName} : {textured} couche(s) sans texture dotee(s) de leur pack PBR "
                      + "avant conversion (sans quoi MicroSplat vide le terrain de ses couches).");
        }

        return textured;
    }

    /// Garantit que chaque couche du terrain existe comme ASSET sur disque.
    ///
    /// LA CAUSE RACINE DES ECHECS EN LOT
    /// MicroSplat decide, couche par couche (MicroSplatTerrain.cs:379) :
    ///
    ///     if (cfg.sourceTextures[i].terrainLayer == null
    ///         || terrainLayer.diffuseTexture != cfg.sourceTextures[i].diffuse)
    ///            -> cree une couche NEUVE, dont l'asset n'est ecrit qu'au tick
    ///               suivant via EditorApplication.delayCall
    ///     else   -> reutilise la couche existante, deja persistee
    ///
    /// Le premier chemin assigne au terrain des TerrainLayer encore en memoire.
    /// Or le TerrainData est lui-meme un asset : il ne peut pas serialiser une
    /// reference vers un objet qui n'existe pas sur disque, et la reference
    /// retombe a null. MicroSplat relit alors terrainLayers[0] (ligne 171) et
    /// leve une NullReferenceException.
    ///
    /// La console l'a montre sans ambiguite le 2026-08-11 :
    ///
    ///     0 couche .terrainlayer preservee  ->  58 echecs, 0 reussite
    ///     >= 2 couches preservees           ->  10 reussites, 6 echecs
    ///
    /// Persister les couches en amont fait donc prendre a MicroSplat la branche
    /// "else", celle qui fonctionne, et supprime tout recours au differe.
    private static int EnsureLayersArePersisted(TerrainLayer[] layers, string mapName)
    {
        string folder = $"Assets/Resources/Data/Maps/{mapName}/TerrainData";
        int repointed = 0;

        // Les couches d'import, par index. Elles vivent dans TerrainData/ et non
        // dans MicroSplatData/ : le nettoyage d'avant substitution ne les touche
        // jamais, elles sont donc toujours la.
        var imported = new Dictionary<int, TerrainLayer>();
        foreach (string guid in AssetDatabase.FindAssets("t:TerrainLayer", new[] { folder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(p);
            string prefix = $"{mapName}_layer_";

            if (!file.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                continue;
            }

            string rest = file.Substring(prefix.Length);
            int sep = rest.IndexOf('_');
            if (sep > 0 && int.TryParse(rest.Substring(0, sep), out int idx))
            {
                imported[idx] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(p);
            }
        }

        for (int i = 0; i < layers.Length; i++)
        {
            // Une couche saine a un chemin d'asset qui existe encore. On ne se
            // fie PAS a une comparaison a null : Unity ne resout les references
            // mortes que paresseusement, souvent trop tard - c'est ce qui rendait
            // le nettoyage precedent aveugle, il ne voyait aucun trou.
            string path = layers[i] != null ? AssetDatabase.GetAssetPath(layers[i]) : null;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                continue;
            }

            if (!imported.TryGetValue(i, out TerrainLayer fallback) || fallback == null)
            {
                continue;
            }

            layers[i] = fallback;
            repointed++;
        }

        if (repointed > 0)
        {
            Debug.Log($"[Terrain] {mapName} : {repointed} couche(s) repointee(s) vers leur asset d'import "
                      + "(leur asset MicroSplat avait disparu, ce qui faisait creer une couche en differe "
                      + "que le terrain ne pouvait pas conserver).");
        }

        return repointed;
    }

    /// Couche neuve et vide, juste assez pour que MicroSplat lise tileSize et
    /// tileOffset sans planter. Sauvegardee en asset : une couche non persistee
    /// serait perdue au prochain rechargement et le probleme reviendrait.
    private static TerrainLayer CreatePlaceholderLayer(string folder, string mapName, int index)
    {
        var layer = new TerrainLayer { tileSize = new Vector2(UV_TILE_SIZE, UV_TILE_SIZE) };
        AssetDatabase.CreateAsset(layer, $"{folder}/{mapName}_layer_{index}_Recovered.asset");
        return layer;
    }

    /// Retrouve le nom de la texture L2 de chaque couche, par index.
    ///
    /// POURQUOI C'EST NECESSAIRE
    /// Certaines couches n'ont aucune texture dans la config MicroSplat, parce
    /// que la texture L2 correspondante n'existe pas dans le projet. Elles
    /// s'affichent alors en gris. Mesure du 2026-08-09 : 67 regions
    /// concernees, dont 63 par la seule texture "Base".
    ///
    /// Or la substitution se resout PAR NOM, et ce nom n'est pas perdu : l'etape
    /// d'import ecrit un fichier de couche qui le porte, sous la forme
    /// "{region}_layer_{index}_{nomL2}.asset". Exemple sur 22_21 :
    ///
    ///   22_21_layer_0_Base.asset      -> entree 0 = "Base"
    ///   22_21_layer_1_GUS05.asset     -> entree 1 = "GUS05"
    ///
    /// Les deux figurent dans la table de substitution. Sans ce rattrapage, ces
    /// couches restaient grises alors que tout etait disponible pour les
    /// resoudre.
    private static Dictionary<int, string> RecoverL2LayerNames(string mapName)
    {
        var byIndex = new Dictionary<int, string>();
        string folder = $"Assets/Resources/Data/Maps/{mapName}/TerrainData";

        if (!AssetDatabase.IsValidFolder(folder))
        {
            return byIndex;
        }

        string prefix = $"{mapName}_layer_";
        foreach (string guid in AssetDatabase.FindAssets("t:TerrainLayer", new[] { folder }))
        {
            string file = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
            if (!file.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                continue;
            }

            // "22_21_layer_10_GI_S3" -> index "10", nom "GI_S3". Le nom peut
            // lui-meme contenir des underscores, d'ou le decoupage en deux.
            string rest = file.Substring(prefix.Length);
            int sep = rest.IndexOf('_');
            if (sep <= 0 || !int.TryParse(rest.Substring(0, sep), out int index))
            {
                continue;
            }

            byIndex[index] = rest.Substring(sep + 1);
        }

        return byIndex;
    }

    private static void UpdateMicrosplatParams(MapGenerationData mapToGenerate)
    {
        Terrain terrain = ResolveTerrain(mapToGenerate.mapName);
        if (terrain == null)
        {
            return;
        }

        L2TerrainInfo terrainInfo = L2T3DInfoParser.LoadMetadata(mapToGenerate.mapName);

        MicroSplatTerrain mst = terrain.GetComponent<MicroSplatTerrain>();
        if (mst == null)
        {
            Debug.LogError($"[Microsplat] '{terrain.name}' n'a pas de MicroSplatTerrain. Lancez l'etape 05 avant la 06.");
            return;
        }

        for (int layer = 0; layer < terrainInfo.uvLayers.Count; layer++)
        {
            string texName = terrainInfo.uvLayers[layer].texture.name;
            if (L2TerrainGeneratorTextureMatcher.Instance.TryGetScaleMatch(
                    mapToGenerate.mapName, texName, out float scale))
            {
                mst.propData.SetValue(layer, PerTexVector2.SplatUVScale, new Vector2(scale, scale));
            }
            else
            {
                // Table de correspondance ecrite a la main : elle ne couvre que
                // les textures des regions deja importees. Une region neuve en
                // apporte forcement de nouvelles - ce n'est pas une erreur, on
                // applique une valeur par defaut et on signale quoi ajouter.
                mst.propData.SetValue(layer, PerTexVector2.SplatUVScale,
                    new Vector2(DefaultSplatUvScale, DefaultSplatUvScale));
                Debug.LogWarning($"[Microsplat] '{texName}' absente de scaleMatches, echelle par defaut {DefaultSplatUvScale} appliquee. " +
                                 $"Ajouter scaleMatches.Add(\"{texName}\", <valeur>); dans L2TerrainGeneratorTextureMatcher pour l'ajuster.");
            }
            if (L2TerrainGeneratorTextureMatcher.Instance.pertexFloatMatches.TryGetValue(texName, out List<PerTexFloatVal> ptv))
            {
                if (ptv != null)
                {
                    ptv.ForEach((ptvv) =>
                    {
                        mst.propData.SetValue(layer, ptvv.ptf, ptvv.value);
                    });
                }
            }
            else
            {
                // Pas de reglage colorimetrique connu : MicroSplat garde ses
                // valeurs par defaut, le rendu reste correct.
                Debug.LogWarning($"[Microsplat] '{texName}' absente de pertexFloatMatches, reglages colorimetriques par defaut.");
            }
            if (L2TerrainGeneratorTextureMatcher.Instance.pertextColorMatches.TryGetValue(texName, out List<PerTexColorVal> ptc))
            {
                if (ptc != null)
                {
                    ptc.ForEach((ptcv) =>
                    {
                        Debug.Log($"Setting propdata color per tex: {ptcv.ptf}:{ptcv.value}");
                        mst.propData.SetValue(layer, ptcv.ptf, ptcv.value);
                    });
                }
            }
            else
            {
                // Pas de teinte connue : MicroSplat garde le blanc neutre.
                Debug.LogWarning($"[Microsplat] '{texName}' absente de pertextColorMatches, teinte neutre conservee.");
            }
        }

        /*      
        public void SetValue(int textureIndex, PerTexFloat channel, float value)
        public void SetValue(int textureIndex, PerTexColor channel, Color value)
        public void SetValue(int textureIndex, PerTexVector2 channel, Vector2 value)*/

        // TextureArrayConfig.
        TextureArrayConfig cfg = Resources.Load<TextureArrayConfig>(Path.Combine("Data", "Maps", mapToGenerate.mapName, "TerrainData", "MicroSplatData", "MicroSplatConfig"));
        // if (TextureArrayConfigEditor.GetFromTerrain(cfg, terrain))
        if (cfg != null)
        {
            cfg.allTextureChannelHeight = TextureArrayConfig.AllTextureChannel.A;
            cfg.allTextureChannelAO = TextureArrayConfig.AllTextureChannel.A;
            cfg.pbrWorkflow = TextureArrayConfig.PBRWorkflow.Specular;

            ApplyArrayResolution(cfg);

            // BRILLANCE : canal VERT, pas alpha.
            //
            // Les cartes de brillance des packs Megascans sont des JPG en
            // niveaux de gris : la donnee est dans R=V=B, et l'alpha vaut 255
            // partout - un JPG n'a pas de couche alpha. Lire le canal A
            // revenait donc a ignorer la carte et a renvoyer une brillance de
            // 1.0 : un miroir, quelle que soit la texture posee.
            //
            // Le test du 2026-08-09 (20_20 sur G contre 18_19 sur A) n'avait rien
            // montre parce qu'AUCUNE des deux n'avait de carte a ce moment-la.
            // Depuis que les cartes sont posees, le canal redevient decisif.
            //
            // Hauteur et occlusion restent sur A : ce sont d'autres cartes, avec
            // leur propre encodage, et leur rendu actuel est correct.
            cfg.allTextureChannelSmoothness = TextureArrayConfig.AllTextureChannel.G;
            // Noms L2 rattrapes depuis les couches d'import, pour les entrees
            // dont la texture d'origine manque. Voir RecoverL2LayerNames.
            Dictionary<int, string> recovered = RecoverL2LayerNames(mapToGenerate.mapName);

            int noDiffuse = 0, recoveredCount = 0, index = -1;
            cfg.sourceTextures.ForEach((sourceTexture) =>
            {
                index++;

                // UNE ENTREE PEUT NE PORTER AUCUNE TEXTURE.
                //
                // Sans garde-fou, sourceTexture.diffuse.name levait une
                // NullReferenceException qui interrompait la boucle ENTIERE :
                // la region ressortait sans aucune substitution appliquee et
                // sans CompileConfig. Constate le 2026-08-09 sur le traitement
                // de masse - 12 regions en echec pour 1 reussie.
                //
                // Deux origines a ces entrees vides :
                //  - le remplissage que MicroSplat ajoute au-dela du nombre reel
                //    de couches (3 entrees par couche) ;
                //  - les couches dont la texture L2 n'existe pas dans le projet
                //    (typiquement "Base"), qui apparaissent en gris sur la carte.
                //
                // Le second cas est RATTRAPABLE : la table se resout par NOM, et
                // ce nom survit dans le fichier de couche produit a l'import
                // ("22_21_layer_0_Base"). On le recupere donc par l'index plutot
                // que d'abandonner la couche en gris.
                // LE NOM D'ORIGINE FAIT AUTORITE, PAS LA TEXTURE EN PLACE.
                //
                // Relire sourceTexture.diffuse.name rendait la substitution NON
                // IDEMPOTENTE : au second passage, le slot contient deja le
                // resultat du premier. La regle s'appliquait donc au nom du PACK
                // et non a celui de la texture L2.
                //
                // Constate sur 22_21 : "GIS03" avait ete remplace par
                // "Soil_Sand_pjErQ0_1K_BaseColor" ; au passage suivant, la regle
                // "sand" a matche ce nom et pose Thai_Beach_Sand. Cinq couches
                // de Giran se sont ainsi retrouvees en sable de plage, et le
                // dossier a accumule deux jeux de .terrainlayer.
                //
                // Les fichiers "{region}_layer_{index}_{nomL2}.asset" produits a
                // l'import conservent eux le nom d'origine et ne sont jamais
                // reecrits. Ils sont donc la source de verite : on les consulte
                // EN PRIORITE, et on ne retombe sur la texture en place que
                // s'ils manquent.
                string l2TextureName = null;

                if (recovered.TryGetValue(index, out string l2Name))
                {
                    l2TextureName = l2Name;
                    if (sourceTexture.diffuse == null)
                    {
                        recoveredCount++;
                    }
                }
                else if (sourceTexture.diffuse != null)
                {
                    l2TextureName = sourceTexture.diffuse.name;
                }

                if (l2TextureName == null)
                {
                    noDiffuse++;
                    return;
                }

                // // get the updated texture based on matching table
                if (L2TerrainGeneratorTextureMatcher.Instance.TryGetTextureMatch(
                        mapToGenerate.mapName, l2TextureName, out string splatTexture))
                {
                    string path = TextureUtils.GetSplatTexturePath(splatTexture);
                    string diffuse = path + "_BaseColor.jpg";
                    string ao = path + "_AO.jpg";
                    string bump = path + "_Bump.jpg";
                    string normal = path + "_Normal.jpg";
                    string roughness = path + "_Roughness.jpg";
                    string gloss = path + "_Gloss.jpg";
                    string specular = path + "_Specular.jpg";

                    Texture2D diffuseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(diffuse);
                    Texture2D aoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ao);
                    Texture2D bumpTex = AssetDatabase.LoadAssetAtPath<Texture2D>(bump);
                    Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
                    Texture2D glossTex = AssetDatabase.LoadAssetAtPath<Texture2D>(gloss);
                    Texture2D roughnessTex = AssetDatabase.LoadAssetAtPath<Texture2D>(roughness);
                    Texture2D specularTex = AssetDatabase.LoadAssetAtPath<Texture2D>(specular);

                    if (diffuseTex != null)
                        sourceTexture.diffuse = diffuseTex;
                    if (aoTex != null)
                        sourceTexture.ao = aoTex;
                    if (bumpTex != null)
                        sourceTexture.height = bumpTex;
                    if (normalTex != null)
                        sourceTexture.normal = normalTex;
                    if (specularTex != null)
                        sourceTexture.specular = specularTex;

                    // BRILLANCE : ne PAS poser _Roughness.jpg tel quel.
                    //
                    // C'etait la cause du terrain miroitant. La rugosite est
                    // l'INVERSE de la brillance : une surface tres rugueuse
                    // (0.9) etait lue comme tres brillante (0.9). MicroSplat
                    // sait inverser, mais seulement si on lui dit - c'est le
                    // role du drapeau isRoughness, qui restait a false.
                    //
                    // Les packs Megascans fournissent les DEUX cartes. _Gloss
                    // est deja de la brillance : aucune inversion, aucun risque.
                    // On ne retombe sur _Roughness que si _Gloss manque, et on
                    // pose alors le drapeau.
                    if (glossTex != null)
                    {
                        sourceTexture.smoothness = glossTex;
                        sourceTexture.isRoughness = false;
                    }
                    else if (roughnessTex != null)
                    {
                        sourceTexture.smoothness = roughnessTex;
                        sourceTexture.isRoughness = true;
                    }
                }
            });

            if (recoveredCount > 0)
            {
                Debug.Log($"[Microsplat] {mapToGenerate.mapName} : {recoveredCount} couche(s) sans texture "
                          + "rattrapee(s) via leur nom L2 d'origine.");
            }

            if (noDiffuse > 0)
            {
                Debug.LogWarning($"[Microsplat] {mapToGenerate.mapName} : {noDiffuse}/{cfg.sourceTextures.Count} "
                                 + "entree(s) sans texture ni nom recuperable, ignoree(s). Celles situees dans "
                                 + "les premieres positions apparaitront en gris sur le terrain.");
            }

            TextureArrayConfigEditor.CompileConfig(cfg);
        }
        else
        {
            Debug.LogError("Cant open TextureArrayConfig for map " + mapToGenerate.mapName);
        }
        mst.Sync();
    }
}
#endif
