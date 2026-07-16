using System.IO;
using UnityEditor;
using UnityEngine;

public class OrcShamanPrefabGenerator
{
    class TypeConfig
    {
        public string typeName;
        public string raceFolder;
        public int raceId;    // CharacterModelType
        public int race;      // CharacterRace
        public int audioRace; // CharacterModelSound
    }

    static readonly TypeConfig[] Configs = new TypeConfig[]
    {
        new TypeConfig{ typeName = "MOrc",    raceFolder = "Orc",    raceId = 8, race = 3, audioRace = 9  },
        new TypeConfig{ typeName = "FOrc",    raceFolder = "Orc",    raceId = 9, race = 3, audioRace = 10 },
        new TypeConfig{ typeName = "MShaman", raceFolder = "Shaman", raceId = 6, race = 8, audioRace = 7  },
        new TypeConfig{ typeName = "FShaman", raceFolder = "Shaman", raceId = 7, race = 8, audioRace = 8  },
    };

    const string PlaceholderMatGuid = "78187e586541d9d40907b180586974f7";
    const string WeaponTrailGuid = "ac5fa735a19959147bf49113757f50b8";

    static string BasePath => "Assets/Resources/Data/Animations";

    // ---------- ETAPE 1 : prefabs de pieces d'equipement (un par FBX dans Models/) ----------

    [MenuItem("Tools/L2Unity/Orc/1. Generate Gear Piece Prefabs")]
    static void GeneratePiecePrefabs()
    {
        string placeholderMatPath = AssetDatabase.GUIDToAssetPath(PlaceholderMatGuid);
        Material placeholderMat = AssetDatabase.LoadAssetAtPath<Material>(placeholderMatPath);

        int totalCreated = 0;
        int totalSkipped = 0;
        int totalFailed = 0;

        foreach (TypeConfig cfg in Configs)
        {
            string modelsDir = $"{BasePath}/{cfg.raceFolder}/{cfg.typeName}/Models";
            if (!AssetDatabase.IsValidFolder(modelsDir))
            {
                Debug.LogWarning($"[OrcPrefabGen] Dossier introuvable: {modelsDir}");
                continue;
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { modelsDir });
            foreach (string guid in guids)
            {
                string fbxPath = AssetDatabase.GUIDToAssetPath(guid);
                if (fbxPath.Replace("\\", "/").Contains("/anim/")) continue;
                if (!fbxPath.ToLower().EndsWith(".fbx")) continue;

                string pieceName = Path.GetFileNameWithoutExtension(fbxPath);
                string outPath = $"{BasePath}/{cfg.raceFolder}/{pieceName}.prefab";

                if (AssetDatabase.LoadAssetAtPath<GameObject>(outPath) != null)
                {
                    totalSkipped++;
                    continue;
                }

                GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbxAsset == null)
                {
                    Debug.LogWarning($"[OrcPrefabGen] Impossible de charger {fbxPath}");
                    totalFailed++;
                    continue;
                }

                Mesh mesh = null;
                SkinnedMeshRenderer sourceSmr = fbxAsset.GetComponentInChildren<SkinnedMeshRenderer>();
                if (sourceSmr != null) mesh = sourceSmr.sharedMesh;
                if (mesh == null)
                {
                    MeshFilter mf = fbxAsset.GetComponentInChildren<MeshFilter>();
                    if (mf != null) mesh = mf.sharedMesh;
                }
                if (mesh == null)
                {
                    Debug.LogWarning($"[OrcPrefabGen] Aucun mesh trouve dans {fbxPath}");
                    totalFailed++;
                    continue;
                }

                GameObject go = new GameObject(pieceName);
                go.transform.localRotation = new Quaternion(-0.7071068f, 0f, 0f, 0.7071067f);
                go.transform.localScale = new Vector3(100f, 100f, 100f);

                SkinnedMeshRenderer smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = new Transform[mesh.bindposes.Length];
                smr.rootBone = null;
                if (placeholderMat != null)
                {
                    smr.sharedMaterial = placeholderMat;
                }

                // sourceSmr.bones contient les vrais noms d'os (dans l'ordre des bindposes)
                // du FBX d'origine. On les capture ici car ils seraient sinon perdus
                // (smr.bones ci-dessus est un tableau vide) ; SkinnedMeshSync s'en sert
                // pour remapper par nom vers le squelette de reference a l'equipement.
                if (sourceSmr != null && sourceSmr.bones != null && sourceSmr.bones.Length == mesh.bindposes.Length)
                {
                    PieceBoneNames boneNamesCache = go.AddComponent<PieceBoneNames>();
                    boneNamesCache.boneNames = new string[sourceSmr.bones.Length];
                    for (int i = 0; i < sourceSmr.bones.Length; i++)
                    {
                        boneNamesCache.boneNames[i] = sourceSmr.bones[i] != null ? sourceSmr.bones[i].name : null;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(go, outPath);
                Object.DestroyImmediate(go);
                totalCreated++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[OrcPrefabGen] Pieces d'equipement: {totalCreated} crees, {totalSkipped} deja presents, {totalFailed} echecs.");
    }

    // ---------- ETAPE 2 : <Type>_Anim.prefab (squelette + weapon_trail) ----------

    [MenuItem("Tools/L2Unity/Orc/2. Generate Anim Prefabs")]
    static void GenerateAnimPrefabs()
    {
        string weaponTrailPath = AssetDatabase.GUIDToAssetPath(WeaponTrailGuid);
        GameObject weaponTrailPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(weaponTrailPath);

        foreach (TypeConfig cfg in Configs)
        {
            string outPath = $"{BasePath}/{cfg.raceFolder}/{cfg.typeName}/{cfg.typeName}_Anim.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(outPath) != null)
            {
                Debug.Log($"[OrcPrefabGen] {outPath} existe deja, saute.");
                continue;
            }

            string bodyFbxPath = $"{BasePath}/{cfg.raceFolder}/{cfg.typeName}/Models/{cfg.typeName}_m000_b.fbx";
            GameObject bodyFbx = AssetDatabase.LoadAssetAtPath<GameObject>(bodyFbxPath);
            if (bodyFbx == null)
            {
                Debug.LogError($"[OrcPrefabGen] FBX de base introuvable: {bodyFbxPath}");
                continue;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(bodyFbx);
            instance.name = $"{cfg.typeName}_Anim";

            // Meme correction d'echelle/rotation que sur les prefabs de pieces (etape 1) :
            // l'import FBX applique un globalScale de 0.019, il faut compenser par x100
            // pour retrouver une taille de personnage normale.
            instance.transform.localRotation = new Quaternion(-0.7071068f, 0f, 0f, 0.7071067f);
            instance.transform.localScale = new Vector3(100f, 100f, 100f);

            Animator anim = instance.GetComponent<Animator>();
            if (anim == null) anim = instance.AddComponent<Animator>();
            anim.runtimeAnimatorController = null;
            anim.avatar = null;

            if (weaponTrailPrefab != null)
            {
                Transform rHand = FindDeepChild(instance.transform, "Weapon_R_Bone");
                if (rHand != null)
                {
                    GameObject trailInstance = (GameObject)PrefabUtility.InstantiatePrefab(weaponTrailPrefab, rHand);
                    trailInstance.name = "weapon_trail";
                    trailInstance.transform.localPosition = Vector3.zero;
                    trailInstance.transform.localRotation = Quaternion.identity;
                    trailInstance.transform.localScale = Vector3.one * 0.1f;
                    trailInstance.SetActive(false);
                }
                else
                {
                    Debug.LogWarning($"[OrcPrefabGen] {cfg.typeName}: bone 'Weapon_R_Bone' introuvable, weapon_trail non attache.");
                }
            }

            PrefabUtility.SaveAsPrefabAsset(instance, outPath);
            Object.DestroyImmediate(instance);
            Debug.Log($"[OrcPrefabGen] {outPath} cree.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ---------- ETAPE 3 : Pawn_/Player_/User_<Type>.prefab ----------

    [MenuItem("Tools/L2Unity/Orc/3. Generate Pawn Player User Prefabs")]
    static void GenerateWrapperPrefabs()
    {
        string fmagicDir = $"{BasePath}/Magic/FMagic";

        foreach (TypeConfig cfg in Configs)
        {
            foreach (string kind in new[] { "Pawn", "Player", "User" })
            {
                GenerateOneWrapper(cfg, kind, fmagicDir);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void GenerateOneWrapper(TypeConfig cfg, string kind, string templateDir)
    {
        string templatePath = $"{templateDir}/{kind}_FMagic.prefab";
        string outDir = $"{BasePath}/{cfg.raceFolder}/{cfg.typeName}";
        string outPath = $"{outDir}/{kind}_{cfg.typeName}.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(outPath) != null)
        {
            Debug.Log($"[OrcPrefabGen] {outPath} existe deja, saute.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(templatePath) == null)
        {
            Debug.LogError($"[OrcPrefabGen] Modele introuvable: {templatePath}");
            return;
        }

        if (!AssetDatabase.CopyAsset(templatePath, outPath))
        {
            Debug.LogError($"[OrcPrefabGen] Echec de copie {templatePath} -> {outPath}");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(outPath);
        try
        {
            root.name = $"{kind}_{cfg.typeName}";

            Transform model = root.transform.Find("model");
            if (model == null)
            {
                Debug.LogError($"[OrcPrefabGen] {outPath}: enfant 'model' introuvable.");
                return;
            }

            Transform oldAnimInstance = null;
            foreach (Transform child in model)
            {
                if (child.name != "Bodyparts" && child.name != "click_area")
                {
                    oldAnimInstance = child;
                    break;
                }
            }
            if (oldAnimInstance == null)
            {
                Debug.LogError($"[OrcPrefabGen] {outPath}: instance de FMagic_Anim introuvable sous 'model'.");
                return;
            }

            Vector3 oldPos = oldAnimInstance.localPosition;
            Quaternion oldRot = oldAnimInstance.localRotation;
            Vector3 oldScale = oldAnimInstance.localScale;
            int oldSiblingIndex = oldAnimInstance.GetSiblingIndex();

            string newAnimPath = $"{BasePath}/{cfg.raceFolder}/{cfg.typeName}/{cfg.typeName}_Anim.prefab";
            GameObject newAnimPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(newAnimPath);
            if (newAnimPrefab == null)
            {
                Debug.LogError($"[OrcPrefabGen] {newAnimPath} introuvable - lance d'abord l'etape 2.");
                return;
            }

            Object.DestroyImmediate(oldAnimInstance.gameObject);
            GameObject newInstance = (GameObject)PrefabUtility.InstantiatePrefab(newAnimPrefab, model);
            newInstance.transform.localPosition = oldPos;
            newInstance.transform.localRotation = oldRot;
            newInstance.transform.localScale = oldScale;
            newInstance.transform.SetSiblingIndex(oldSiblingIndex);
            newInstance.name = $"{cfg.typeName}_Anim";

            Transform rHand = FindDeepChild(newInstance.transform, "Weapon_R_Bone");
            Transform lHand = FindDeepChild(newInstance.transform, "Weapon_L_Bone");
            Transform shield = FindDeepChild(newInstance.transform, "Shield_L_Bone");
            Transform head = FindDeepChild(newInstance.transform, "Bip01_head");
            Transform rootBone = FindDeepChild(newInstance.transform, "bip01");
            SkinnedMeshRenderer smr = newInstance.GetComponentInChildren<SkinnedMeshRenderer>();
            Animator animator = newInstance.GetComponentInChildren<Animator>();

            Transform weaponTrailTransform = rHand != null ? rHand.Find("weapon_trail") : null;
            ParticleSystem weaponTrailPs = weaponTrailTransform != null ? weaponTrailTransform.GetComponent<ParticleSystem>() : null;
            GameObject headGo = head != null ? head.gameObject : null;

            // "Bodyparts" est un frere de l'instance Anim sous 'model' (deja utilise plus haut pour le detecter).
            Transform bodyparts = model.Find("Bodyparts");
            GameObject bodypartsGo = bodyparts != null ? bodyparts.gameObject : null;

            LogIfMissing(cfg.typeName, "Weapon_R_Bone", rHand);
            LogIfMissing(cfg.typeName, "Weapon_L_Bone", lHand);
            LogIfMissing(cfg.typeName, "Shield_L_Bone", shield);
            LogIfMissing(cfg.typeName, "Bip01_head", head);
            LogIfMissing(cfg.typeName, "bip01", rootBone);
            LogIfMissing(cfg.typeName, "Bodyparts", bodyparts);
            if (weaponTrailPs == null)
            {
                Debug.LogWarning($"[OrcPrefabGen] {cfg.typeName}: 'weapon_trail' introuvable sous Weapon_R_Bone (etape 2 non relancee ?).");
            }

            // CameraController.SetTarget() cherche ce tag directement (pas la reference _rootBone)
            // pour calculer _rootBoneHeight. Sans lui, FindRecursive renvoie null et SetTarget plante,
            // empechant la reconstruction du CameraCollisionDetection (cf FMagic_Anim.prefab: bip01 a m_TagString: Root).
            if (rootBone != null)
            {
                rootBone.gameObject.tag = "Root";
            }

            foreach (Component comp in root.GetComponents<Component>())
            {
                if (comp == null) continue;
                SerializedObject so = new SerializedObject(comp);
                TrySetObjectRef(so, "_rightHandBone", rHand);
                TrySetObjectRef(so, "_leftHandBone", lHand);
                TrySetObjectRef(so, "_shieldBone", shield);
                TrySetObjectRef(so, "_headBone", headGo); // GameObject, pas Transform (type du champ dans UserGear.cs)
                TrySetObjectRef(so, "_rootBone", rootBone);
                TrySetObjectRef(so, "_rootSkinnedRenderer", smr);
                TrySetObjectRef(so, "_animator", animator);
                TrySetObjectRef(so, "_Animator", animator); // AnimancerComponent utilise cette casse precise
                TrySetObjectRef(so, "_weaponTrail", weaponTrailPs);
                TrySetObjectRef(so, "_bodypartsContainer", bodypartsGo);
                so.ApplyModifiedProperties();
            }

            // Piece 'ah' de la race : bindee sur le rig d'ORIGINE (celui des clips
            // d'animation). SkinnedMeshSync s'en sert pour re-binder visage/cheveux
            // dessus, sinon la tete flotte (les autres pieces sont bindees sur un rig
            // aux poses de repos differentes de celles supposees par les animations).
            string donorPath = $"{BasePath}/{cfg.raceFolder}/{cfg.typeName}_m000_m00_ah.prefab";
            GameObject bindDonor = AssetDatabase.LoadAssetAtPath<GameObject>(donorPath);
            if (bindDonor == null)
            {
                Debug.LogWarning($"[OrcPrefabGen] {cfg.typeName}: piece donneuse de bind introuvable ({donorPath}), _bindDonorPiece non cable.");
            }

            foreach (Component comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                SerializedObject so = new SerializedObject(comp);

                TrySetObjectRef(so, "_bindDonorPiece", bindDonor);

                SerializedProperty raceIdProp = so.FindProperty("_raceId");
                if (raceIdProp != null && raceIdProp.propertyType == SerializedPropertyType.Enum)
                {
                    raceIdProp.enumValueIndex = cfg.raceId;
                }

                SerializedProperty raceProp = so.FindProperty("_race");
                if (raceProp != null && raceProp.propertyType == SerializedPropertyType.Enum)
                {
                    raceProp.enumValueIndex = comp.GetType().Name == "HumanoidAudioHandler" ? cfg.audioRace : cfg.race;
                }

                so.ApplyModifiedProperties();
            }

            string tplDir = $"{BasePath}/_Template/Player";
            foreach (Component comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                string typeName = comp.GetType().Name;
                if (typeName != "NewHumanoidAnimationController" && typeName != "NewPlayerAnimationController") continue;

                SerializedObject so = new SerializedObject(comp);
                TryLoadAndSetAsset(so, "_defaultAnimContainer", $"{tplDir}/{cfg.typeName}_Default.asset");
                TryLoadAndSetAsset(so, "_atkAnimContainer", $"{tplDir}/{cfg.typeName}_Atk.asset");
                TryLoadAndSetAsset(so, "_spAtkAnimContainer", $"{tplDir}/{cfg.typeName}_SpAtk.asset");
                so.ApplyModifiedProperties();
            }

            PrefabUtility.SaveAsPrefabAsset(root, outPath);
            Debug.Log($"[OrcPrefabGen] {outPath} cree et relie.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void LogIfMissing(string typeName, string boneName, Transform found)
    {
        if (found == null)
        {
            Debug.LogWarning($"[OrcPrefabGen] {typeName}: bone '{boneName}' introuvable dans le squelette importe.");
        }
    }

    static void TrySetObjectRef(SerializedObject so, string propName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop != null && value != null)
        {
            prop.objectReferenceValue = value;
        }
    }

    static void TryLoadAndSetAsset(SerializedObject so, string propName, string assetPath)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop == null) return;
        Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
        if (asset != null)
        {
            prop.objectReferenceValue = asset;
        }
        else
        {
            Debug.LogWarning($"[OrcPrefabGen] Asset introuvable: {assetPath}");
        }
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
