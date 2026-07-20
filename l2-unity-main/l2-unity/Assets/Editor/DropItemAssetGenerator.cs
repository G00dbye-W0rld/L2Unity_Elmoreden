#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Genere les materiaux (a partir des .props.txt + textures extraites via umodel)
// et les prefabs (un par mesh FBX) pour les objets au sol ("dropitems"), en
// suivant le meme genre de pipeline que L2MaterialBuilder/OrcShamanPrefabGenerator.
// Voir le plan de drop/pickup pour le contexte complet.
public class DropItemAssetGenerator
{
    const string MeshDir = "Assets/Resources/Data/Animations/DropItems";
    const string TexDir = "Assets/Resources/Data/Animations/DropItems/Textures";
    const string MaterialOutDir = "Assets/Resources/Data/Animations/DropItems/Materials";
    const string PrefabOutDir = "Assets/Resources/Prefabs/World/DropItems";

    // ---------- ETAPE 1 : materiaux (depuis les .props.txt + textures) ----------

    [MenuItem("Tools/L2Unity/Items/1. Generate Drop Item Materials")]
    static void GenerateMaterials()
    {
        EnsureFolder(MaterialOutDir);

        string[] propsGuids = AssetDatabase.FindAssets("t:TextAsset", new[] { TexDir });
        int created = 0, skipped = 0;

        foreach (string guid in propsGuids)
        {
            string propsPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!propsPath.EndsWith(".props.txt")) continue;

            string baseName = Path.GetFileName(propsPath).Replace(".props.txt", "");
            string materialPath = $"{MaterialOutDir}/{baseName}.mat";

            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) != null)
            {
                skipped++;
                continue;
            }

            ParsedProps parsed = ParseProps(propsPath);
            Texture2D texture = parsed.TextureName != null
                ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{parsed.TextureName}.png")
                : null;

            Material material = parsed.IsTransparent
                ? BuildTransparentMaterial(parsed.IsDoubleFace)
                : BuildLitMaterial(parsed.IsDoubleFace);

            if (texture != null)
            {
                // material.mainTexture ne mappe pas toujours de facon fiable
                // sur _BaseMap du shader URP Lit (confirme visuellement : le
                // slot "Base Map" restait vide dans l'Inspector malgre une
                // texture assignee). Assignation explicite du nom de propriete
                // reel du shader.
                material.SetTexture("_BaseMap", texture);
            }
            else
            {
                Debug.LogWarning($"[DropItemAssetGenerator] Texture introuvable pour {baseName} (attendu: {parsed.TextureName}).");
            }

            AssetDatabase.CreateAsset(material, materialPath);
            created++;
        }

        // Repli : certaines textures (ex. coin_t00/coin_t01 pour l'adena) n'ont
        // aucun .props.txt correspondant dans l'export umodel (uniquement le
        // PNG) - sans ce repli elles n'auraient jamais de materiau du tout.
        // On leur construit un materiau Lit basique directement depuis le PNG.
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir });
        int fallbackCreated = 0;

        foreach (string guid in textureGuids)
        {
            string texPath = AssetDatabase.GUIDToAssetPath(guid);
            string baseName = Path.GetFileNameWithoutExtension(texPath);
            string materialPath = $"{MaterialOutDir}/{baseName}.mat";

            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) != null)
            {
                continue;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (texture == null) continue;

            Material material = BuildLitMaterial(false);
            material.SetTexture("_BaseMap", texture);
            AssetDatabase.CreateAsset(material, materialPath);
            fallbackCreated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[DropItemAssetGenerator] Materiaux: {created} crees (.props.txt), {fallbackCreated} crees (repli PNG seul), {skipped} deja presents.");
    }

    class ParsedProps
    {
        public string TextureName;
        public bool IsTransparent;
        public bool IsDoubleFace;
    }

    static ParsedProps ParseProps(string propsPath)
    {
        ParsedProps result = new ParsedProps();

        foreach (string rawLine in File.ReadAllLines(propsPath))
        {
            string line = rawLine.Trim();
            int eq = line.IndexOf('=');
            if (eq < 0) continue;

            string key = line.Substring(0, eq).Trim();
            string value = line.Substring(eq + 1).Trim();

            if ((key == "Diffuse" || key == "SelfIllumination") && value.StartsWith("Texture") && result.TextureName == null)
            {
                int quote = value.IndexOf('\'');
                if (quote >= 0)
                {
                    string texRef = value.Substring(quote + 1).TrimEnd('\'');
                    string[] parts = texRef.Split('.');
                    result.TextureName = parts[parts.Length - 1];
                }
            }
            else if (key == "Opacity" && value.StartsWith("Texture"))
            {
                result.IsTransparent = true;
            }
            else if (key == "AlphaTest" && value == "true")
            {
                result.IsTransparent = true;
            }
            else if (key == "TwoSided")
            {
                result.IsDoubleFace = value == "true";
            }
            else if (key == "OutputBlending" && value.StartsWith("OB_Masked"))
            {
                result.IsTransparent = true;
            }
        }

        return result;
    }

    static Material BuildLitMaterial(bool isDoubleFace)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetFloat("_Cull", isDoubleFace ? 0f : 2f);
        return material;
    }

    static Material BuildTransparentMaterial(bool isDoubleFace)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", isDoubleFace ? 0f : 2f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return material;
    }

    // ---------- ETAPE 2 : prefabs (un par mesh FBX importe) ----------

    [MenuItem("Tools/L2Unity/Items/2. Generate Drop Item Prefabs")]
    static void GeneratePrefabs()
    {
        EnsureFolder("Assets/Resources/Prefabs");
        EnsureFolder("Assets/Resources/Prefabs/World");
        EnsureFolder(PrefabOutDir);

        string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { MeshDir });
        int created = 0, skipped = 0, failed = 0;

        foreach (string guid in fbxGuids)
        {
            string fbxPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!fbxPath.ToLower().EndsWith(".fbx")) continue;

            string meshName = Path.GetFileNameWithoutExtension(fbxPath);
            string prefabPath = $"{PrefabOutDir}/{meshName}.prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                skipped++;
                continue;
            }

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset == null)
            {
                Debug.LogWarning($"[DropItemAssetGenerator] Impossible de charger {fbxPath}");
                failed++;
                continue;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            instance.name = meshName;
            instance.tag = "Pickup";

            // Correction d'axe uniquement (Z-up PSK/Unreal -> Y-up Unity),
            // meme convention que OrcShamanPrefabGenerator. PAS de x100 ici :
            // ce facteur etait calibre pour l'echelle intrinseque des
            // personnages (essaye puis retire - donnait un collider de rayon
            // ~4 unites pour une piece de monnaie, bien trop grand). L'ajustement
            // fin d'echelle se fait a l'execution via ItemSpawner.WorldScale,
            // reglable sans avoir a regenerer les prefabs.
            instance.transform.localRotation = new Quaternion(-0.7071068f, 0f, 0f, 0.7071067f);
            instance.transform.localScale = Vector3.one;

            Renderer renderer = instance.GetComponentInChildren<Renderer>();
            Bounds bounds = renderer != null
                ? renderer.bounds
                : new Bounds(instance.transform.position, Vector3.one * 0.5f);

            SphereCollider collider = instance.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.center = instance.transform.InverseTransformPoint(bounds.center);
            collider.radius = Mathf.Max(bounds.extents.magnitude, 0.2f);

            instance.AddComponent<WorldItem>();

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[DropItemAssetGenerator] Prefabs: {created} crees, {skipped} deja presents, {failed} echecs.");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
