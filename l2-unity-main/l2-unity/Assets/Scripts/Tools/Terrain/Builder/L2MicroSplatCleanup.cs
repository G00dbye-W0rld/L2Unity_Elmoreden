#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// Supprime les assets MicroSplat par region devenus inutiles apres la
/// mutualisation.
///
/// CE QUI EST DEVENU ORPHELIN
/// Depuis que chaque region pointe sur le materiau maitre, sa propre config,
/// ses texture arrays, son materiau et son shader ne sont plus atteints par
/// rien. Ils restent pourtant embarques dans le build : tout ce qui vit sous
/// Resources/ y est inclus INCONDITIONNELLEMENT, reference ou non. D'ou ~2,4 Go
/// de poids mort.
///
/// CE QU'IL NE FAUT PAS SUPPRIMER
/// Trois fichiers restent references par le MicroSplatTerrain de chaque region
/// et par le terrain lui-meme :
///
///     MicroSplat_propdata.asset   reglages par texture de la region
///     MicroSplat_keywords.asset   fonctionnalites du shader
///     MicroSplat_Base.shader      shader de basemap, utilise au-dela de
///                                 basemapDistance - le supprimer casse le
///                                 rendu lointain
///
/// POURQUOI VERIFIER REGION PAR REGION
/// Le motif n'est pas uniforme. Mesure du 2026-08-16 : 24_18 reference encore
/// son ancien MicroSplat.mat depuis son PREFAB (sa scene, elle, pointe bien sur
/// le maitre). Une suppression en bloc casserait ce prefab. On ne supprime donc
/// qu'apres avoir constate l'absence de reference, fichier par fichier.
public static class L2MicroSplatCleanup
{
    private const string MapsFolder = "Assets/Resources/Data/Maps";
    private const string ScenesFolder = "Assets/Resources/Scenes";

    private static readonly string[] ReferenceRegions = { "16_24", "16_25", "17_24", "17_25" };

    /// Candidats a la suppression, par nom de fichier dans MicroSplatData/.
    /// Tout ce qui n'est pas dans cette liste est conserve d'office.
    private static readonly string[] Candidates =
    {
        "MicroSplatConfig.asset",
        "MicroSplatConfig_diff_tarray.asset",
        "MicroSplatConfig_normSAO_tarray.asset",
        "MicroSplatConfig_specular_tarray.asset",
        "MicroSplat.mat",
        "MicroSplat.shader"
    };

    [MenuItem("L2/Terrain/Mutualisation/8. Analyser les assets orphelins (aucune suppression)", false, 192)]
    public static void Analyse()
    {
        Run(false);
    }

    [MenuItem("L2/Terrain/Mutualisation/9. SUPPRIMER les assets orphelins", false, 193)]
    public static void Delete()
    {
        if (!EditorUtility.DisplayDialog("Supprimer les assets MicroSplat orphelins",
                "Seuls les fichiers dont AUCUNE reference n'a ete trouvee seront supprimes.\n\n"
                + "Talking Island est exclue.\n\n"
                + "Sauvegarde attendue : _backup_maps_20260816_postmutualisation\n\n"
                + "Continuer ?",
                "Supprimer", "Annuler"))
        {
            return;
        }

        Run(true);
    }

    private static void Run(bool actuallyDelete)
    {
        string[] regions = EnumerateRegions()
            .Where(r => !ReferenceRegions.Contains(r))
            .ToArray();

        long freed = 0;
        int removed = 0, kept = 0;
        var keptDetail = new List<string>();

        for (int i = 0; i < regions.Length; i++)
        {
            if (EditorUtility.DisplayCancelableProgressBar(
                    actuallyDelete ? "Suppression" : "Analyse",
                    $"{regions[i]} ({i + 1}/{regions.Length})", (float)i / regions.Length))
            {
                break;
            }

            string folder = $"{MapsFolder}/{regions[i]}/TerrainData/MicroSplatData";
            if (!Directory.Exists(folder))
            {
                continue;
            }

            string prefab = ReadIfExists($"{MapsFolder}/{regions[i]}/{regions[i]}.prefab");
            string scene = ReadIfExists($"{ScenesFolder}/{regions[i]}.unity");

            foreach (string candidate in Candidates)
            {
                string path = $"{folder}/{candidate}";
                if (!File.Exists(path))
                {
                    continue;
                }

                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                // Une seule occurrence suffit a interdire la suppression.
                bool referenced = (prefab != null && prefab.Contains(guid))
                                  || (scene != null && scene.Contains(guid));

                if (referenced)
                {
                    kept++;
                    keptDetail.Add($"{regions[i]}/{candidate}");
                    continue;
                }

                long size = new FileInfo(path).Length;

                if (actuallyDelete)
                {
                    if (!AssetDatabase.DeleteAsset(path))
                    {
                        Debug.LogWarning($"[Nettoyage] Suppression refusee : {path}");
                        continue;
                    }
                }

                freed += size;
                removed++;
            }
        }

        EditorUtility.ClearProgressBar();

        if (actuallyDelete)
        {
            AssetDatabase.Refresh();
        }

        string verb = actuallyDelete ? "supprimes" : "supprimables";
        var report = new System.Text.StringBuilder();
        report.AppendLine($"[Nettoyage] {removed} fichier(s) {verb}, "
                          + $"{freed / (1024 * 1024)} Mo, sur {regions.Length} region(s).");

        if (kept > 0)
        {
            report.AppendLine($"  {kept} conserve(s) car encore reference(s) :");
            foreach (string k in keptDetail.Take(12))
            {
                report.AppendLine($"    {k}");
            }
            if (keptDetail.Count > 12)
            {
                report.AppendLine($"    ... et {keptDetail.Count - 12} autre(s)");
            }
        }

        Debug.Log(report.ToString());
    }

    private static string ReadIfExists(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static string[] EnumerateRegions()
    {
        if (!Directory.Exists(MapsFolder))
        {
            return new string[0];
        }

        return Directory.GetDirectories(MapsFolder)
            .Select(Path.GetFileName)
            .Where(n => Regex.IsMatch(n, @"^\d+_\d+$"))
            .OrderBy(n => n, System.StringComparer.Ordinal)
            .ToArray();
    }
}
#endif
