using System.IO;
using UnityEditor;
using UnityEngine;

public class ExtractAnimationClips
{
    [MenuItem("Tools/L2Unity/Extract Animation Clips From Selected FBX")]
    static void ExtractFromSelection()
    {
        foreach (Object obj in Selection.objects)
        {
            string fbxPath = AssetDatabase.GetAssetPath(obj);
            if (!fbxPath.ToLower().EndsWith(".fbx"))
            {
                continue;
            }

            // .../<Type>/Models/anim/<Type>_anim.fbx
            string animDir = Path.GetDirectoryName(fbxPath).Replace("\\", "/");
            string modelsDir = Path.GetDirectoryName(animDir).Replace("\\", "/");
            string typeDir = Path.GetDirectoryName(modelsDir).Replace("\\", "/");
            string typeName = Path.GetFileName(typeDir);

            string clipsDir = $"{typeDir}/Clips";
            if (!AssetDatabase.IsValidFolder(clipsDir))
            {
                AssetDatabase.CreateFolder(typeDir, "Clips");
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            int count = 0;
            int failed = 0;

            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    try
                    {
                        // Blender nomme les Takes "NomArmature|NomAction" en export NLA.
                        // On ne garde que la partie apres le "|" (le vrai nom de sequence).
                        string rawName = clip.name;
                        int pipeIndex = rawName.LastIndexOf('|');
                        string sequenceName = pipeIndex >= 0 ? rawName.Substring(pipeIndex + 1) : rawName;

                        // Nettoyage defensif de tout caractere invalide restant.
                        foreach (char c in Path.GetInvalidFileNameChars())
                        {
                            sequenceName = sequenceName.Replace(c, '_');
                        }

                        string clipFileName = $"{typeName}_m000_b.ao_{sequenceName}.anim";
                        string clipPath = $"{clipsDir}/{clipFileName}";

                        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                        if (existing != null)
                        {
                            // Ecrase le CONTENU en conservant l'asset (donc son GUID) :
                            // les conteneurs d'animation qui referencent ce clip restent
                            // valides. Indispensable pour re-extraire apres un re-export
                            // du FBX d'animations.
                            AnimationClip source = Object.Instantiate(clip);
                            EditorUtility.CopySerialized(source, existing);
                            existing.name = Path.GetFileNameWithoutExtension(clipPath);
                            Object.DestroyImmediate(source);
                            ApplyLoopSetting(existing, sequenceName);
                            EditorUtility.SetDirty(existing);
                        }
                        else
                        {
                            AnimationClip newClip = Object.Instantiate(clip);
                            AssetDatabase.CreateAsset(newClip, clipPath);
                            ApplyLoopSetting(newClip, sequenceName);
                        }
                        count++;
                    }
                    catch (System.Exception e)
                    {
                        failed++;
                        Debug.LogWarning($"[ExtractAnimationClips] Echec sur le clip '{clip.name}': {e.Message}");
                    }
                }
            }

            Debug.Log($"[ExtractAnimationClips] {typeName}: {count} clip(s) extrait(s), {failed} echec(s) -> {clipsDir}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // A lancer sur des clips deja extraits (idempotent) pour corriger m_LoopTime
    // sans avoir a refaire toute l'extraction.
    [MenuItem("Tools/L2Unity/Fix Loop Settings On Existing Clips")]
    static void FixLoopSettingsOnExisting()
    {
        string[] raceFolders = { "Orc", "Shaman" };
        int fixedCount = 0;
        int total = 0;

        foreach (string race in raceFolders)
        {
            string raceDir = $"Assets/Resources/Data/Animations/{race}";
            string[] clipGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { raceDir });
            foreach (string guid in clipGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("/Clips/")) continue;

                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) continue;

                string fileName = Path.GetFileNameWithoutExtension(path);
                int markerIndex = fileName.IndexOf(".ao_");
                if (markerIndex < 0) continue;
                string sequenceName = fileName.Substring(markerIndex + 4);

                total++;
                bool changed = ApplyLoopSetting(clip, sequenceName);
                if (changed) fixedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ExtractAnimationClips] Loop settings: {fixedCount}/{total} clip(s) mis a jour.");
    }

    // Retourne true si le clip doit boucler (marche/course/nage/postures d'attente),
    // false pour les animations "one-shot" (attaques, morts, sorts, emotes...).
    static bool ShouldLoop(string sequenceName)
    {
        string lower = sequenceName.ToLower();
        if (lower.Contains("wait")) return true;
        if (lower.StartsWith("walk") || lower.StartsWith("run")) return true;
        if (lower == "swim") return true;
        if (lower == "stand") return true;
        return false;
    }

    static bool ApplyLoopSetting(AnimationClip clip, string sequenceName)
    {
        bool shouldLoop = ShouldLoop(sequenceName);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        if (settings.loopTime == shouldLoop) return false;

        settings.loopTime = shouldLoop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return true;
    }
}
