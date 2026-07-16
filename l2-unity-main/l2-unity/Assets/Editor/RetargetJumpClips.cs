using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Retarget du clip de saut du MFighter (JumpRun_Dual_MFighter.001, utilise par
// jump_run ET jump_stand) vers les races Orc/Shaman, dont les PSA d'origine ne
// contiennent aucune animation de saut.
//
// Methode : transplantation des courbes au niveau du clip Unity.
//  - courbes de ROTATION locales copiees vers les chemins equivalents de la
//    hierarchie cible (mapping insensible a la casse via les chemins du clip
//    Wait de la race ; les os absents chez la cible sont abandonnes) ;
//  - courbes de POSITION abandonnees sauf l'os racine Bip01 (motion du saut),
//    mise a l'echelle du ratio de taille des rigs pour garder les proportions ;
//  - conteneurs <Type>_Default.asset cables automatiquement (events jump_run=13
//    et jump_stand=14, comme le MFighter qui utilise le meme clip pour les deux).
//
// Usage : Tools > L2Unity > Orc > 4. Retarget Jump Clips (apres extraction des
// clips de la race, car le mapping de chemins lit le clip Wait existant).
public class RetargetJumpClips
{
    const string SourceClipPath = "Assets/Resources/Data/Animations/Fighter/MFighter/Clips/MFighter_m000_b.ao_JumpRun_Dual_MFighter.001.anim";
    const string SourceRootSegment = "MFighter_m000_b.ao";
    const string SourceRootBonePath = "MFighter_m000_b.ao/bip01";
    const string BasePath = "Assets/Resources/Data/Animations";

    static readonly (string typeName, string raceFolder)[] Races =
    {
        ("FOrc", "Orc"), ("MOrc", "Orc"), ("FShaman", "Shaman"), ("MShaman", "Shaman"),
    };

    [MenuItem("Tools/L2Unity/Orc/4. Retarget Jump Clips")]
    static void Retarget()
    {
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceClipPath);
        if (source == null)
        {
            Debug.LogError($"[RetargetJump] Clip source introuvable: {SourceClipPath}");
            return;
        }

        foreach ((string typeName, string raceFolder) in Races)
        {
            string clipsDir = $"{BasePath}/{raceFolder}/{typeName}/Clips";
            string waitPath = $"{clipsDir}/{typeName}_m000_b.ao_Wait_1HS_{typeName}.anim";
            AnimationClip waitClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(waitPath);
            if (waitClip == null)
            {
                Debug.LogError($"[RetargetJump] {typeName}: clip Wait introuvable ({waitPath}) - extraire les clips d'abord (etape Extract).");
                continue;
            }

            // dictionnaire chemin-minuscules -> chemin exact de la hierarchie cible
            var targetPaths = new Dictionary<string, string>();
            foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(waitClip))
            {
                if (!targetPaths.ContainsKey(b.path.ToLowerInvariant()))
                {
                    targetPaths.Add(b.path.ToLowerInvariant(), b.path);
                }
            }

            string targetRootSegment = $"{typeName}_m000_b.ao";
            float scale = HeightRatio(typeName, raceFolder);

            var jump = new AnimationClip { frameRate = source.frameRate };
            int copied = 0, dropped = 0;

            foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(source))
            {
                bool isRootPosition = b.path == SourceRootBonePath && b.propertyName.StartsWith("m_LocalPosition");
                if (b.propertyName.StartsWith("m_LocalPosition") && !isRootPosition)
                {
                    dropped++;
                    continue; // les longueurs d'os de la cible font foi
                }

                string mappedLower = b.path.ToLowerInvariant().Replace(SourceRootSegment.ToLowerInvariant(), targetRootSegment.ToLowerInvariant());
                if (!targetPaths.TryGetValue(mappedLower, out string targetPath))
                {
                    dropped++;
                    continue; // os absent chez la cible
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, b);
                if (curve == null) continue;

                if (isRootPosition && !Mathf.Approximately(scale, 1f))
                {
                    Keyframe[] keys = curve.keys;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        keys[i].value *= scale;
                        keys[i].inTangent *= scale;
                        keys[i].outTangent *= scale;
                    }
                    curve = new AnimationCurve(keys);
                }

                var nb = new EditorCurveBinding { path = targetPath, propertyName = b.propertyName, type = b.type };
                AnimationUtility.SetEditorCurve(jump, nb, curve);
                copied++;
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(jump, settings);

            string outPath = $"{clipsDir}/{typeName}_m000_b.ao_JumpRun_{typeName}.anim";
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(jump, existing);
                existing.name = Path.GetFileNameWithoutExtension(outPath);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(jump);
                jump = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(jump, outPath);
            }

            WireContainer(typeName, jump);
            Debug.Log($"[RetargetJump] {typeName}: {copied} courbes copiees, {dropped} abandonnees, echelle racine x{scale:F3} -> {outPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ratio de taille des rigs = |localPosition du Bip01| cible / source,
    // lu directement dans les FBX de corps des deux races.
    static float HeightRatio(string typeName, string raceFolder)
    {
        float src = RootBoneMagnitude("Assets/Resources/Data/Animations/Fighter/MFighter/Models/MFighter_m000_b.fbx", "bip01");
        float dst = RootBoneMagnitude($"{BasePath}/{raceFolder}/{typeName}/Models/{typeName}_m000_b.fbx", "Bip01");
        if (src <= 0f || dst <= 0f)
        {
            Debug.LogWarning($"[RetargetJump] {typeName}: ratio de taille indeterminable (src={src}, dst={dst}), echelle 1.");
            return 1f;
        }
        return dst / src;
    }

    static float RootBoneMagnitude(string fbxPath, string boneName)
    {
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbx == null) return -1f;
        Transform bone = FindDeepChild(fbx.transform, boneName);
        return bone != null ? bone.localPosition.magnitude : -1f;
    }

    static void WireContainer(string typeName, AnimationClip jump)
    {
        string containerPath = $"{BasePath}/_Template/Player/{typeName}_Default.asset";
        Object container = AssetDatabase.LoadAssetAtPath<Object>(containerPath);
        if (container == null)
        {
            Debug.LogWarning($"[RetargetJump] {typeName}: conteneur introuvable ({containerPath}), events jump non cables.");
            return;
        }

        var so = new SerializedObject(container);
        SerializedProperty animations = so.FindProperty("_animations");
        if (animations == null || !animations.isArray)
        {
            Debug.LogWarning($"[RetargetJump] {typeName}: propriete _animations introuvable dans le conteneur.");
            return;
        }

        int wired = 0;
        for (int i = 0; i < animations.arraySize; i++)
        {
            SerializedProperty entry = animations.GetArrayElementAtIndex(i);
            SerializedProperty ev = entry.FindPropertyRelative("_event");
            if (ev != null && (ev.intValue == (int)HumanoidAnimationDefaultEvent.jump_run
                            || ev.intValue == (int)HumanoidAnimationDefaultEvent.jump_stand))
            {
                entry.FindPropertyRelative("_clip").objectReferenceValue = jump;
                wired++;
            }
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(container);
        Debug.Log($"[RetargetJump] {typeName}: {wired} event(s) jump cable(s) dans {containerPath}.");
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase)) return child;
            Transform r = FindDeepChild(child, name);
            if (r != null) return r;
        }
        return null;
    }
}
