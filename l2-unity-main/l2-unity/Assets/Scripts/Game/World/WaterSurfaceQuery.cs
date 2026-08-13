using Bitgem.VFX.StylisedWater;
using UnityEngine;

// Interroge la hauteur de surface de l'eau a une position donnee, en utilisant
// les WaterVolumeBase deja places sur chaque carte (plugin Stylised Water,
// jusqu'ici jamais utilise cote gameplay). N'utilise PAS WaterVolumeHelper.
// Instance (singleton du plugin) : s'il y avait un jour deux plans d'eau
// distincts dans une meme carte (ex. une mer et un lac de montagne separe),
// le singleton ne retiendrait que le dernier initialise et casserait les
// requetes sur l'autre. On interroge donc TOUS les volumes actifs de la
// scene et on garde celui dont l'empreinte horizontale contient la position.
//
// N'utilise PAS WaterVolumeBase.GetHeight() (systeme de tuiles construit a
// partir de "markers" places a la main sous chaque objet Water) : constate en
// test que sa couverture peut avoir des trous, auquel cas GetHeight renvoie
// null meme au milieu d'un plan d'eau visible -> plus rien ne freinait la
// remontee du joueur (envol). A la place, on utilise les limites (bounds) du
// MeshRenderer du plan d'eau : le mesh est quasiment plat (echelle Y ~0.1
// dans les donnees de carte observees), donc sa hauteur represente fidelement
// la surface partout dans son emprise horizontale, sans dependre du
// placement des markers.
public static class WaterSurfaceQuery
{
    private static WaterVolumeBase[] _volumes;

    public static bool TryGetSurfaceHeight(Vector3 position, out float height)
    {
        RefreshIfNeeded();

        foreach (WaterVolumeBase volume in _volumes)
        {
            if (volume == null) continue;

            MeshRenderer meshRenderer = volume.GetComponent<MeshRenderer>();
            if (meshRenderer == null) continue;

            Bounds bounds = meshRenderer.bounds;
            if (position.x < bounds.min.x || position.x > bounds.max.x ||
                position.z < bounds.min.z || position.z > bounds.max.z)
            {
                continue;
            }

            height = volume.transform.position.y;
            return true;
        }

        height = 0f;
        return false;
    }

    // A appeler quand on quitte l'eau (fin de session de nage) : force une
    // nouvelle recherche au prochain besoin, au cas ou la carte aurait change
    // entre-temps (changement de zone/carte pendant qu'on ne nageait pas).
    public static void Invalidate()
    {
        _volumes = null;
        _indexed = false;
    }

    // Le drapeau est distinct du tableau : se fier a sa longueur relancerait un
    // FindObjectsByType a CHAQUE image dans une region sans eau - "aucun volume"
    // est un resultat valide, pas un cache vide. UnderwaterEffect interroge en
    // Update, la difference n'est pas theorique.
    private static bool _indexed;

    private static void RefreshIfNeeded()
    {
        if (_indexed)
        {
            return;
        }

        _volumes = Object.FindObjectsByType<WaterVolumeBase>(FindObjectsSortMode.None);
        _indexed = true;
    }
}
