#if (UNITY_EDITOR)
using UnityEditor;
using UnityEngine;

public class L2WaterBuilder
{
    // La structure du plugin StylisedWater (composants + materiau) est trop
    // specifique pour etre recreee a la main sans risque : on clone plutot
    // l'objet "Water" d'une region de reference deja fonctionnelle, comme le
    // recommandait deja le tuto pour un ajout manuel region par region.
    private const string ReferenceRegion = "17_25";

    [MenuItem("L2/Debug/Water - Build water", false, 905)]
    static void BuildWaterMenu()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || selected.GetComponent<Terrain>() == null)
        {
            Debug.LogError("[Water] Selectionnez l'objet Terrain de la region dans la Hierarchy.");
            return;
        }

        BuildWaterFrom(selected.name);
    }

    /// Etape Phase 2 sans dialogue. Voir L2MapBatchImporter.
    ///
    /// Verifie sur les 4 regions de reference (16_24, 16_25, 17_24, 17_25) :
    /// l'objet "Water" y a EXACTEMENT la meme echelle locale (104.2, 0.1,
    /// 104.2) et la meme position X/Z (52.1, 52.1), seul le Y variant a peine
    /// (109.9 a 110.2). Ce n'est donc PAS une dalle mise a l'echelle du
    /// terrain de chaque region (ce que la premiere version de cette methode
    /// faisait a tort, produisant une eau bien trop grande sur 17_23) : c'est
    /// un objet FIXE, clone tel quel d'une region a l'autre. Le WaterVolume
    /// du .unr n'est donc pas utilise ici - sa Location ne correspondait pas
    /// non plus a la hauteur reelle observee.
    public static void BuildWaterFrom(string mapName)
    {
        GameObject terrainObject = GameObject.Find(mapName);
        if (terrainObject == null)
        {
            Debug.LogError($"[Water] Terrain '{mapName}' introuvable dans la scene.");
            return;
        }

        GameObject referenceWater = FindReferenceChild("Water");
        if (referenceWater == null)
        {
            Debug.LogError($"[Water] Objet 'Water' introuvable dans le prefab de reference {ReferenceRegion}.");
            return;
        }

        Transform existing = terrainObject.transform.Find("Water");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject water = CloneWithOriginalLocalTransform(referenceWater, terrainObject.transform);
        water.name = "Water";

        Debug.Log($"[Water] '{mapName}' : plan d'eau clone de '{ReferenceRegion}' "
                  + $"(position locale {water.transform.localPosition}, echelle {water.transform.localScale}) "
                  + "- verifiez visuellement, un ajustement fin (notamment en X/Z selon la position "
                  + "de la region dans la grille) peut rester necessaire.");
    }

    /// Cherche un enfant nomme sous le prefab racine d'une region de
    /// reference (utilise aussi par L2SafenetBuilder).
    internal static GameObject FindReferenceChild(string childName)
    {
        string prefabPath = $"Assets/Resources/Data/Maps/{ReferenceRegion}/{ReferenceRegion}.prefab";
        GameObject referencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (referencePrefab == null)
        {
            return null;
        }

        Transform child = referencePrefab.transform.Find(childName);
        return child != null ? child.gameObject : null;
    }

    /// Clone un objet de reference sous un nouveau parent en conservant sa
    /// position/rotation/echelle LOCALE d'origine (pas sa position monde) -
    /// c'est justement le fait de garder cette transform locale identique qui
    /// reproduit correctement le positionnement de Water/Safenet, verifie
    /// constant sur les 4 regions de reference.
    internal static GameObject CloneWithOriginalLocalTransform(GameObject reference, Transform newParent)
    {
        Vector3 localPos = reference.transform.localPosition;
        Quaternion localRot = reference.transform.localRotation;
        Vector3 localScale = reference.transform.localScale;

        GameObject clone = (GameObject)Object.Instantiate(reference);
        clone.transform.SetParent(newParent, worldPositionStays: false);
        clone.transform.localPosition = localPos;
        clone.transform.localRotation = localRot;
        clone.transform.localScale = localScale;

        return clone;
    }
}
#endif
