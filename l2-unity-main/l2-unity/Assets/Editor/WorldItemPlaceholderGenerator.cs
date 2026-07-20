using UnityEditor;
using UnityEngine;

// Genere le prefab placeholder utilise pour representer un objet au sol
// (WorldItem) en attendant l'integration des vrais meshes "dropitems.*"
// extraits du client Interlude via umodel (voir plan de drop/pickup).
public class WorldItemPlaceholderGenerator
{
    const string OutPath = "Assets/Resources/Prefabs/World/WorldItemPlaceholder.prefab";

    [MenuItem("Tools/L2Unity/Items/Generate World Item Placeholder")]
    static void Generate()
    {
        string dir = "Assets/Resources/Prefabs/World";
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder(dir))
        {
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "World");
        }

        GameObject root = new GameObject("WorldItemPlaceholder");
        root.tag = "Pickup";

        SphereCollider collider = root.AddComponent<SphereCollider>();
        collider.radius = 0.3f;
        collider.isTrigger = true;

        GameObject icon = GameObject.CreatePrimitive(PrimitiveType.Quad);
        icon.name = "Icon";
        Object.DestroyImmediate(icon.GetComponent<Collider>());
        icon.transform.SetParent(root.transform, false);
        icon.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        icon.transform.localScale = Vector3.one * 0.5f;

        Material material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        material.SetFloat("_Surface", 1f); // Transparent
        material.SetFloat("_Blend", 2f); // Alpha
        material.SetFloat("_BlendOp", 0f);
        material.SetFloat("_ColorMode", 0f);
        material.SetFloat("_DstBlend", 1f);
        material.SetFloat("_DstBlendAlpha", 1f);
        material.SetFloat("_Cull", 0f); // Off - visible des deux cotes

        MeshRenderer iconRenderer = icon.GetComponent<MeshRenderer>();
        iconRenderer.sharedMaterial = material;
        iconRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        WorldItem worldItem = root.AddComponent<WorldItem>();
        SerializedObject so = new SerializedObject(worldItem);
        so.FindProperty("_iconBillboard").objectReferenceValue = icon.transform;
        so.FindProperty("_iconRenderer").objectReferenceValue = iconRenderer;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, OutPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[WorldItemPlaceholderGenerator] Prefab genere: {OutPath}");
    }
}
