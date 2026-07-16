using System.Linq;
using UnityEditor;
using UnityEngine;

// Diagnostique a quel(s) os un mesh de piece est reellement pondere (skin weights),
// par opposition a quel os est simplement present dans son tableau bones[]/bindposes.
// Utile pour verifier si un decalage visuel vient d'une pondaration de sommets qui
// pointe vers un index d'os different de celui attendu (bug de donnees d'export),
// independamment du remapping par nom fait par SkinnedMeshSync.
public class DiagnoseBoneWeights
{
    [MenuItem("Tools/L2Unity/Orc/Diagnose Bone Weights (MOrc face)")]
    static void DiagnoseFace()
    {
        DiagnoseOne("Assets/Resources/Data/Animations/Orc/MOrc/Models/MOrc_m000_f.fbx");
    }

    [MenuItem("Tools/L2Unity/Orc/Diagnose Bone Weights (MOrc ah hair)")]
    static void DiagnoseHair()
    {
        DiagnoseOne("Assets/Resources/Data/Animations/Orc/MOrc/Models/MOrc_m000_m00_ah.fbx");
    }

    [MenuItem("Tools/L2Unity/Orc/Diagnose Bone Weights (MOrc body b)")]
    static void DiagnoseBody()
    {
        DiagnoseOne("Assets/Resources/Data/Animations/Orc/MOrc/Models/MOrc_m000_b.fbx");
    }

    static void DiagnoseOne(string fbxPath)
    {
        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbxAsset == null)
        {
            Debug.LogError($"[DiagnoseBoneWeights] Introuvable: {fbxPath}");
            return;
        }

        SkinnedMeshRenderer smr = fbxAsset.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null || smr.sharedMesh == null)
        {
            Debug.LogError($"[DiagnoseBoneWeights] Pas de SkinnedMeshRenderer/mesh dans {fbxPath}");
            return;
        }

        Mesh mesh = smr.sharedMesh;
        Transform[] bones = smr.bones;
        BoneWeight[] weights = mesh.boneWeights;

        if (weights == null || weights.Length == 0)
        {
            Debug.LogWarning($"[DiagnoseBoneWeights] {fbxPath}: mesh.boneWeights est vide (peut-être >4 influences/vertex, verifier avec GetAllBoneWeights). bones.Length={bones.Length}");
            return;
        }

        double[] totalWeightPerBone = new double[bones.Length];
        foreach (BoneWeight bw in weights)
        {
            if (bw.boneIndex0 >= 0 && bw.boneIndex0 < bones.Length) totalWeightPerBone[bw.boneIndex0] += bw.weight0;
            if (bw.boneIndex1 >= 0 && bw.boneIndex1 < bones.Length) totalWeightPerBone[bw.boneIndex1] += bw.weight1;
            if (bw.boneIndex2 >= 0 && bw.boneIndex2 < bones.Length) totalWeightPerBone[bw.boneIndex2] += bw.weight2;
            if (bw.boneIndex3 >= 0 && bw.boneIndex3 < bones.Length) totalWeightPerBone[bw.boneIndex3] += bw.weight3;
        }

        var ranked = Enumerable.Range(0, bones.Length)
            .OrderByDescending(i => totalWeightPerBone[i])
            .Take(10)
            .Where(i => totalWeightPerBone[i] > 0);

        Debug.Log($"[DiagnoseBoneWeights] === {fbxPath} === {weights.Length} vertices, {bones.Length} os. Top os par poids cumule:");
        foreach (int i in ranked)
        {
            string name = bones[i] != null ? bones[i].name : "NULL";
            Debug.Log($"[DiagnoseBoneWeights]   index {i} '{name}': poids cumule = {totalWeightPerBone[i]:F2}");
        }
    }
}
