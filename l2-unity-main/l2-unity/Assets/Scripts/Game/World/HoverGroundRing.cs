using UnityEngine;

// Anneau au sol pour le survol/ciblage des PNJ/monstres. Instanciee deux
// fois par ClickManager (un anneau "cible", un anneau "survol") puisque la
// cible actuelle et l'entite survolee peuvent etre deux PNJ differents en
// meme temps - mirroir de la logique de priorite Target/Attack/Hover des
// anciennes bulles de nameplate (cf. NameplatesManagerGame.UpdateWorldBubbleStates).
//
// Charge un prefab genere par HoverRingGenerator (Quad+MeshRenderer, meme
// recette de materiau transparent que les bulles de nameplate - PROUVEE
// fonctionnelle en jeu dans ce projet, car sauvegardee comme asset .mat
// avant usage). Un premier essai construit entierement au runtime (materiau
// jamais sauvegarde comme asset) s'affichait en carre opaque au lieu d'un
// anneau - cf. HoverRingGenerator pour le detail.
//
// Etat porte par UN MATERIAU/TEXTURE PAR ETAT (3 PNG prepares a la main,
// cf. HoverRingGenerator) plutot que par une teinte runtime - meme
// principe que l'ancien systeme de bulles de nameplate. Seul l'alpha de
// fondu est encore applique via MaterialPropertyBlock (_BaseColor en
// blanc, alpha variable) : ca ne modifie pas la couleur/texture propre du
// materiau, juste sa visibilite progressive.
public class HoverGroundRing
{
    public enum RingState { Hover, Target, Attack }

    private const string PrefabResourcePath = "Prefab/Game/HoverRing";
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private readonly Transform _root;
    private readonly MeshRenderer _renderer;
    private readonly MaterialPropertyBlock _mpb;
    private readonly float _rotationSpeed;
    private readonly float _fadeSpeed;

    private Material _hoverMaterial;
    private Material _targetMaterial;
    private Material _attackMaterial;

    private float _currentAlpha;
    private bool _visible;

    public HoverGroundRing(float rotationSpeed = 30f, float fadeSpeed = 6f)
    {
        _rotationSpeed = rotationSpeed;
        _fadeSpeed = fadeSpeed;
        _mpb = new MaterialPropertyBlock();

        GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[HoverGroundRing] Prefab introuvable (Resources/{PrefabResourcePath}) - a generer via Tools > L2Unity > Highlight > Generate HoverRing Prefab. Anneau desactive.");
            return;
        }

        GameObject go = Object.Instantiate(prefab);
        go.name = "HoverRing";

        _renderer = go.GetComponent<MeshRenderer>();
        _root = go.transform;
        _renderer.enabled = false;
    }

    // Un materiau-asset PAR ETAT (genere par HoverRingGenerator a partir de
    // 3 PNG prepares a la main) : changer d'etat = echanger sharedMaterial,
    // jamais modifier la texture d'un materiau en place (partage entre les
    // deux instances d'anneau - le muter changerait l'autre anneau aussi).
    public void SetMaterials(Material hover, Material target, Material attack)
    {
        _hoverMaterial = hover;
        _targetMaterial = target;
        _attackMaterial = attack;
    }

    public void SetState(RingState state)
    {
        if (_renderer == null) return;

        Material material = state switch
        {
            RingState.Target => _targetMaterial,
            RingState.Attack => _attackMaterial,
            _ => _hoverMaterial
        };

        if (material != null)
        {
            _renderer.sharedMaterial = material;
        }
    }

    public void Show(Vector3 groundPosition, float radius)
    {
        if (_renderer == null) return;

        _visible = true;
        _root.position = groundPosition + Vector3.up * 0.02f;
        _root.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
    }

    public void Hide()
    {
        _visible = false;
    }

    // A appeler depuis un Update() existant (cette classe n'est pas un
    // MonoBehaviour), CHAQUE FRAME que l'anneau soit visible ou non (le
    // fondu de sortie continue de s'executer pendant que _visible est deja
    // faux) : anime le fondu alpha, et fait tourner l'anneau (axe vertical
    // MONDE, pas l'axe local du Quad deja bascule a plat par le prefab)
    // tant qu'il est visible.
    public void Tick()
    {
        if (_renderer == null) return;

        float targetAlpha = _visible ? 1f : 0f;
        _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, _fadeSpeed * Time.deltaTime);

        if (_currentAlpha <= 0.001f)
        {
            if (_renderer.enabled)
            {
                _renderer.enabled = false;
            }
            return;
        }

        if (!_renderer.enabled)
        {
            _renderer.enabled = true;
        }

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColorId, new Color(1f, 1f, 1f, _currentAlpha));
        _renderer.SetPropertyBlock(_mpb);

        if (_visible)
        {
            _root.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime, Space.World);
        }
    }
}
