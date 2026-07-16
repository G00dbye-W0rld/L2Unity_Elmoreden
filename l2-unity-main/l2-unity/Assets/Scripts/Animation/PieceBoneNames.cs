using UnityEngine;

// Cache les noms des os du mesh source (dans l'ordre exact des bindposes), captures
// au moment de la generation du prefab de piece d'equipement
// (OrcShamanPrefabGenerator.GeneratePiecePrefabs). SkinnedMeshSync s'en sert pour
// remapper renderer.bones par nom vers le squelette de reference, car l'ordre des os
// d'une piece ne correspond pas toujours a un simple prefixe du squelette complet.
public class PieceBoneNames : MonoBehaviour
{
    public string[] boneNames;

    // Cache runtime du bindpose ORIGINAL (non modifie) du mesh, capture au tout
    // premier DoSync(). Necessaire car SkinnedMeshSync peut cloner et remplacer
    // sharedMesh (emprunt de bindpose / correctif tete) ; sans ce cache, un
    // second SyncMesh() lirait le mesh deja corrige et cumulerait le correctif.
    [System.NonSerialized] public Matrix4x4[] pristineBindposes;
}
