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

    private static readonly string[] ReferenceRegions = { "16_24", "16_25", "17_24", "17_25" };
    private static readonly string[] TestRegions = { "17_22", "17_23", "18_22", "18_23" };

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

    /// Taille de tuile des couches partagees, en unites monde.
    ///
    /// L'echelle reelle est portee par le propdata de chaque region (_UVScale
    /// par texture) ; cette valeur ne sert que d'ancrage pour l'apercu editeur.
    private const float RegionTileSize = 624.1524f / 256f;

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

        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();

        JBooth.MicroSplat.TextureArrayConfigEditor.CompileConfig(cfg);

        Debug.Log($"[Mutualisation] Config partagee : {cfg.sourceTextures.Count} textures compilees "
                  + $"dans {SharedFolder}. Assignez-la maintenant a un terrain via l'inspecteur "
                  + "MicroSplat pour produire le materiau maitre.");
    }

    // ================================================================
    //  ETAPE 3 - reindexation
    // ================================================================

    [MenuItem("L2/Terrain/Mutualisation/4. Reindexer les regions TEST", false, 184)]
    public static void ReindexTestRegions()
    {
        Reindex(TestRegions, "regions test");
    }

    [MenuItem("L2/Terrain/Mutualisation/5. Reindexer TOUTES les regions", false, 185)]
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

        for (int i = 0; i < regions.Length; i++)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Reindexation des splatmaps",
                    $"{regions[i]} ({i + 1}/{regions.Length})", (float)i / regions.Length))
            {
                break;
            }

            try
            {
                if (ReindexOne(regions[i], packOf, indexOf, sharedLayers)) { done++; } else { failed++; }
            }
            catch (Exception e)
            {
                failed++;
                Debug.LogError($"[Mutualisation] {regions[i]} : {e.Message}");
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();

        Debug.Log($"[Mutualisation] {label} : {done} region(s) reindexee(s), {failed} echec(s).");
    }

    private static bool ReindexOne(string mapName, Dictionary<string, string> packOf,
                                   Dictionary<string, int> indexOf, TerrainLayer[] sharedLayers)
    {
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

        // L'ordre compte : changer les couches redimensionne les splatmaps,
        // il faut donc ecrire les poids APRES.
        data.terrainLayers = sharedLayers.Take(newLayers).ToArray();
        data.SetAlphamaps(0, 0, fresh);

        EditorUtility.SetDirty(data);

        Debug.Log($"[Mutualisation] {mapName} : {oldLayers} -> {newLayers} couches, "
                  + $"poids conserve a {(1 - loss):P2}.");

        return true;
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

    /// Recalcule l'ordre partage. Doit produire EXACTEMENT le meme resultat
    /// que l'analyse : c'est lui qui definit ce que signifie chaque tranche.
    private static List<string> ResolveSharedOrder()
    {
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
