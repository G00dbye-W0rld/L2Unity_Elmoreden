#if (UNITY_EDITOR)
using UnityEditor;
using UnityEngine;

/// Le "Safenet" est un plan (mesh Plane par defaut d'Unity, invisible,
/// collider plein non-trigger) qui empeche de tomber dans le vide sous la
/// region. Present et strictement identique (echelle 62.41525, position
/// locale ~(312.08, 0, 312.08)) sur les 4 regions de reference (16_24, 16_25,
/// 17_24, 17_25) - un objet fixe a cloner tel quel, comme Water.
public class L2SafenetBuilder
{
    [MenuItem("Shnok/[Debug][Safenet] Build safenet")]
    static void BuildSafenetMenu()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || selected.GetComponent<Terrain>() == null)
        {
            Debug.LogError("[Safenet] Selectionnez l'objet Terrain de la region dans la Hierarchy.");
            return;
        }

        BuildSafenetFor(selected.name);
    }

    /// Etape Phase 2 sans dialogue. Voir L2MapBatchImporter.
    public static void BuildSafenetFor(string mapName)
    {
        GameObject terrainObject = GameObject.Find(mapName);
        if (terrainObject == null)
        {
            Debug.LogError($"[Safenet] Terrain '{mapName}' introuvable dans la scene.");
            return;
        }

        GameObject referenceSafenet = L2WaterBuilder.FindReferenceChild("Safenet");
        if (referenceSafenet == null)
        {
            Debug.LogError("[Safenet] Objet 'Safenet' introuvable dans le prefab de reference.");
            return;
        }

        Transform existing = terrainObject.transform.Find("Safenet");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject safenet = L2WaterBuilder.CloneWithOriginalLocalTransform(referenceSafenet, terrainObject.transform);
        safenet.name = "Safenet";

        Debug.Log($"[Safenet] '{mapName}' : filet de securite clone "
                  + $"(position locale {safenet.transform.localPosition}, echelle {safenet.transform.localScale}).");
    }
}
#endif
