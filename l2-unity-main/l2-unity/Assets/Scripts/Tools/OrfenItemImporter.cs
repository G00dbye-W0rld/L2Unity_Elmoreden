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

    [MenuItem("L2/Objets/Reimporter les armes converties")]
    public static void ImportWeapons()
    {
        int count = 0;
        string[] manifests = Directory.GetFiles(AnimationsRoot, "*.import.json", SearchOption.AllDirectories);
        try
        {
            for (int i = 0; i < manifests.Length; i++)
            {
                string path = manifests[i].Replace('\\', '/');
                Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
                if (manifest.kind != "weapon")
                {
                    continue;
                }

                EditorUtility.DisplayProgressBar("Armes", path, (float)i / manifests.Length);
                try
                {
                    ImportWeapon(path, manifest);
                    count++;
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

        Debug.Log($"[Import objets] {count} arme(s) reimportee(s).");
    }

    // Couvre aussi les boucliers d'origine du projet, qui n'ont pas de manifeste d'import.
    [MenuItem("L2/Objets/Reorienter les boucliers")]
    public static void ReorientShields()
    {
        int count = 0;
        foreach (string path in Directory.GetFiles(AnimationsRoot, "*_sh.prefab", SearchOption.AllDirectories))
        {
            string prefabPath = path.Replace('\\', '/');
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            if (instance == null)
            {
                continue;
            }

            try
            {
                Renderer renderer = instance.GetComponentInChildren<Renderer>();
                Mesh mesh = renderer is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh
                    : instance.GetComponentInChildren<MeshFilter>()?.sharedMesh;
                if (renderer != null && mesh != null)
                {
                    OrientShield(instance, renderer.transform, mesh);
                    PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                    count++;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Import objets] {count} bouclier(s) reoriente(s).");
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

    // Utile apres avoir depose une icone a la main : ne touche que celles mal reglees.
    [MenuItem("L2/Objets/Regler les icones importees")]
    public static void FixIconsMenu()
    {
        Debug.Log($"[Import objets] {FixIcons()} icone(s) reglee(s).");
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
            OrientWeapon(instance, IsShield(manifest.mesh));
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

    // Les modeles d'Orfen n'ont pas tous le meme sens. On les remet d'apres leur forme :
    // la longueur le long de X (la lame), la plus fine dimension le long de Z (le tranchant).
    private static void OrientWeapon(GameObject instance, bool shield)
    {
        Renderer renderer = instance.GetComponentInChildren<Renderer>();
        Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : instance.GetComponentInChildren<MeshFilter>()?.sharedMesh;
        if (mesh == null)
        {
            return;
        }

        Transform model = renderer.transform;
        if (shield)
        {
            OrientShield(instance, model, mesh);
            return;
        }

        Vector3 size = mesh.bounds.size;
        int thin = size.x <= size.y && size.x <= size.z ? 0 : (size.y <= size.z ? 1 : 2);

        // X est la longueur d'origine de tous ces modeles. On ne cherche la plus grande
        // dimension que si X est justement la plus fine : sur un bouclier rond, les deux
        // autres cotes sont a un millimetre pres et le choix basculerait d'un modele a l'autre.
        int main = thin != 0 ? 0 : (size.y >= size.z ? 1 : 2);

        // Axes du mesh exprimes dans le repere du prefab (le FBX porte deja sa propre rotation).
        Vector3 meshThin = ToPrefab(instance, model, Axis(thin));
        Vector3 meshMain = ToPrefab(instance, model, Axis(main));

        instance.transform.localRotation =
            Quaternion.LookRotation(Vector3.forward, Vector3.right) * Quaternion.Inverse(Quaternion.LookRotation(meshThin, meshMain));
    }

    // Sur l'os de bouclier, l'axe qui descend le long du bras est Z, pas X : mesure faite en
    // rejouant les poses wait_1HS, atkWait_1HS et run_1HS sur le squelette du joueur. Le modele
    // porte sa hauteur sur X et sa face bombee sur Y, d'ou le quart de tour. On annule d'abord
    // la rotation que le FBX transporte, pour ne pas dependre de la facon dont il a ete exporte.
    private static void OrientShield(GameObject instance, Transform model, Mesh mesh)
    {
        Quaternion chain = Quaternion.Inverse(instance.transform.rotation) * model.rotation;
        // Le quart de tour sur Z met la hauteur du bouclier le long du bras ; celui sur X la
        // fait pivoter autour de cette hauteur pour que la face regarde devant et non sur le
        // cote. L'axe a ete etabli par elimination : un demi-tour sur Z retournait le
        // bouclier, un quart sur Y le couchait, donc sa hauteur suit bien X.
        Quaternion rotation =
            Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 0f, 90f) * Quaternion.Inverse(chain);

        // Demi-tour autour de la largeur du bouclier, l'axe median du maillage : il remet la
        // pointe en bas et la face bombee vers l'avant, qui etaient inverses tous les deux.
        Vector3 size = mesh.bounds.size;
        int thin = size.x <= size.y && size.x <= size.z ? 0 : (size.y <= size.z ? 1 : 2);
        int longest = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
        if (thin != longest)
        {
            Vector3 width = ToPrefab(instance, model, Axis(3 - thin - longest));
            rotation *= Quaternion.AngleAxis(180f, width);

            // Quart de tour autour de la hauteur : le bouclier se porte de profil, sa face
            // tournee vers l'exterieur, pas vers l'avant du personnage.
            Vector3 height = ToPrefab(instance, model, Axis(longest));
            rotation *= Quaternion.AngleAxis(90f, height);
        }

        instance.transform.localRotation = rotation;
    }

    private static Vector3 ToPrefab(GameObject instance, Transform model, Vector3 axis)
    {
        return instance.transform.InverseTransformDirection(model.TransformDirection(axis));
    }

    // Convention du client : les boucliers se terminent par _sh, les armes par _wp.
    private static bool IsShield(string mesh)
    {
        return mesh != null && mesh.EndsWith("_sh", System.StringComparison.OrdinalIgnoreCase);
    }

    private static Vector3 Axis(int index)
    {
        return index == 0 ? Vector3.right : (index == 1 ? Vector3.up : Vector3.forward);
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
