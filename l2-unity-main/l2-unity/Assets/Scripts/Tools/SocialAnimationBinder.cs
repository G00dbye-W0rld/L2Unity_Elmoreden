#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Branche les clips d'emote des races jouables sur leur conteneur social, qui etait vide.
// L'index dans le conteneur est l'identifiant social du serveur (2 a 13), pas un rang
// d'affichage : c'est ce que RequestSocialAction envoie et ce que Emote() rejoue.
public static class SocialAnimationBinder
{
    private const string PlayerContainers = "Assets/Resources/Data/Animations/_Template/Player";
    private const string AnimationsRoot = "Assets/Resources/Data/Animations";
    private const float RaceScale = 0.019f;

    // Clip du client pour chaque identifiant social. La correspondance vient du jeu minimal
    // de la naine, qui ne contient que les douze emotes de base : chacune tombe sur un seul
    // identifiant, sans ambiguite. "Social_Atk" est bien la charge, pas une attaque.
    private static readonly Dictionary<int, string> Clips = new Dictionary<int, string>
    {
        { 2, "Social_Nod" },
        { 3, "Social_Victory" },
        { 4, "Social_Atk" },
        { 5, "Social_no" },
        { 6, "Social_yes" },
        { 7, "Social_bow" },
        { 8, "Social_unaware" },
        { 9, "Social_waiting_a" },
        { 10, "Social_laugh" },
        { 11, "Social_clap" },
        { 12, "Social_dance" },
        { 13, "Social_sad" },
        { 14, "Social_charming" },
        { 15, "Social_shy" },
        { 16, "Social_Couple_Bow" },
        { 17, "Social_Couple_Hifive" },
        { 18, "Social_Couple_Dance" },
        { 19, "Social_Propose" },
        { 20, "Social_Provocation" },
        { 21, "Social_Beauty" },
    };

    // Les emotes manquantes de la naine et de l'elfe noire arrivent dans un FBX a part,
    // produit par InterludeEmoteImport.py. On en sort les clips avant de brancher, sans
    // toucher a ceux qui existent deja.
    [MenuItem("L2/Animations/Extraire les emotes recuperees du client")]
    public static void ExtractImported()
    {
        int created = 0;
        foreach (string fbx in Directory.GetFiles(AnimationsRoot, "*_emotes.fbx", SearchOption.AllDirectories))
        {
            string fbxPath = fbx.Replace('\\', '/');
            string mesh = Path.GetFileNameWithoutExtension(fbxPath).Replace("_emotes", string.Empty);
            string clipsDir = Path.GetDirectoryName(Path.GetDirectoryName(fbxPath)).Replace('\\', '/') + "/Clips";
            if (!Directory.Exists(clipsDir))
            {
                continue;
            }

            // Memes reglages que les autres modeles de la race, sinon les translations d'os
            // sortiraient a une autre echelle que les clips deja en place.
            // Qualifie : le projet a son propre AssetImporter (outils de terrain).
            var importer = (ModelImporter)UnityEditor.AssetImporter.GetAtPath(fbxPath);
            if (importer != null && (!Mathf.Approximately(importer.globalScale, RaceScale) || importer.useFileScale))
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.globalScale = RaceScale;
                importer.useFileScale = false;
                importer.importAnimation = true;
                importer.SaveAndReimport();
            }

            foreach (AnimationClip source in AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>())
            {
                if (source.name.StartsWith("__preview__"))
                {
                    continue;
                }

                string sequence = source.name;
                int bar = sequence.LastIndexOf('|');
                if (bar >= 0)
                {
                    sequence = sequence.Substring(bar + 1);
                }

                string clipPath = $"{clipsDir}/{mesh}.ao_{sequence}.anim";
                if (File.Exists(clipPath))
                {
                    continue;
                }

                var clip = new AnimationClip();
                EditorUtility.CopySerialized(source, clip);
                clip.name = Path.GetFileNameWithoutExtension(clipPath);
                AssetDatabase.CreateAsset(clip, clipPath);
                created++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Emotes] {created} clip(s) extrait(s) du client.");
    }

    [MenuItem("L2/Animations/Brancher les emotes des joueurs")]
    public static void Bind()
    {
        int bound = 0;
        var missing = new List<string>();

        foreach (string file in Directory.GetFiles(PlayerContainers, "*_Social.asset"))
        {
            string assetPath = file.Replace('\\', '/');
            string race = Path.GetFileNameWithoutExtension(assetPath).Replace("_Social", string.Empty);
            var container = AssetDatabase.LoadAssetAtPath<L2HumanoidAnimationContainerSocial>(assetPath);
            if (container == null || string.IsNullOrEmpty(race))
            {
                continue;
            }

            var serialized = new SerializedObject(container);
            SerializedProperty animations = serialized.FindProperty("_animations");

            // Les conteneurs sur disque datent d'avant l'ajout des emotes 14 a 21.
            int slots = Enum.GetValues(typeof(HumanoidAnimationSocialEvent)).Length;
            if (animations.arraySize < slots)
            {
                animations.arraySize = slots;
                for (int i = 0; i < slots; i++)
                {
                    animations.GetArrayElementAtIndex(i).FindPropertyRelative("_event").enumValueIndex = i;
                }
            }

            foreach (KeyValuePair<int, string> entry in Clips)
            {
                if (entry.Key >= animations.arraySize)
                {
                    continue;
                }

                AnimationClip clip = FindClip(race, entry.Value);
                if (clip == null)
                {
                    missing.Add($"{race}/{entry.Value}");
                    continue;
                }

                animations.GetArrayElementAtIndex(entry.Key).FindPropertyRelative("_clip").objectReferenceValue = clip;
                bound++;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(container);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Emotes] {bound} clip(s) branche(s).");
        if (missing.Count > 0)
        {
            Debug.LogWarning($"[Emotes] Introuvables : {string.Join(", ", missing)}");
        }
    }

    // Les clips sont nommes "<race>_m000_b.ao_<clip>_<race>.001" : on compare la partie
    // qui suit "ao_", et on exige le souligne pour ne pas confondre Cheer et cheer02.
    private static AnimationClip FindClip(string race, string name)
    {
        foreach (string guid in AssetDatabase.FindAssets($"t:AnimationClip {name}", new[] { AnimationsRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.IndexOf("/" + race + "/", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            string file = Path.GetFileNameWithoutExtension(path);
            int ao = file.IndexOf("ao_", StringComparison.OrdinalIgnoreCase);
            string clipName = ao >= 0 ? file.Substring(ao + 3) : file;
            if (clipName.StartsWith(name + "_", StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            }
        }

        return null;
    }
}
#endif
