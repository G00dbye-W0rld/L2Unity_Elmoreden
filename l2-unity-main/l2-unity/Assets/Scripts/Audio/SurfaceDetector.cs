using System.Collections.Generic;
using UnityEngine;

public class SurfaceDetector : MonoBehaviour
{
    [SerializeField] private ObjectData _surfaceObject;

    // Le sol naturel n'a pas de tag : c'est la couche de terrain dominante sous
    // le joueur qui donne la matiere. Les 28 couches sont partagees par toutes
    // les regions, mais on classe par NOM et non par indice - l'ordre a deja
    // change une fois pendant la mutualisation MicroSplat.
    private static readonly Dictionary<TerrainData, string[]> _layerTags
        = new Dictionary<TerrainData, string[]>();

    public string GetSurfaceTag()
    {
        if (World.Instance == null)
        {
            return "stone";
        }

        if (!Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down,
                out var hit, 2f, World.Instance.GroundMask))
        {
            return "Dirt";
        }

        Terrain terrain = hit.collider.GetComponent<Terrain>();

        if (terrain != null)
        {
            return TerrainTag(terrain, hit.point);
        }

        _surfaceObject = new ObjectData(hit.collider.gameObject);

        return _surfaceObject.ObjectTag;
    }

    private static string TerrainTag(Terrain terrain, Vector3 point)
    {
        TerrainData data = terrain.terrainData;

        if (data == null || data.alphamapLayers == 0)
        {
            return "Dirt";
        }

        Vector3 local = point - terrain.transform.position;

        int x = Mathf.Clamp(
            Mathf.FloorToInt(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
        int z = Mathf.Clamp(
            Mathf.FloorToInt(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);

        float[,,] weights = data.GetAlphamaps(x, z, 1, 1);

        int best = 0;
        float bestWeight = -1f;

        for (int i = 0; i < data.alphamapLayers; i++)
        {
            if (weights[0, 0, i] > bestWeight)
            {
                bestWeight = weights[0, 0, i];
                best = i;
            }
        }

        return LayerTags(data)[best];
    }

    private static string[] LayerTags(TerrainData data)
    {
        if (_layerTags.TryGetValue(data, out string[] tags))
        {
            return tags;
        }

        TerrainLayer[] layers = data.terrainLayers;
        tags = new string[Mathf.Max(data.alphamapLayers, layers.Length)];

        for (int i = 0; i < tags.Length; i++)
        {
            tags[i] = Classify(i < layers.Length && layers[i] != null ? layers[i].name : null);
        }

        _layerTags[data] = tags;

        return tags;
    }

    // L'utilisateur ne veut distinguer que l'herbe et le sable ; roche, neige et
    // graviers restent en terre tant qu'aucun son ne leur correspond.
    private static string Classify(string layerName)
    {
        if (string.IsNullOrEmpty(layerName))
        {
            return "Dirt";
        }

        string n = layerName.ToLowerInvariant();

        if (n.Contains("grass") || n.Contains("moss"))
        {
            return "Grass";
        }

        if (n.Contains("sand") || n.Contains("beach"))
        {
            return "Sand";
        }

        return "Dirt";
    }
}
