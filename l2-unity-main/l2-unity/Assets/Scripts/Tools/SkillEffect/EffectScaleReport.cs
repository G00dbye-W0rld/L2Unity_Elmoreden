#if (UNITY_EDITOR)
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Mesure les maillages employes par les MeshEmitter des recettes. Le chemin
// sprite applique une constante de calibration, le chemin mesh non : ce rapport
// sert a trancher quel facteur appliquer, sur des tailles reelles.
public class EffectScaleReport
{
    private const string RecipeFolder = "../EffectRecipes";

    [MenuItem("L2/Outils/SkillEffects - Rapport d'echelle des maillages")]
    public static void Report()
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, RecipeFolder));
        if (!Directory.Exists(folder))
        {
            Debug.LogError($"Dossier de recettes introuvable : {folder}");
            return;
        }

        Regex meshRe = new Regex(@"StaticMesh=StaticMesh'([^']+)'");
        Regex sizeRe = new Regex(@"StartSizeRange=\(X=\(Min=([-0-9.]+)");
        Regex scaleRe = new Regex(@"^\s*DrawScale=([-0-9.]+)", RegexOptions.Multiline);

        // Une ligne par maillage distinct, avec la plus grande taille finale rencontree.
        Dictionary<string, float> rawSize = new Dictionary<string, float>();
        Dictionary<string, float> worstNow = new Dictionary<string, float>();
        Dictionary<string, float> worstScaled = new Dictionary<string, float>();
        Dictionary<string, string> worstRecipe = new Dictionary<string, string>();
        int missing = 0;

        string[] files = Directory.GetFiles(folder, "*.uc");
        for (int f = 0; f < files.Length; f++)
        {
            string text = File.ReadAllText(files[f]);
            string recipe = Path.GetFileNameWithoutExtension(files[f]);

            Match ds = scaleRe.Match(text);
            float drawScale = ds.Success
                ? float.Parse(ds.Groups[1].Value, CultureInfo.InvariantCulture) : 1f;

            // Les emetteurs sont apparies dans l'ordre du fichier : un bloc Begin
            // Object porte au plus un StaticMesh et un StartSizeRange.
            foreach (string block in text.Split(new[] { "Begin Object" }, System.StringSplitOptions.None).Skip(1))
            {
                Match mm = meshRe.Match(block);
                if (!mm.Success) continue;

                string meshRef = mm.Groups[1].Value;
                Match sm = sizeRe.Match(block);
                float startSize = sm.Success
                    ? float.Parse(sm.Groups[1].Value, CultureInfo.InvariantCulture) : 1f;

                if (!rawSize.ContainsKey(meshRef))
                {
                    GameObject res = StaticMeshUtils.LoadMeshFromInfo(meshRef);
                    if (res == null) { missing++; rawSize[meshRef] = -1f; }
                    else
                    {
                        // sharedMesh.bounds est local : Renderer.bounds vaut zero
                        // sur un prefab qui n'est pas instancie dans une scene.
                        MeshFilter mf = res.GetComponentInChildren<MeshFilter>();
                        rawSize[meshRef] = (mf == null || mf.sharedMesh == null) ? -1f
                            : Mathf.Max(mf.sharedMesh.bounds.size.x,
                                        mf.sharedMesh.bounds.size.y,
                                        mf.sharedMesh.bounds.size.z);
                    }
                }

                float raw = rawSize[meshRef];
                if (raw < 0f) continue;

                float now = raw * startSize;
                if (!worstNow.ContainsKey(meshRef) || now > worstNow[meshRef])
                {
                    worstNow[meshRef] = now;
                    worstScaled[meshRef] = now * drawScale;
                    worstRecipe[meshRef] = $"{recipe} (size {startSize}, draw {drawScale})";
                }
            }
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Maillages distincts : {rawSize.Count}, introuvables : {missing}");
        sb.AppendLine("brut x taille | avec DrawScale | maillage | pire recette");
        foreach (KeyValuePair<string, float> kv in worstNow.OrderByDescending(k => k.Value))
        {
            sb.AppendLine($"{kv.Value,10:F2} | {worstScaled[kv.Key],8:F2} | {kv.Key} | {worstRecipe[kv.Key]}");
        }

        string outPath = Path.Combine(Path.GetTempPath(), "effect_scale_report.txt");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log($"Rapport d'echelle ecrit dans {outPath}\n" + sb.ToString());
    }
}
#endif
