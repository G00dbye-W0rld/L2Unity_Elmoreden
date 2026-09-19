#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

// Transforme les FBX convertis par Blender/OrfenNpcImport.py en prefabs de PNJ et de monstres,
// et remplit les conteneurs d'animation d'apres le nom des clips.
public static class NpcModelImporter
{
    private const string AnimationsRoot = "Assets/Resources/Data/Animations";
    private const string TemplateRoot = AnimationsRoot + "/_Template";
    private const string NpcTemplate = AnimationsRoot + "/LineageNPCs/a_traderB_MHuman_m00/a_traderB_MHuman_m00.prefab";
    private const string MonsterTemplate = AnimationsRoot + "/LineageMonsters/wolf_m00/wolf_m00.prefab";
    private const string MaterialTemplate = "Assets/Resources/Data/SysTextures/LineageNpcsTex/Materials/a_traderB_MHuman_m00_t00_b00.mat";
    private const float ModelScale = 0.019f;

    [Serializable]
    private class Manifest
    {
        public string mesh;
        public string package;
        public string animation;
        public string kind;
        public List<MaterialInfo> materials = new List<MaterialInfo>();
    }

    [Serializable]
    private class MaterialInfo
    {
        public string slot;
        public string texture;
        public bool alphaTest;
        public int alphaRef;
        public bool twoSided;
    }

    [MenuItem("L2/PNJ/Importer les modeles Orfen convertis")]
    public static void ImportAll()
    {
        string[] manifests = Directory.GetFiles(AnimationsRoot, "*.import.json", SearchOption.AllDirectories);
        int done = 0;
        try
        {
            for (int i = 0; i < manifests.Length; i++)
            {
                string path = manifests[i].Replace('\\', '/');
                EditorUtility.DisplayProgressBar("Import des PNJ Orfen", path, (float)i / manifests.Length);
                try
                {
                    ImportModel(path);
                    done++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Import PNJ] {path} : {e}");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[Import PNJ] {done}/{manifests.Length} modele(s) importe(s).");
    }

    [MenuItem("L2/PNJ/Reconstruire les conteneurs d'animation existants")]
    public static void RebuildExistingContainers()
    {
        int done = 0;
        foreach (string prefabPath in Directory.GetFiles(AnimationsRoot, "*.prefab", SearchOption.AllDirectories))
        {
            string path = prefabPath.Replace('\\', '/');
            if (!Regex.IsMatch(path, "/Lineage(NPCs|Npcs|Monsters)[^/]*/"))
            {
                continue;
            }

            string mesh = Path.GetFileNameWithoutExtension(path);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            List<AnimationClip> clips = ClipsForArmature(root, Path.GetDirectoryName(path).Replace('\\', '/'));
            try
            {
                if (AssignContainers(root, mesh, clips))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Import PNJ] Conteneurs reconstruits pour {done} modele(s).");
    }

    private static void ImportModel(string manifestPath)
    {
        Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
        if (manifest.kind != "npc" && manifest.kind != "monster")
        {
            return;
        }

        string modelDir = Path.GetDirectoryName(manifestPath).Replace('\\', '/');
        string fbxPath = $"{modelDir}/{manifest.mesh}.fbx";
        string prefabPath = $"{Path.GetDirectoryName(modelDir).Replace('\\', '/')}/{manifest.mesh}.prefab";

        ConfigureModel(fbxPath);
        List<AnimationClip> clips = ExtractClips(fbxPath, modelDir, manifest.mesh);
        Dictionary<string, Material> materials = BuildMaterials(manifest);

        // On part d'une copie du modele de reference (racine -> model -> .ao/.mo/click_area)
        // et on n'y remplace que l'armature et le mesh.
        bool monster = manifest.kind == "monster";
        GameObject entity = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(monster ? MonsterTemplate : NpcTemplate));
        PrefabUtility.UnpackPrefabInstance(entity, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        GameObject fbx = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath));
        PrefabUtility.UnpackPrefabInstance(fbx, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        try
        {
            string oldName = entity.name;
            Transform model = entity.transform.Find("model");
            var references = RecordBoneReferences(entity, model);

            foreach (Transform child in model.Cast<Transform>().ToList())
            {
                if (child.name != "click_area")
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            foreach (Transform child in fbx.transform.Cast<Transform>().ToList())
            {
                child.SetParent(model, false);
                SetLayer(child, model.gameObject.layer);
            }

            entity.name = manifest.mesh;
            RestoreBoneReferences(references, entity, oldName);

            Animator animator = model.GetComponent<Animator>();
            if (animator != null)
            {
                animator.avatar = null;
                animator.runtimeAnimatorController = null;
            }

            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.sharedMaterials = smr.sharedMaterials
                    .Select(m => m != null && materials.TryGetValue(m.name, out Material mat) ? mat : m)
                    .ToArray();
            }

            Bounds bounds = ModelBounds(entity);
            FitClickArea(model, bounds);
            FitCharacterController(entity, bounds);
            AssignContainers(entity, manifest.mesh, clips);

            PrefabUtility.SaveAsPrefabAsset(entity, prefabPath);
            Debug.Log($"[Import PNJ] {manifest.mesh} : {clips.Count} clip(s), prefab {prefabPath}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(entity);
            UnityEngine.Object.DestroyImmediate(fbx);
        }
    }

    // Memes reglages que les modeles importes a la main : rig generique, echelle 0,019.
    public static void ConfigureModel(string fbxPath)
    {
        ModelImporter importer = (ModelImporter)UnityEditor.AssetImporter.GetAtPath(fbxPath);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        importer.globalScale = ModelScale;
        importer.useFileScale = false;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        // Materiaux gardes dans le FBX : le reglage par defaut du projet les extrait dans Models/Materials.
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.SaveAndReimport();
    }

    private static List<AnimationClip> ExtractClips(string fbxPath, string modelDir, string mesh)
    {
        var clips = new List<AnimationClip>();
        foreach (AnimationClip source in AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>())
        {
            if (source.name.StartsWith("__preview__"))
            {
                continue;
            }

            string sequence = SequenceName(source.name);
            string clipPath = $"{modelDir}/{mesh}.ao_{sequence}.anim";
            // Deux sequences de meme nom (onyx_beast) : la seconde ecraserait la premiere.
            for (int n = 2; clips.Any(c => AssetDatabase.GetAssetPath(c) == clipPath); n++)
            {
                clipPath = $"{modelDir}/{mesh}.ao_{sequence}.{n:000}.anim";
            }
            var clip = new AnimationClip();
            EditorUtility.CopySerialized(source, clip);
            clip.name = Path.GetFileNameWithoutExtension(clipPath);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = IsLooping(Normalize(sequence));
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.DeleteAsset(clipPath);
            AssetDatabase.CreateAsset(clip, clipPath);
            clips.Add(clip);
        }

        return clips;
    }

    private static List<AnimationClip> LoadClips(string folder)
    {
        return Directory.GetFiles(folder, "*.anim", SearchOption.AllDirectories)
            .Select(p => AssetDatabase.LoadAssetAtPath<AnimationClip>(p.Replace('\\', '/')))
            .Where(c => c != null)
            .ToList();
    }

    // "armature.ao|Wait_Hand", "x_m00.ao_wait_Hand.007" ou "(grp) Wait_Hand" donnent "Wait_Hand".
    private static string SequenceName(string clipName)
    {
        string name = clipName;
        int bar = name.LastIndexOf('|');
        if (bar >= 0)
        {
            name = name.Substring(bar + 1);
        }

        int ao = name.IndexOf(".ao_", StringComparison.OrdinalIgnoreCase);
        if (ao >= 0)
        {
            name = name.Substring(ao + 4);
        }

        name = Regex.Replace(name, @"^\([^)]*\)\s*", "");
        name = Regex.Replace(name, @"\.\d+$", "");
        return name;
    }

    private static string Normalize(string name)
    {
        return SequenceName(name).ToLowerInvariant().Replace("_", "").Replace(" ", "");
    }

    private static bool IsLooping(string key)
    {
        return key.StartsWith("wait") || key.StartsWith("walk") || key.StartsWith("run") || key.StartsWith("atkwait")
            || key.StartsWith("deathwait") || key.StartsWith("sitwait") || key.StartsWith("swim");
    }

    private static Dictionary<string, Material> BuildMaterials(Manifest manifest)
    {
        var result = new Dictionary<string, Material>();
        foreach (MaterialInfo info in manifest.materials)
        {
            if (string.IsNullOrEmpty(info.texture))
            {
                Debug.LogWarning($"[Import PNJ] {manifest.mesh} : pas de texture pour {info.slot}");
                continue;
            }

            string folder = $"Assets/Resources/{Path.GetDirectoryName(info.texture).Replace('\\', '/')}/Materials";
            result[info.slot] = CreateMaterial($"{folder}/{info.slot}.mat", info.texture, info.alphaTest, info.alphaRef, info.twoSided);
        }

        return result;
    }

    // Materiau URP Lit copie du modele des PNJ ; texture = chemin Resources sans extension.
    public static Material CreateMaterial(string materialPath, string texture, bool alphaTest, int alphaRef, bool twoSided)
    {
        string texturePath = $"Assets/Resources/{texture}.png";
        AssetDatabase.ImportAsset(texturePath);
        var textureImporter = (TextureImporter)UnityEditor.AssetImporter.GetAtPath(texturePath);
        if (textureImporter != null && alphaTest && !textureImporter.alphaIsTransparency)
        {
            textureImporter.alphaIsTransparency = true;
            textureImporter.SaveAndReimport();
        }

        string folder = Path.GetDirectoryName(materialPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Directory.CreateDirectory(folder);
            AssetDatabase.ImportAsset(folder);
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>(MaterialTemplate));
            AssetDatabase.CreateAsset(material, materialPath);
        }

        Texture2D map = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        material.SetTexture("_BaseMap", map);
        material.SetTexture("_MainTex", map);
        material.SetFloat("_AlphaClip", alphaTest ? 1 : 0);
        material.SetFloat("_Cutoff", alphaTest && alphaRef > 0 ? alphaRef / 255f : 0.5f);
        material.SetFloat("_Cull", twoSided ? 0 : 2);
        if (alphaTest)
        {
            material.EnableKeyword("_ALPHATEST_ON");
        }
        else
        {
            material.DisableKeyword("_ALPHATEST_ON");
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private class BoneReference
    {
        public Component Owner;
        public string Property;
        public string Path;
        public bool IsGameObject;
        public Type Type;
    }

    // Les scripts de la racine pointent vers des os (mains, bouclier, bip01) : on note
    // leur chemin avant de remplacer l'armature, pour les retrouver dans la nouvelle.
    private static List<BoneReference> RecordBoneReferences(GameObject entity, Transform model)
    {
        var result = new List<BoneReference>();
        foreach (Component component in entity.GetComponents<Component>())
        {
            if (component == null || component is Transform)
            {
                continue;
            }

            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script")
                {
                    continue;
                }

                UnityEngine.Object value = property.objectReferenceValue;
                Transform target = value is Component c ? c.transform : (value is GameObject g ? g.transform : null);
                if (target == null || target == model || !target.IsChildOf(model) || target.name == "click_area")
                {
                    continue;
                }

                result.Add(new BoneReference
                {
                    Owner = component,
                    Property = property.propertyPath,
                    Path = AnimationUtility.CalculateTransformPath(target, entity.transform),
                    IsGameObject = value is GameObject,
                    Type = value.GetType(),
                });
            }
        }

        return result;
    }

    private static void RestoreBoneReferences(List<BoneReference> references, GameObject entity, string oldName)
    {
        foreach (var group in references.GroupBy(r => r.Owner))
        {
            var serialized = new SerializedObject(group.Key);
            foreach (BoneReference reference in group)
            {
                string path = reference.Path.Replace(oldName, entity.name);
                Transform found = entity.transform.Find(path) ?? FindByName(entity.transform, path.Substring(path.LastIndexOf('/') + 1));
                UnityEngine.Object value = null;
                if (found != null)
                {
                    value = reference.IsGameObject ? found.gameObject
                        : reference.Type == typeof(Transform) ? found : (UnityEngine.Object)found.GetComponent(reference.Type);
                }

                serialized.FindProperty(reference.Property).objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetLayer(Transform root, int layer)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = layer;
        }
    }

    private static Transform FindByName(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }

        return null;
    }

    private static Bounds ModelBounds(GameObject entity)
    {
        Renderer[] renderers = entity.GetComponentsInChildren<Renderer>(true).Where(r => r.name != "click_area").ToArray();
        if (renderers.Length == 0)
        {
            return new Bounds(new Vector3(0, 0.5f, 0), Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            bounds.Encapsulate(r.bounds);
        }

        bounds.center -= entity.transform.position;
        return bounds;
    }

    // Zone cliquable (couche 13) a la taille du modele.
    private static void FitClickArea(Transform model, Bounds bounds)
    {
        Transform area = model.Find("click_area");
        if (area == null)
        {
            return;
        }

        area.localPosition = model.InverseTransformPoint(model.root.position + bounds.center);
        area.localRotation = Quaternion.identity;
        area.localScale = new Vector3(Mathf.Max(bounds.size.x, 0.25f), bounds.size.y, Mathf.Max(bounds.size.z, 0.25f));
    }

    private static void FitCharacterController(GameObject entity, Bounds bounds)
    {
        CharacterController controller = entity.GetComponent<CharacterController>();
        if (controller == null)
        {
            return;
        }

        controller.height = Mathf.Max(bounds.size.y, 0.2f);
        controller.center = new Vector3(0, controller.height / 2f, 0);
        controller.radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) / 2f, 0.1f, controller.height / 2f);
    }

    // Le nom de l'armature designe le squelette : fox_m00 contient en fait young_fox_m00.ao,
    // seuls les clips "young_fox_m00.ao_*" s'y accrochent.
    private static List<AnimationClip> ClipsForArmature(GameObject root, string prefabDir)
    {
        Transform armature = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith(".ao"));
        List<AnimationClip> local = LoadClips(prefabDir);
        if (armature == null)
        {
            return local;
        }

        string prefix = armature.name + "_";
        List<AnimationClip> matching = local.Where(c => c.name.StartsWith(prefix)).ToList();
        if (matching.Count == 0)
        {
            matching = LoadClips(Path.GetDirectoryName(prefabDir)).Where(c => c.name.StartsWith(prefix)).ToList();
        }

        return matching.Count > 0 ? matching : local;
    }

    private static bool AssignContainers(GameObject root, string mesh, List<AnimationClip> clips)
    {
        var humanoid = root.GetComponent<NewHumanoidAnimationController>();
        var monster = root.GetComponent<NewMonsterAnimationController>();
        if (humanoid == null && monster == null)
        {
            return false;
        }

        var clipsByKey = new Dictionary<string, AnimationClip>();
        foreach (AnimationClip clip in clips.OrderBy(c => c.name))
        {
            string key = Normalize(clip.name);
            if (!clipsByKey.ContainsKey(key))
            {
                clipsByKey[key] = clip;
            }
        }

        var serialized = new SerializedObject(humanoid != null ? (Component)humanoid : monster);
        if (humanoid != null)
        {
            // Les modeles de PNJ ont une foulee plus longue que ceux des joueurs.
            serialized.FindProperty("_defaultWalkAnimationSpeed").floatValue = 0.45f;
            SetContainer<L2HumanoidAnimationContainerDefault>(serialized, "_defaultAnimContainer", $"Npc/{mesh}_Default",
                Enum.GetNames(typeof(HumanoidAnimationDefaultEvent)), n => PickDefault(n, clipsByKey));
            SetContainer<L2HumanoidAnimationContainerAtk>(serialized, "_atkAnimContainer", $"Npc/{mesh}_Atk",
                Enum.GetNames(typeof(HumanoidAnimationAtkEvent)), n => PickWeaponVariant(n, clipsByKey, "atk01"));
            SetContainer<L2HumanoidAnimationContainerSpAtk>(serialized, "_spAtkAnimContainer", $"Npc/{mesh}_SpAtk",
                Enum.GetNames(typeof(HumanoidAnimationSpAtkEvent)), n => PickWeaponVariant(n, clipsByKey, null));
            List<AnimationClip> socials = SocialOrder(clipsByKey);
            SetContainer<L2HumanoidAnimationContainerSocial>(serialized, "_socialAnimContainer", $"Npc/{mesh}_Social",
                Enum.GetNames(typeof(HumanoidAnimationSocialEvent)), n => Social(n, socials));
        }
        else
        {
            SetContainer<L2MonsterAnimationContainer>(serialized, "_animContainer", $"Monster/{mesh}",
                Enum.GetNames(typeof(MonsterAnimationEvent)), n => PickMonster(n, clipsByKey));
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static void SetContainer<T>(SerializedObject owner, string field, string assetName, string[] events, Func<string, AnimationClip> pick)
        where T : ScriptableObject
    {
        string path = $"{TemplateRoot}/{assetName}.asset";
        T container = AssetDatabase.LoadAssetAtPath<T>(path);
        if (container == null)
        {
            container = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(container, path);
        }

        var serialized = new SerializedObject(container);
        SerializedProperty animations = serialized.FindProperty("_animations");
        animations.arraySize = events.Length;
        for (int i = 0; i < events.Length; i++)
        {
            SerializedProperty entry = animations.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("_event").intValue = i;
            entry.FindPropertyRelative("_clip").objectReferenceValue = pick(events[i]);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(container);
        owner.FindProperty(field).objectReferenceValue = container;
    }

    private static AnimationClip Find(Dictionary<string, AnimationClip> clips, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (key != null && clips.TryGetValue(key, out AnimationClip clip))
            {
                return clip;
            }
        }

        return null;
    }

    private static AnimationClip FindPrefix(Dictionary<string, AnimationClip> clips, string prefix, string exclude = null)
    {
        return clips.Where(kv => kv.Key.StartsWith(prefix) && (exclude == null || !kv.Key.Contains(exclude)))
            .OrderBy(kv => kv.Key.Length).Select(kv => kv.Value).FirstOrDefault();
    }

    // wait_pole manquant : on prend wait_hand, wait_1HS, puis n'importe quelle attente,
    // pour qu'un PNJ ne reste jamais en T-pose ni ne glisse.
    private static AnimationClip PickDefault(string eventName, Dictionary<string, AnimationClip> clips)
    {
        string key = eventName.ToLowerInvariant().Replace("_", "");
        AnimationClip exact = Find(clips, key);
        if (exact != null)
        {
            return exact;
        }

        foreach (string move in new[] { "atkwait", "wait", "walk", "run" })
        {
            if (key.StartsWith(move) && !(move == "wait" && key.StartsWith("atkwait")))
            {
                AnimationClip clip = Find(clips, move + "hand", move + "1hs") ?? FindPrefix(clips, move);
                if (clip == null && move == "walk") clip = Find(clips, "runhand", "run1hs") ?? FindPrefix(clips, "run");
                if (clip == null && move == "run") clip = Find(clips, "walkhand", "walk1hs") ?? FindPrefix(clips, "walk");
                if (clip == null && move == "atkwait") clip = Find(clips, "waithand", "wait1hs") ?? FindPrefix(clips, "wait");
                return clip;
            }
        }

        switch (key)
        {
            case "death": return FindPrefix(clips, "death", "wait");
            case "deathwait": return FindPrefix(clips, "deathwait");
            case "jumprun": return FindPrefix(clips, "jumprun");
            case "jumpstand": return FindPrefix(clips, "jumpstand");
            case "pickup": return FindPrefix(clips, "pickitem") ?? FindPrefix(clips, "picitem") ?? FindPrefix(clips, "pickup");
            case "sitwait": return FindPrefix(clips, "sitwait");
            case "sit": return FindPrefix(clips, "sit", "wait");
            case "stand": return FindPrefix(clips, "stand");
            case "magicshot": return FindPrefix(clips, "magicshot") ?? FindPrefix(clips, "magic01");
            case "swimwait": return FindPrefix(clips, "swimwait");
            case "swim": return FindPrefix(clips, "swim", "wait") ?? FindPrefix(clips, "swim", "death");
            default: return FindPrefix(clips, key);
        }
    }

    private static AnimationClip PickWeaponVariant(string eventName, Dictionary<string, AnimationClip> clips, string anyFallback)
    {
        string key = eventName.ToLowerInvariant().Replace("_", "");
        AnimationClip clip = Find(clips, key);
        if (clip != null || anyFallback == null || !key.StartsWith(anyFallback))
        {
            return clip;
        }

        return Find(clips, anyFallback + "hand", anyFallback + "1hs") ?? FindPrefix(clips, anyFallback);
    }

    private static AnimationClip PickMonster(string eventName, Dictionary<string, AnimationClip> clips)
    {
        switch (eventName)
        {
            case "wait": return Find(clips, "wait") ?? FindPrefix(clips, "wait");
            case "atk01": return Find(clips, "atk01") ?? FindPrefix(clips, "atk");
            case "atkwait": return Find(clips, "atkwait") ?? Find(clips, "wait");
            case "death": return FindPrefix(clips, "death", "wait");
            case "deathwait": return FindPrefix(clips, "deathwait");
            case "run": return Find(clips, "run") ?? Find(clips, "walk");
            case "walk": return Find(clips, "walk") ?? Find(clips, "run");
            case "spatk": return FindPrefix(clips, "spatk");
            case "spwait": return FindPrefix(clips, "spwait");
            default: return null;
        }
    }

    // Meme ordre que les conteneurs faits a la main : social01, puis les spwait, puis les autres socials.
    private static List<AnimationClip> SocialOrder(Dictionary<string, AnimationClip> clips)
    {
        var ordered = new List<AnimationClip>();
        AnimationClip first = Find(clips, "social01");
        if (first != null) ordered.Add(first);
        ordered.AddRange(clips.Where(kv => kv.Key.StartsWith("spwait")).OrderBy(kv => kv.Key).Select(kv => kv.Value));
        ordered.AddRange(clips.Where(kv => kv.Key.StartsWith("social") && kv.Value != first).OrderBy(kv => kv.Key).Select(kv => kv.Value));
        return ordered;
    }

    private static AnimationClip Social(string eventName, List<AnimationClip> socials)
    {
        int index = int.Parse(eventName.Substring("social".Length)) - 1;
        return index < socials.Count ? socials[index] : null;
    }
}
#endif
