using System.Linq;
using UnityEditor;
using UnityEngine;

public class DiagnoseBoneMismatch
{
    // (raceFolder, typeName)
    static readonly (string raceFolder, string typeName)[] TypesToCheck = new[]
    {
        ("Orc", "MOrc"),
        ("Orc", "FOrc"),
        ("Shaman", "MShaman"),
        ("Shaman", "FShaman"),
        ("Magic", "FMagic"), // race de reference qui fonctionne, pour comparaison
    };
    static string BasePath => "Assets/Resources/Data/Animations";

    [MenuItem("Tools/L2Unity/Orc/Diagnose Bone Mismatch")]
    static void Diagnose()
    {
        foreach (var (cfgRaceFolder, typeName) in TypesToCheck)
        {
            string animPath = $"{BasePath}/{cfgRaceFolder}/{typeName}/{typeName}_Anim.prefab";
            GameObject animPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(animPath);
            if (animPrefab == null) continue;

            SkinnedMeshRenderer rootSmr = animPrefab.GetComponentInChildren<SkinnedMeshRenderer>();
            if (rootSmr == null)
            {
                Debug.LogWarning($"[Diagnose] {typeName}: pas de SkinnedMeshRenderer trouve dans {animPath}");
                continue;
            }

            Debug.Log($"[Diagnose] === {typeName} === Squelette de reference ({animPath}): {rootSmr.bones.Length} os. " +
                $"Premiers: {string.Join(", ", rootSmr.bones.Take(5).Select(b => b == null ? "NULL" : b.name))}");

            string modelsDir = $"{BasePath}/{cfgRaceFolder}/{typeName}/Models";
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { modelsDir });
            foreach (string guid in guids)
            {
                string fbxPath = AssetDatabase.GUIDToAssetPath(guid);
                if (fbxPath.Replace("\\", "/").Contains("/anim/")) continue;
                if (!fbxPath.ToLower().EndsWith(".fbx")) continue;

                GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                SkinnedMeshRenderer pieceSmr = fbxAsset != null ? fbxAsset.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                Mesh mesh = pieceSmr != null ? pieceSmr.sharedMesh : null;

                if (mesh == null)
                {
                    MeshFilter mf = fbxAsset != null ? fbxAsset.GetComponentInChildren<MeshFilter>() : null;
                    mesh = mf != null ? mf.sharedMesh : null;
                }

                if (mesh == null)
                {
                    Debug.LogWarning($"[Diagnose]   {System.IO.Path.GetFileName(fbxPath)}: aucun mesh trouve.");
                    continue;
                }

                int bindposeCount = mesh.bindposeCount;
                int pieceBoneArrayLength = pieceSmr != null ? pieceSmr.bones.Length : -1;

                string flag = bindposeCount != rootSmr.bones.Length ? "  <-- MISMATCH" : "";
                Debug.Log($"[Diagnose]   {System.IO.Path.GetFileName(fbxPath)}: bindposes={bindposeCount}, bones(smr)={pieceBoneArrayLength}{flag}");
            }
        }
    }
}
