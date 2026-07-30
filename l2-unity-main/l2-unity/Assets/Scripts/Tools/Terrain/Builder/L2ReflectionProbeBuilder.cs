#if (UNITY_EDITOR)
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class L2ReflectionProbeBuilder
{
    // NON appelee par L2MapBatchImporter.RunImport : verifie sur les 4
    // regions de reference (16_24, 16_25, 17_24, 17_25), 3 sur 4 n'ont NI
    // Light NI ReflectionProbe du tout. Seule 17_25 en a (5 sondes posees a
    // la main pres de points d'interet precis - l'eglise notamment - avec
    // des tailles de boite ajustees au cas par cas). Une grille automatique
    // ne correspond donc pas a la convention majoritaire des regions
    // "propres" ; cet outil reste disponible en manuel (menu ci-dessous)
    // pour une region ou l'on juge, au cas par cas, qu'elle apporterait
    // quelque chose - a utiliser avec discernement, pas par defaut.
    private const float GridSpacing = 40f;
    private const float ProbeHeightAboveGround = 8f;
    private static readonly Vector3 ProbeBoxSize = new Vector3(45f, 30f, 45f);

    [MenuItem("Shnok/[Debug][Light] (Terrain) Build reflection probe grid")]
    static void BuildGridMenu()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || selected.GetComponent<Terrain>() == null)
        {
            Debug.LogError("[ReflectionProbe] Selectionnez l'objet Terrain de la region dans la Hierarchy.");
            return;
        }

        BuildGridFor(selected.name);
    }

    /// Etape Phase 2 sans dialogue. Voir L2MapBatchImporter.
    public static void BuildGridFor(string mapName)
    {
        GameObject terrainObject = GameObject.Find(mapName);
        Terrain terrain = terrainObject != null ? terrainObject.GetComponent<Terrain>() : null;
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogError($"[ReflectionProbe] Terrain '{mapName}' introuvable ou invalide dans la scene.");
            return;
        }

        int removed = L2TerrainGenerator.DestroyContainers("ReflectionProbes");
        if (removed > 0)
        {
            Debug.Log($"[ReflectionProbe] {removed} conteneur(s) precedent(s) supprime(s) avant regeneration.");
        }

        GameObject container = new GameObject("ReflectionProbes");

        Vector3 size = terrain.terrainData.size;
        Vector3 origin = terrainObject.transform.position;

        int columns = Mathf.Max(1, Mathf.RoundToInt(size.x / GridSpacing));
        int rows = Mathf.Max(1, Mathf.RoundToInt(size.z / GridSpacing));

        int created = 0;
        for (int i = 0; i <= columns; i++)
        {
            for (int j = 0; j <= rows; j++)
            {
                float localX = Mathf.Min(i * GridSpacing, size.x);
                float localZ = Mathf.Min(j * GridSpacing, size.z);
                float worldX = origin.x + localX;
                float worldZ = origin.z + localZ;

                // SampleHeight rend une hauteur relative a l'origine du
                // Terrain, pas une position monde absolue - cf. doc Unity.
                float groundY = origin.y + terrain.SampleHeight(new Vector3(worldX, 0f, worldZ));

                GameObject probeObject = new GameObject($"Probe_{i}_{j}");
                probeObject.transform.parent = container.transform;
                probeObject.transform.position = new Vector3(worldX, groundY + ProbeHeightAboveGround, worldZ);

                ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Baked;
                probe.size = ProbeBoxSize;
                probe.resolution = 256;
                probe.hdr = true;
                probe.boxProjection = true;
                probe.nearClipPlane = 0.01f;
                probe.farClipPlane = 100f;

                created++;
            }
        }

        Debug.Log($"[ReflectionProbe] {created} sonde(s) posee(s) en grille ({columns + 1}x{rows + 1}, "
                  + $"espacement {GridSpacing}). Baked : lancez Window > Rendering > Lighting > "
                  + "'Generate Lighting' pour les calculer, ce n'est pas fait automatiquement.");
    }
}
#endif
