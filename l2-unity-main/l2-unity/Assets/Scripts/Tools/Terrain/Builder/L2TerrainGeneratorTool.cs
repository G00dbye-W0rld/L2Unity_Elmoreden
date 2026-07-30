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
    // Echelle appliquee aux textures absentes de scaleMatches. 3 correspond
    // a la valeur de la couche "Base", neutre et deja utilisee par toutes les
    // regions existantes.
    private const float DefaultSplatUvScale = 3f;

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
    [MenuItem("Shnok/00. [Scene] Nettoyer les objets generes")]
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

    [MenuItem("Shnok/04. [Terrain] Generate terrain")]
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


    [MenuItem("Shnok/05. [Terrain] Convert terrain to microsplat")]
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


    [MenuItem("Shnok/06. [Terrain] Update microsplat params")]
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

    [MenuItem("Shnok/03. [StaticMeshes] Generate staticmeshes")]
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

    [MenuItem("Shnok/11. [Terrain] Stitch terrain seams")]
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

        MicroSplatTerrainEditor.ConvertTerrains(new Terrain[] { terrain }, terrain.terrainData.terrainLayers);
        mst.Sync();
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
            if (L2TerrainGeneratorTextureMatcher.Instance.scaleMatches.TryGetValue(texName, out float scale))
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
                        Debug.Log($"Setting propdata float per tex: {ptvv.ptf}:{ptvv.value}");
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
            cfg.allTextureChannelSmoothness = TextureArrayConfig.AllTextureChannel.A;
            cfg.allTextureChannelAO = TextureArrayConfig.AllTextureChannel.A;
            cfg.pbrWorkflow = TextureArrayConfig.PBRWorkflow.Specular;
            cfg.sourceTextures.ForEach((sourceTexture) =>
            {
                //TODO update source textures
                // string[] folderTexture = L2MetaDataUtils.GetFolderAndFileFromInfo(info);

                // // get the updated texture based on matching table
                if (L2TerrainGeneratorTextureMatcher.Instance.textureMatches.TryGetValue(sourceTexture.diffuse.name, out string splatTexture))
                {
                    string path = TextureUtils.GetSplatTexturePath(splatTexture);
                    string diffuse = path + "_BaseColor.jpg";
                    string ao = path + "_AO.jpg";
                    string bump = path + "_Bump.jpg";
                    string normal = path + "_Normal.jpg";
                    string roughness = path + "_Roughness.jpg";
                    string specular = path + "_Specular.jpg";

                    Texture2D diffuseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(diffuse);
                    Texture2D aoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ao);
                    Texture2D bumpTex = AssetDatabase.LoadAssetAtPath<Texture2D>(bump);
                    Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
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
                    if (roughnessTex != null)
                        sourceTexture.smoothness = roughnessTex;
                    if (specularTex != null)
                        sourceTexture.specular = specularTex;
                }
            });

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
