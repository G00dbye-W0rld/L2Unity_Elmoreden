#if UNITY_EDITOR
using System.Collections.Generic;
using FMODUnity;
using UnityEditor;
using UnityEngine;

// Les evenements ont ete recrees dans FMOD Studio et ont change de GUID : les
// EventReference serialisees pointent vers des identifiants morts, d'ou
// "Event not found" en jeu alors que les banques sont bien construites.
//
// Les chemins, eux, sont intacts (event:/AmbSound/AmbSound/id_cicada_02
// correspond toujours a la hierarchie du projet FMOD). On repare donc le GUID
// a partir du chemin.
public static class L2FmodReferenceRepair
{
    private const string Root = "Assets/Resources";

    [MenuItem("L2/Outils/FMOD - reparer les references (rapport)", false, 540)]
    private static void Report()
    {
        Run(false);
    }

    [MenuItem("L2/Outils/FMOD - reparer les references (appliquer)", false, 541)]
    private static void Apply()
    {
        if (EditorUtility.DisplayDialog("Reparer les references FMOD",
                "Les prefabs concernes vont etre reecrits sur disque.\n\n"
                + "Lancez le rapport d'abord si ce n'est pas fait.", "Reparer", "Annuler"))
        {
            Run(true);
        }
    }

    private static void Run(bool write)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { Root });

        var missing = new Dictionary<string, int>();
        int scanned = 0, repaired = 0, assets = 0, ok = 0;

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                EditorUtility.DisplayProgressBar("References FMOD", path, (float)i / guids.Length);

                GameObject root = write
                    ? PrefabUtility.LoadPrefabContents(path)
                    : AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (root == null)
                {
                    continue;
                }

                int n = Repair(root, write, missing, ref scanned, ref ok);

                if (write)
                {
                    if (n > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }

                    PrefabUtility.UnloadPrefabContents(root);
                }

                if (n > 0)
                {
                    repaired += n;
                    assets++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (write)
        {
            AssetDatabase.SaveAssets();
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[FMOD] {(write ? "REPARE" : "RAPPORT")} - {scanned} reference(s) examinee(s), "
                      + $"{ok} deja bonne(s), {repaired} a reparer dans {assets} prefab(s), "
                      + $"{missing.Count} chemin(s) introuvable(s) dans le projet FMOD");

        foreach (KeyValuePair<string, int> m in missing)
        {
            sb.AppendLine($"   INTROUVABLE  x{m.Value}  {m.Key}");
        }

        Debug.Log(sb.ToString());
    }

    private static int Repair(GameObject root, bool write, Dictionary<string, int> missing,
        ref int scanned, ref int ok)
    {
        int count = 0;

        foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null)
            {
                continue;
            }

            var so = new SerializedObject(mb);
            SerializedProperty it = so.GetIterator();
            bool dirty = false;

            while (it.NextVisible(true))
            {
                if (it.type != "EventReference")
                {
                    continue;
                }

                SerializedProperty pathProp = it.FindPropertyRelative("Path");

                if (pathProp == null || string.IsNullOrEmpty(pathProp.stringValue))
                {
                    continue;
                }

                scanned++;

                EditorEventRef found = EventManager.EventFromPath(pathProp.stringValue);

                if (found == null)
                {
                    missing.TryGetValue(pathProp.stringValue, out int n);
                    missing[pathProp.stringValue] = n + 1;
                    continue;
                }

                SerializedProperty guid = it.FindPropertyRelative("Guid");

                if (guid == null || Matches(guid, found.Guid))
                {
                    ok++;
                    continue;
                }

                count++;

                if (!write)
                {
                    continue;
                }

                guid.FindPropertyRelative("Data1").intValue = found.Guid.Data1;
                guid.FindPropertyRelative("Data2").intValue = found.Guid.Data2;
                guid.FindPropertyRelative("Data3").intValue = found.Guid.Data3;
                guid.FindPropertyRelative("Data4").intValue = found.Guid.Data4;

                dirty = true;
            }

            if (write && dirty)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        return count;
    }

    private static bool Matches(SerializedProperty guid, FMOD.GUID other)
    {
        return guid.FindPropertyRelative("Data1").intValue == other.Data1
               && guid.FindPropertyRelative("Data2").intValue == other.Data2
               && guid.FindPropertyRelative("Data3").intValue == other.Data3
               && guid.FindPropertyRelative("Data4").intValue == other.Data4;
    }
}
#endif
