#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Finalise ce que Blender/OrfenItemImport.py a converti : prefabs d'armes et de pieces
// d'armure, materiaux d'armure (nommes comme dans Armorgrp) et reglages des icones.
public static class OrfenItemImporter
{
    private const string AnimationsRoot = "Assets/Resources/Data/Animations";
    private const string SysTexturesRoot = "Assets/Resources/Data/SysTextures";
    private const string IconReference = SysTexturesRoot + "/Icon/accessary_apprentices_earing_i00.png";
    private const string PlaceholderMaterialGuid = "78187e586541d9d40907b180586974f7";
    private const float WeaponScale = 0.019f / 100f;

    [Serializable]
    private class Manifest
    {
        public string kind;
        public string mesh;
        public string package;
        public List<MaterialInfo> materials = new List<MaterialInfo>();
    }

    [Serializable]
    private class MaterialInfo
    {
        public string name;
        public string slot;
        public string texture;
        public string material;
        public bool alphaTest;
        public int alphaRef;
        public bool twoSided;
    }

    [MenuItem("L2/Objets/Importer les objets Orfen convertis")]
    public static void ImportAll()
    {
        int weapons = 0, armors = 0, materials = 0, icons = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            icons = FixIcons();
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        materials = BuildArmorMaterials();

        string[] manifests = Directory.GetFiles(AnimationsRoot, "*.import.json", SearchOption.AllDirectories);
        try
        {
            for (int i = 0; i < manifests.Length; i++)
            {
                string path = manifests[i].Replace('\\', '/');
                EditorUtility.DisplayProgressBar("Import des objets Orfen", path, (float)i / manifests.Length);
                try
                {
                    Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
                    if (manifest.kind == "weapon")
                    {
                        ImportWeapon(path, manifest);
                        weapons++;
                    }
                    else if (manifest.kind == "armor")
                    {
                        ImportArmorPiece(path, manifest);
                        armors++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Import objets] {path} : {e}");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[Import objets] {weapons} arme(s), {armors} piece(s) d'armure, {materials} materiau(x) d'armure, {icons} icone(s) reglee(s).");
    }

    // Reapplique detourage, double face et texture a tous les materiaux importes, sans toucher aux meshes.
    [MenuItem("L2/Objets/Rafraichir les materiaux importes")]
    public static void RefreshMaterials()
    {
        int count = BuildArmorMaterials();
        string[] manifests = Directory.GetFiles(AnimationsRoot, "*.import.json", SearchOption.AllDirectories);
        try
        {
            for (int i = 0; i < manifests.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Materiaux importes", manifests[i], (float)i / manifests.Length);
                Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifests[i]));
                foreach (MaterialInfo info in manifest.materials)
                {
                    if (string.IsNullOrEmpty(info.texture))
                    {
                        continue;
                    }

                    // Armes : nom de la texture Weapongrp ; PNJ et monstres : nom de l'emplacement du mesh.
                    string name = manifest.kind == "weapon" ? info.name : info.slot;
                    string folder = $"Assets/Resources/{Path.GetDirectoryName(info.texture).Replace('\\', '/')}/Materials";
                    NpcModelImporter.CreateMaterial($"{folder}/{name}.mat", info.texture, info.alphaTest, info.alphaRef, info.twoSided);
                    count++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[Import objets] {count} materiau(x) rafraichi(s).");
    }

    [MenuItem("L2/Objets/Recaler l'echelle des armes importees")]
    public static void FixWeaponScale()
    {
        int count = 0;
        foreach (string manifestPath in Directory.GetFiles(AnimationsRoot, "*.import.json", SearchOption.AllDirectories))
        {
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            string fbxPath = $"{Path.GetDirectoryName(manifestPath).Replace('\\', '/')}/{manifest.mesh}.fbx";
            var importer = manifest.kind == "weapon" ? UnityEditor.AssetImporter.GetAtPath(fbxPath) as ModelImporter : null;
            if (importer != null && !Mathf.Approximately(importer.globalScale, WeaponScale))
            {
                importer.globalScale = WeaponScale;
                importer.SaveAndReimport();
                count++;
            }
        }

        Debug.Log($"[Import objets] Echelle recalee pour {count} arme(s).");
    }

    // Memes reglages que les icones deja presentes (sprite, sans mipmap, filtrage point).
    private static int FixIcons()
    {
        var reference = (TextureImporter)UnityEditor.AssetImporter.GetAtPath(IconReference);
        if (reference == null)
        {
            return 0;
        }

        var settings = new TextureImporterSettings();
        reference.ReadTextureSettings(settings);
        int count = 0;
        foreach (string file in Directory.GetFiles(SysTexturesRoot + "/Icon", "*.png"))
        {
            var importer = (TextureImporter)UnityEditor.AssetImporter.GetAtPath(file.Replace('\\', '/'));
            if (importer == null || importer.textureType == reference.textureType)
            {
                continue;
            }

            importer.SetTextureSettings(settings);
            importer.textureCompression = reference.textureCompression;
            importer.SaveAndReimport();
            count++;
        }

        return count;
    }

    private static int BuildArmorMaterials()
    {
        string path = SysTexturesRoot + "/armor_materials.import.json";
        if (!File.Exists(path))
        {
            return 0;
        }

        Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
        int count = 0;
        for (int i = 0; i < manifest.materials.Count; i++)
        {
            MaterialInfo info = manifest.materials[i];
            if (string.IsNullOrEmpty(info.texture))
            {
                continue;
            }

            EditorUtility.DisplayProgressBar("Materiaux d'armure", info.material, (float)i / manifest.materials.Count);
            NpcModelImporter.CreateMaterial($"Assets/Resources/{info.material}.mat", info.texture, info.alphaTest, info.alphaRef, info.twoSided);
            count++;
        }

        EditorUtility.ClearProgressBar();
        return count;
    }

    // Arme : variante du FBX avec ses materiaux, comme les prefabs d'armes existants.
    private static void ImportWeapon(string manifestPath, Manifest manifest)
    {
        string modelDir = Path.GetDirectoryName(manifestPath).Replace('\\', '/');
        string fbxPath = $"{modelDir}/{manifest.mesh}.fbx";
        string prefabPath = $"{AnimationsRoot}/{manifest.package}/{manifest.mesh}.prefab";
        NpcModelImporter.ConfigureModel(fbxPath);

        // Les armes d'origine sont des FBX en centimetres (UnitScaleFactor 100), les notres en metres :
        // Unity convertit les unites, il faut donc diviser l'echelle par 100 pour garder la meme taille.
        var importer = (ModelImporter)UnityEditor.AssetImporter.GetAtPath(fbxPath);
        importer.globalScale = WeaponScale;
        importer.SaveAndReimport();

        var materials = new List<Material>();
        foreach (MaterialInfo info in manifest.materials)
        {
            if (string.IsNullOrEmpty(info.texture))
            {
                materials.Add(null);
                continue;
            }

            string folder = $"Assets/Resources/{Path.GetDirectoryName(info.texture).Replace('\\', '/')}/Materials";
            materials.Add(NpcModelImporter.CreateMaterial($"{folder}/{info.name}.mat", info.texture, info.alphaTest, info.alphaRef, info.twoSided));
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath));
        try
        {
            instance.name = manifest.mesh;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length && i < materials.Count; i++)
                {
                    if (materials[i] != null)
                    {
                        shared[i] = materials[i];
                    }
                }

                renderer.sharedMaterials = shared;
            }

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    // Piece d'armure : meme construction que OrcShamanPrefabGenerator (etape 1), les os
    // sont retrouves par nom a l'equipement grace a PieceBoneNames.
    private static void ImportArmorPiece(string manifestPath, Manifest manifest)
    {
        string modelDir = Path.GetDirectoryName(manifestPath).Replace('\\', '/');
        string fbxPath = $"{modelDir}/{manifest.mesh}.fbx";
        string prefabPath = $"{AnimationsRoot}/{manifest.package}/{manifest.mesh}.prefab";
        NpcModelImporter.ConfigureModel(fbxPath);

        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        SkinnedMeshRenderer source = fbx.GetComponentInChildren<SkinnedMeshRenderer>();
        Mesh mesh = source != null ? source.sharedMesh : fbx.GetComponentInChildren<MeshFilter>()?.sharedMesh;
        if (mesh == null)
        {
            Debug.LogWarning($"[Import objets] Aucun mesh dans {fbxPath}");
            return;
        }

        var piece = new GameObject(manifest.mesh);
        try
        {
            piece.transform.localRotation = new Quaternion(-0.7071068f, 0f, 0f, 0.7071067f);
            piece.transform.localScale = new Vector3(100f, 100f, 100f);

            SkinnedMeshRenderer renderer = piece.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = new Transform[mesh.bindposes.Length];
            Material placeholder = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(PlaceholderMaterialGuid));
            if (placeholder != null)
            {
                renderer.sharedMaterial = placeholder;
            }

            if (source != null && source.bones != null && source.bones.Length == mesh.bindposes.Length)
            {
                PieceBoneNames names = piece.AddComponent<PieceBoneNames>();
                names.boneNames = new string[source.bones.Length];
                for (int i = 0; i < source.bones.Length; i++)
                {
                    names.boneNames[i] = source.bones[i] != null ? source.bones[i].name : null;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(piece, prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(piece);
        }
    }
}
#endif
