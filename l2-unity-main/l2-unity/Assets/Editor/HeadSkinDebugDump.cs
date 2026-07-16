using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Diagnostic runtime du decalage tete/visage Orc/Shaman.
//
// ACTION PRINCIPALE : selectionner le pawn concerne (ex: Pawn21) en Play mode
// (pause OK) puis Tools > L2Unity > Debug > Dump + Bake (selection).
// Au MEME instant :
//  1. calcule, pour CHAQUE os partage (par nom) entre chaque piece et la
//     reference (_u), la matrice de skin M = bone.localToWorldMatrix * bindpose,
//     et rapporte l'os au delta maximal (position + angle) -> HeadSkinDump.txt ;
//  2. exporte chaque renderer en OBJ via BakeMesh (geometrie exactement telle
//     que rendue) -> BakedObj/.
// Si le bake montre un decalage, le dump du meme instant DOIT montrer quel os
// de quelle piece diverge, et de combien.
public static class HeadSkinDebugDump
{
    [MenuItem("Tools/L2Unity/Debug/Dump + Bake (selection)")]
    static void DumpAndBake()
    {
        Transform sel = Selection.activeTransform;
        if (sel == null)
        {
            Debug.LogWarning("[HeadSkinDebug] Selectionne le pawn (ex: Pawn21) dans la hierarchie.");
            return;
        }

        var report = new StringBuilder();
        report.AppendLine($"[HeadSkinDebug] DUMP+BAKE atomique  personnage='{GetPath(sel, null)}'  t={Time.time:F2}");

        var renderers = sel.GetComponentsInChildren<SkinnedMeshRenderer>(false);
        SkinnedMeshRenderer reference = null;
        foreach (var r in renderers)
        {
            string n = r.name.Replace("(Clone)", "");
            if (n.EndsWith("_u")) { reference = r; break; }
        }
        if (reference == null && renderers.Length > 0) reference = renderers[0];

        // matrices de la reference, par nom d'os normalise
        var refMats = AllBoneMats(reference);

        foreach (var r in renderers)
        {
            Mesh mesh = r.sharedMesh;
            report.AppendLine($"--- {r.name}  mesh='{(mesh ? mesh.name : "NULL")}' (meshID={(mesh ? mesh.GetInstanceID() : 0)})  bones={r.bones?.Length ?? 0}  bindposes={(mesh ? mesh.bindposes.Length : 0)}");
            // une piece jamais synchronisee garde le transform du prefab (-90 deg X, scale 100)
            report.AppendLine($"    transform local: pos={r.transform.localPosition.ToString("F3")} rot={r.transform.localRotation.eulerAngles.ToString("F1")} scale={r.transform.localScale.ToString("F2")}  activeRenderer={r.enabled}");
            if (mesh == null || r.bones == null || r == reference) continue;

            int nulls = 0;
            foreach (var b in r.bones) if (b == null) nulls++;
            if (nulls > 0) report.AppendLine($"    !!! {nulls} os NULL dans renderer.bones");

            var mats = AllBoneMats(r);
            float worstPos = 0f, worstAng = 0f;
            string worstBone = "-";
            Matrix4x4 worstM = Matrix4x4.identity, worstRef = Matrix4x4.identity;
            foreach (var kv in mats)
            {
                if (!refMats.TryGetValue(kv.Key, out Matrix4x4 Mref)) continue;
                float dPos = ((Vector3)(Mref.GetColumn(3) - kv.Value.GetColumn(3))).magnitude;
                float dAng = Quaternion.Angle(Mref.rotation, kv.Value.rotation);
                if (dPos + dAng * 0.001f > worstPos + worstAng * 0.001f)
                {
                    worstPos = dPos; worstAng = dAng; worstBone = kv.Key;
                    worstM = kv.Value; worstRef = Mref;
                }
            }
            report.AppendLine($"    pire os partage: '{worstBone}'  dPos={worstPos:F4}  dAngle={worstAng:F2} deg");
            foreach (string key in new[] { "bip01_neck", "bip01_head", "bip01_headnub" })
            {
                if (mats.TryGetValue(key, out Matrix4x4 M) && refMats.TryGetValue(key, out Matrix4x4 Mref))
                {
                    Vector3 d = Mref.GetColumn(3) - M.GetColumn(3);
                    report.AppendLine($"    {key,-14} dPos={d.ToString("F4")} (|{d.magnitude:F4}|)  dAngle={Quaternion.Angle(Mref.rotation, M.rotation):F2} deg  scale={M.lossyScale.ToString("F4")} refScale={Mref.lossyScale.ToString("F4")}");
                    if (d.magnitude > 1e-4f)
                    {
                        Matrix4x4 C = M.inverse * Mref;
                        report.AppendLine($"      -> correctif : pos={((Vector3)C.GetColumn(3)).ToString("F5")} euler={C.rotation.eulerAngles.ToString("F2")}");
                    }
                }
            }
        }

        // Matrices MONDE des os tete/cou du squelette de reference a cet instant :
        // necessaires pour convertir un ecart mesure en monde (sur les OBJ bakes)
        // en valeurs de correctif exprimees dans l'espace local de l'os.
        if (reference != null && reference.bones != null)
        {
            foreach (var bone in reference.bones)
            {
                if (bone == null) continue;
                string key = bone.name.Replace(' ', '_').ToLowerInvariant();
                if (key != "bip01_head" && key != "bip01_neck") continue;
                Vector3 p = sel.InverseTransformPoint(bone.position);
                Quaternion q = Quaternion.Inverse(sel.rotation) * bone.rotation;
                report.AppendLine($"OS {key}: pos_perso={p.ToString("F4")}  rot_perso(euler)={q.eulerAngles.ToString("F2")}  right={(Quaternion.Inverse(sel.rotation) * bone.right).ToString("F3")}  up={(Quaternion.Inverse(sel.rotation) * bone.up).ToString("F3")}  fwd={(Quaternion.Inverse(sel.rotation) * bone.forward).ToString("F3")}");
            }
        }

        // pieces rendues par MeshRenderer simple (ex: cheveux rigides parentes sous
        // l'os de la tete) - invisibles pour l'analyse SMR ci-dessus mais bien a l'ecran
        foreach (var mr in sel.GetComponentsInChildren<MeshRenderer>(false))
        {
            var mf = mr.GetComponent<MeshFilter>();
            report.AppendLine($"--- [MeshRenderer] {mr.name}  tag={mr.tag}  mesh='{(mf && mf.sharedMesh ? mf.sharedMesh.name : "NULL")}'  parent='{GetPath(mr.transform.parent, sel)}'");
            report.AppendLine($"    transform local: pos={mr.transform.localPosition.ToString("F3")} rot={mr.transform.localRotation.eulerAngles.ToString("F1")} scale={mr.transform.localScale.ToString("F2")}  enabled={mr.enabled}");
        }

        var sync = sel.GetComponentInChildren<SkinnedMeshSync>(true);
        if (sync != null)
        {
            var so = new SerializedObject(sync);
            report.AppendLine($"SkinnedMeshSync sur '{GetPath(sync.transform, sel)}': headCorrectionEuler={so.FindProperty("_headCorrectionEuler")?.vector3Value.ToString("F3")}  headCorrectionPosition={so.FindProperty("_headCorrectionPosition")?.vector3Value.ToString("F5")}");
        }

        // ---- bake atomique ----
        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "BakedObj");
        Directory.CreateDirectory(dir);
        Matrix4x4 toLocal = sel.worldToLocalMatrix;
        foreach (var r in renderers)
        {
            if (r.sharedMesh == null) continue;
            var baked = new Mesh();
            r.BakeMesh(baked, true);
            Matrix4x4 m = toLocal * r.transform.localToWorldMatrix;
            var sb = new StringBuilder();
            foreach (var v in baked.vertices)
            {
                Vector3 w = m.MultiplyPoint3x4(v);
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", w.x, w.y, w.z));
            }
            for (int s = 0; s < baked.subMeshCount; s++)
            {
                int[] tris = baked.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                    sb.AppendLine($"f {tris[i] + 1} {tris[i + 1] + 1} {tris[i + 2] + 1}");
            }
            File.WriteAllText(Path.Combine(dir, $"{sel.name}_{r.name.Replace("(Clone)", "")}.obj"), sb.ToString());
            Object.DestroyImmediate(baked);
        }
        // bake des MeshRenderer simples (geometrie statique, transformee par leur transform)
        foreach (var mr in sel.GetComponentsInChildren<MeshRenderer>(false))
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Matrix4x4 m = toLocal * mr.transform.localToWorldMatrix;
            var sb = new StringBuilder();
            foreach (var v in mf.sharedMesh.vertices)
            {
                Vector3 w = m.MultiplyPoint3x4(v);
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", w.x, w.y, w.z));
            }
            for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
            {
                int[] tris = mf.sharedMesh.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                    sb.AppendLine($"f {tris[i] + 1} {tris[i + 1] + 1} {tris[i + 2] + 1}");
            }
            File.WriteAllText(Path.Combine(dir, $"{sel.name}_MR_{mr.name.Replace("(Clone)", "")}.obj"), sb.ToString());
        }

        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HeadSkinDump.txt");
        File.WriteAllText(path, report.ToString());
        Debug.Log($"[HeadSkinDebug] rapport: {path}  +  OBJ dans {dir}\n" + report.ToString());
    }

    static Dictionary<string, Matrix4x4> AllBoneMats(SkinnedMeshRenderer r)
    {
        var result = new Dictionary<string, Matrix4x4>();
        if (r == null || r.sharedMesh == null || r.bones == null) return result;
        Matrix4x4[] bindposes = r.sharedMesh.bindposes;
        int count = Mathf.Min(r.bones.Length, bindposes.Length);
        for (int i = 0; i < count; i++)
        {
            Transform bone = r.bones[i];
            if (bone == null) continue;
            string key = bone.name.Replace(' ', '_').ToLowerInvariant();
            if (!result.ContainsKey(key))
                result[key] = bone.localToWorldMatrix * bindposes[i];
        }
        return result;
    }

    static string GetPath(Transform t, Transform stopAt)
    {
        var parts = new List<string>();
        while (t != null && t != stopAt) { parts.Add(t.name); t = t.parent; }
        parts.Reverse();
        return string.Join("/", parts);
    }
}
