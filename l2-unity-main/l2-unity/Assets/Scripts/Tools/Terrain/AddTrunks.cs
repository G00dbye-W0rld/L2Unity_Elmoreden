#if (UNITY_EDITOR)
using UnityEditor;
using UnityEngine;

/// Remplace le collider du feuillage des arbres par un tronc cylindrique.
///
/// POURQUOI
/// Le mesh d'un arbre est une seule piece, feuillage compris. Avec son
/// MeshCollider d'origine, le joueur se cogne dans les branches a plusieurs
/// metres du sol et la geodata client marque toute la couronne comme
/// infranchissable. On desactive donc ce collider et on pose a la place un
/// cylindre invisible a la base : seul le tronc bloque, comme dans le client
/// d'origine.
///
/// Les troncs vont sur le layer Unwalkable (16) - c'est ainsi que sont les
/// 149 troncs de la region de reference 17_25.
public class AddTrunks
{
    // Layer du projet (ProjectSettings/TagManager.asset).
    private const int LayerUnwalkable = 16;

    /// Fragments de noms identifiant un package d'arbres.
    ///
    /// Avant, un seul nom etait code en dur ("speaking_tree_s") : l'outil ne
    /// faisait donc rien sur toute region hors Talking Island - les arbres de
    /// Gludio (gludio_tree_S) gardaient leur collider de feuillage sans aucun
    /// signal. Cette liste est comparee en minuscules.
    private static readonly string[] TreePackages =
    {
        "speaking_tree_s",
        "gludio_tree_s",
        "dion_tree_s",
        "oren_tree_s",
        "rionsctgart_tree_s",
    };

    [MenuItem("Shnok/[Debug] Add trunks to trees")]
    private static void AddTrunksToTreesMenu()
    {
        int count = AddTrunksToTrees();
        Debug.Log($"[Trunks] {count} arbre(s) traite(s).");
    }

    /// Rend le nombre d'arbres traites. Appelable sans dialogue depuis
    /// L2MapBatchImporter.
    public static int AddTrunksToTrees()
    {
        GameObject[] foundObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int treated = 0;

        foreach (var gameObj in foundObjects)
        {
            if (!gameObj.activeSelf || !IsTree(gameObj.name))
            {
                continue;
            }

            // Un arbre deja traite porte son tronc : on ne l'empile pas une
            // seconde fois si l'etape est rejouee.
            if (gameObj.transform.Find("trunk") != null)
            {
                continue;
            }

            MeshCollider canopy = gameObj.GetComponent<MeshCollider>();
            if (canopy != null)
            {
                canopy.enabled = false;
            }

            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

            float cylinderHeight = 1.5f;
            cylinder.transform.position = gameObj.transform.position + Vector3.up * cylinderHeight / 2f;
            cylinder.transform.localScale = new Vector3(1.25f, cylinderHeight, 1.25f);

            // La primitive Cylinder ne fournit pas de collider exploitable ici
            // (verifie sur 17_25 : 1115 MeshCollider, aucun CapsuleCollider).
            // Sans cet ajout explicite, les troncs seraient traversables.
            cylinder.AddComponent<MeshCollider>();
            cylinder.GetComponent<MeshRenderer>().enabled = false;
            cylinder.layer = LayerUnwalkable;

            cylinder.transform.parent = gameObj.transform;
            cylinder.transform.name = "trunk";

            treated++;
        }

        return treated;
    }

    private static bool IsTree(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        string lower = objectName.ToLower();
        foreach (string package in TreePackages)
        {
            if (lower.Contains(package))
            {
                return true;
            }
        }

        return false;
    }
}
#endif
