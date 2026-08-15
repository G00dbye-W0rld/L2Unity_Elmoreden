using System.Collections.Generic;
using UnityEngine;

/// Force tous les terrains a partager UN SEUL materiau, donc un seul shader.
///
/// POURQUOI CE COMPOSANT EXISTE
/// Outil de diagnostic, pas de production. Depuis le 12/08/2026, la machine de
/// test subit des resets GPU (TDR confirme par Windows, evenement nvlddmkm 153)
/// sans aucun precurseur logiciel : au dernier releve avant le crash, 13 a
/// 23 ms par image, 1808 draws, memoire graphique plate, aucun ramassage de
/// miettes. Le GPU s'ennuyait, puis il s'est fige.
///
/// Toutes les pistes mesurables ont ete eliminees - bruit de log, memoire
/// managee, exceptions FMOD, ombres, MSAA, shadowmaps, brouillard, NavMesh.
/// FurMark passe sans incident a 74 degres, mais il dessine la meme image en
/// boucle : il ne cree jamais de ressource.
///
/// Reste une hypothese que rien n'a pu tester : chaque region possede SON
/// shader MicroSplat genere - 153 programmes reellement distincts, MicroSplat
/// cuisant ses fonctionnalites dans le code source plutot que dans des
/// mots-cles runtime. Quand le streaming fait apparaitre une region jamais vue,
/// le pilote doit construire un pipeline state, operation synchrone sur le
/// thread de rendu et invisible a tous les compteurs Unity.
///
/// CE QUE FAIT CE COMPOSANT
/// Il remplace le materiau de chaque terrain charge par un materiau unique
/// partage. Le rendu devient faux - les index de couches ne correspondent plus
/// - mais un seul pipeline state est construit au lieu de quatre.
///
/// Si le jeu tient dans ces conditions et lache sans, l'hypothese est
/// demontree et la mutualisation MicroSplat devient le correctif.
///
/// A RETIRER une fois la question tranchee.
public class L2TerrainSharedMaterial : MonoBehaviour
{
    [Tooltip("Materiau applique a TOUS les terrains. Un materiau de terrain URP "
             + "standard suffit : on ne juge pas le rendu, seulement la stabilite. "
             + "Laisser vide desactive le composant.")]
    [SerializeField] private Material _sharedMaterial;

    [Tooltip("Secondes entre deux balayages. Les regions arrivent par le "
             + "streaming, il faut donc repasser regulierement.")]
    [SerializeField] private float _interval = 1f;

    [SerializeField] private bool _verbose = true;

    /// Materiau d'origine de chaque terrain, pour pouvoir revenir en arriere
    /// sans rechargement. Les terrains detruits sortent du dictionnaire au
    /// balayage suivant.
    private readonly Dictionary<Terrain, Material> _originals = new Dictionary<Terrain, Material>();

    private float _next;
    private int _swapped;

    private void Update()
    {
        if (_sharedMaterial == null || Time.time < _next)
        {
            return;
        }

        _next = Time.time + _interval;

        Terrain[] terrains = Terrain.activeTerrains;
        int changed = 0;

        foreach (Terrain terrain in terrains)
        {
            if (terrain == null || terrain.materialTemplate == _sharedMaterial)
            {
                continue;
            }

            if (!_originals.ContainsKey(terrain))
            {
                _originals[terrain] = terrain.materialTemplate;
            }

            terrain.materialTemplate = _sharedMaterial;
            changed++;
        }

        if (changed > 0)
        {
            _swapped += changed;

            if (_verbose)
            {
                Debug.Log($"[SharedMat] {changed} terrain(s) bascules sur le materiau partage "
                          + $"({terrains.Length} actifs, {_swapped} depuis le debut).");
            }
        }
    }

    /// Restaure les materiaux d'origine. Utile pour comparer a chaud sans
    /// relancer la session.
    [ContextMenu("Restaurer les materiaux d'origine")]
    public void Restore()
    {
        int restored = 0;

        foreach (var kv in _originals)
        {
            if (kv.Key != null)
            {
                kv.Key.materialTemplate = kv.Value;
                restored++;
            }
        }

        _originals.Clear();
        _sharedMaterial = null;

        Debug.Log($"[SharedMat] {restored} terrain(s) restaures. Composant neutralise.");
    }
}
