using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

// Systeme de nameplates world-space (TextMeshPro), alternative poolee au
// systeme UI Toolkit existant (NameplatesManagerBase/Nameplate). Active via
// NameplatesManagerGame.useWorldSpaceNameplates. Positionnement par
// transform.position + rotation billboard, sans passer par
// Camera.WorldToScreenPoint ni le moteur de layout Yoga d'UI Toolkit -
// c'est le point qui elimine le cout structurel identifie dans l'audit.
public class WorldNameplateRenderer : MonoBehaviour
{
    [SerializeField] private GameObject nameplatePrefab;
    [SerializeField] private Transform poolParent;
    [SerializeField] private float nameplateHeightMultiplier = 1.95f;

    // Un texte world-space retrecit avec la distance/le dezoom (perspective
    // normale), contrairement a l'ancien systeme UI Toolkit qui gardait
    // toujours la meme taille a l'ecran. On compense en grossissant la
    // nameplate proportionnellement a sa distance a la camera, dans une
    // fourchette min/max pour eviter l'extreme de trop pres/trop loin.
    [Header("Mise a l'echelle par distance (lisibilite)")]
    [SerializeField] private float distanceScaleFactor = 1f;
    [SerializeField] private float minScale = 0.6f;
    [SerializeField] private float maxScale = 15f;
    [Tooltip("Boost de lisibilite pour les nameplates ELOIGNEES : grossissement supplementaire proportionnel a la distance (0 = taille ecran constante). Compense la perte de lisibilite des noms/titres lointains.")]
    [SerializeField] private float farBoostPerMeter = 0.01f;

    [Tooltip("Taille ECRAN de la barre de cast, independante du zoom : la jauge est contre-scalee chaque frame pour garder une taille apparente constante quelle que soit la distance camera (elle ignore le clamp min/max applique au reste de la nameplate).")]
    [SerializeField] private float gaugeScreenFactor = 0.075f;

    [Tooltip("Vitesse de lissage de la rotation billboard (Slerp par frame). Sans lissage, root.rotation copiait directement _mainCameraTransform.forward chaque frame - le moindre micro-bruit de la camera (suivi du joueur en mouvement) se traduisait par une vibration visible du nom, surtout genante vue de profil (une rotation infime y change beaucoup l'aspect d'un plan de texte plat). Plus haut = plus reactif mais moins filtre.")]
    [SerializeField] private float billboardSmoothing = 20f;

    [Header("Fondu par distance")]
    [Tooltip("Debut du fondu, en fraction de la distance de disparition (cull) : 0.7 = commence a 70% de la portee, termine (invisible) pile au cull. Exprime en fraction pour rester aligne sur le cull quelle que soit sa valeur - pas de pop. La nameplate du joueur local n'est jamais estompee.")]
    [Range(0f, 1f)]
    [SerializeField] private float fadeStartFraction = 0.7f;

    // Materiaux-assets generes par WorldNameplatePrefabGenerator (un par etat
    // de l'icone, prepares a la main). Si non assignes dans l'Inspector,
    // charges automatiquement depuis Resources au premier Initialize().
    [Header("Icone (survol/cible/attaque)")]
    [SerializeField] private Material hoverMaterial;
    [SerializeField] private Material targetMaterial;
    [SerializeField] private Material attackMaterial;

    const string MaterialResourceDir = "Data/UI/Assets/NameplateIcon";

    private readonly ConcurrentQueue<GameObject> _pool = new();
    private readonly ConcurrentDictionary<int, WorldNameplate> _active = new();
    private WorldPlayerNameplate _playerNameplate;

    private Transform _mainCameraTransform;
    private Transform _playerTransform;
    private float _viewDistance;

    public void Initialize(Camera mainCamera, Transform playerTransform, float viewDistance)
    {
        _mainCameraTransform = mainCamera != null ? mainCamera.transform : null;
        _playerTransform = playerTransform;
        _viewDistance = viewDistance;

        if (hoverMaterial == null) hoverMaterial = Resources.Load<Material>($"{MaterialResourceDir}/IconHover");
        if (targetMaterial == null) targetMaterial = Resources.Load<Material>($"{MaterialResourceDir}/IconTarget");
        if (attackMaterial == null) attackMaterial = Resources.Load<Material>($"{MaterialResourceDir}/IconAttack");
        if (hoverMaterial == null || targetMaterial == null || attackMaterial == null)
        {
            Debug.LogWarning("[WorldNameplateRenderer] Materiaux d'icone introuvables (Inspector et Resources) - regenerer via Tools > L2Unity > Nameplate > Generate WorldNameplate Prefab.");
        }
    }

    public bool HasNameplate(int id) => _active.ContainsKey(id);

    private GameObject Rent()
    {
        if (_pool.TryDequeue(out GameObject go))
        {
            go.SetActive(true);
            return go;
        }

        return Instantiate(nameplatePrefab);
    }

    private void Return(GameObject go)
    {
        // Au demontage de scene, Unity peut deja avoir detruit ces
        // GameObjects avant que NameplatesManagerGame.OnDestroy() ait
        // termine son propre nettoyage (MissingReferenceException observee
        // dans les logs a l'arret du Play Mode) - inoffensif, juste a garder.
        if (go == null) return;

        go.SetActive(false);
        go.transform.SetParent(poolParent, false);
        _pool.Enqueue(go);
    }

    public void CreateNameplate(int id, Entity entity)
    {
        if (_active.ContainsKey(id)) return;

        GameObject go = Rent();
        go.transform.SetParent(null, false);
        WorldNameplate nameplate = new WorldNameplate(go);
        nameplate.SetBubbleMaterials(hoverMaterial, targetMaterial, attackMaterial);
        nameplate.Bind(entity);
        _active.TryAdd(id, nameplate);
    }

    public void RemoveNameplate(int id)
    {
        if (_active.TryRemove(id, out WorldNameplate nameplate))
        {
            Return(nameplate.Root);
        }
    }

    public void RemoveAll()
    {
        foreach (int id in _active.Keys)
        {
            RemoveNameplate(id);
        }
    }

    public void Tick()
    {
        if (_mainCameraTransform == null) return;

        foreach (WorldNameplate nameplate in _active.Values)
        {
            if (nameplate.Target == null) continue;
            UpdateNameplateTransform(nameplate);
            nameplate.ManageColors();
        }
    }

    private void UpdateNameplateTransform(WorldNameplate nameplate)
    {
        Entity entity = nameplate.Entity;

        nameplate.OffsetHeight = entity.IsDead
            ? entity.Appearance.CollisionHeight * 0.85f
            : entity.Appearance.CollisionHeight * nameplateHeightMultiplier * (entity.IsSitting ? 0.70f : 1f);

        Transform root = nameplate.RootTransform;
        root.position = nameplate.Target.position + Vector3.up * nameplate.OffsetHeight;

        // Billboard ECRAN-PARALLELE, yaw uniquement : la nameplate adopte
        // l'orientation de la camera projetee a l'horizontale, au lieu de
        // "regarder" la position de la camera. Un look-at par position incline
        // les nameplates laterales (hors centre de l'ecran) -> en perspective,
        // les bulles gauche/droite (a +-0.35) ne se projettent plus
        // symetriquement autour du nom. En restant parallele a l'ecran, toutes
        // les nameplates partagent la meme orientation et les bulles restent
        // centrees sur le nom quelle que soit leur position a l'ecran. La
        // composante verticale est ignoree (y=0) pour garder la nameplate bien
        // droite meme quand la camera plonge (vue a l'epaule).
        Vector3 toCamera = root.position - _mainCameraTransform.position;
        Vector3 camForwardFlat = _mainCameraTransform.forward;
        camForwardFlat.y = 0f;
        if (camForwardFlat.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(camForwardFlat);
            root.rotation = Quaternion.Slerp(root.rotation, targetRotation, Time.deltaTime * billboardSmoothing);
        }

        float distance = toCamera.magnitude;
        // distance * factor = taille ecran constante ; le terme farBoost ajoute
        // un leger grossissement supplementaire avec la distance pour garder
        // les noms lointains lisibles (ils paraissent sinon "perdus" a taille
        // apparente strictement constante).
        float scale = Mathf.Clamp(distance * distanceScaleFactor * (1f + distance * farBoostPerMeter), minScale, maxScale);
        root.localScale = Vector3.one * scale;

        // La barre de cast, elle, doit garder une taille ecran RIGOUREUSEMENT
        // constante (pas de reaction au zoom/dezoom) : contre-scale qui annule
        // le clamp et le boost appliques a la racine. La nameplate du joueur
        // local n'est jamais estompee (toujours pertinente, distance ~constante).
        if (nameplate is WorldPlayerNameplate playerNameplate)
        {
            playerNameplate.UpdateGaugeScreenScale(distance, scale, gaugeScreenFactor);
        }
        else
        {
            nameplate.SetAlpha(ComputeFadeAlpha(nameplate.Target));
        }
    }

    // Fondu en distance JOUEUR (meme referentiel que le cull IsNameplateVisible,
    // mesure depuis le joueur), termine pile a la distance de cull : le fondu
    // atteint 0 exactement quand la nameplate serait depoolee -> pas de pop.
    private float ComputeFadeAlpha(Transform target)
    {
        if (_playerTransform == null || _viewDistance <= 0f) return 1f;
        float start = _viewDistance * fadeStartFraction;
        if (_viewDistance <= start) return 1f;
        float d = Vector3.Distance(_playerTransform.position, target.position);
        return Mathf.Clamp01(1f - (d - start) / (_viewDistance - start));
    }

    public void SetBubbleState(int id, WorldNameplate.BubbleState state)
    {
        if (_active.TryGetValue(id, out WorldNameplate nameplate))
        {
            nameplate.SetBubbleState(state);
        }
    }

    // Culling par distance/occlusion : le systeme ancien fait ca via
    // ProcessNameplateVisibility() sur son propre dictionnaire "nameplates",
    // qui ne voit jamais les entites gerees ici (elles ne rejoignent jamais
    // ce dictionnaire cote monde). Meme logique, appliquee au dictionnaire
    // _active de ce renderer.
    public void CullOutOfRange(Func<Transform, bool> isVisible)
    {
        foreach (int id in new List<int>(_active.Keys))
        {
            if (_active.TryGetValue(id, out WorldNameplate nameplate))
            {
                if (nameplate.Target == null || !isVisible(nameplate.Target))
                {
                    RemoveNameplate(id);
                }
            }
        }
    }

    // Permet a l'appelant (NameplatesManagerGame) de calculer l'etat des
    // bulles de ciblage/survol, qui depend de TargetManager/ClickManager -
    // des dependances que ce renderer n'a pas besoin de connaitre lui-meme.
    public IEnumerable<KeyValuePair<int, Transform>> ActiveTargets()
    {
        foreach (var kvp in _active)
        {
            yield return new KeyValuePair<int, Transform>(kvp.Key, kvp.Value.Target);
        }
    }

    // Nameplate du joueur local, geree a part du dictionnaire _active (meme
    // convention que playerNameplate dans NameplatesManagerGame).
    public WorldPlayerNameplate GetOrCreatePlayerNameplate(Entity playerEntity)
    {
        if (_playerNameplate == null)
        {
            GameObject go = Rent();
            go.transform.SetParent(null, false);
            _playerNameplate = new WorldPlayerNameplate(go);
            _playerNameplate.SetBubbleMaterials(hoverMaterial, targetMaterial, attackMaterial);
            _playerNameplate.Bind(playerEntity);
            // L'icone hover/target/attack n'a pas de sens sur SA PROPRE
            // nameplate (on ne se cible/attaque jamais soi-meme) - masquee
            // explicitement. Bind() l'affiche par defaut (etat Hover,
            // toujours visible) comme pour toute autre nameplate ; jamais
            // mise a jour ensuite puisque la nameplate du joueur ne fait
            // pas partie de worldRenderer.ActiveTargets() / UpdateWorldBubbleStates.
            _playerNameplate.SetBubbleState(WorldNameplate.BubbleState.None);
        }

        return _playerNameplate;
    }

    public void TickPlayerNameplate()
    {
        if (_playerNameplate == null) return;
        UpdateNameplateTransform(_playerNameplate);
        _playerNameplate.ManageColors();
    }

    public void RemovePlayerNameplate()
    {
        if (_playerNameplate != null)
        {
            Return(_playerNameplate.Root);
            _playerNameplate = null;
        }
    }
}
