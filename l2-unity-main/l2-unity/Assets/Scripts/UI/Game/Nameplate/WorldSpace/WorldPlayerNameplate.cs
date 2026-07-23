using UnityEngine;

// Mirroir world-space de PlayerNameplate.cs : ajoute la barre de cast du
// joueur local. GaugeFill est un Quad+MeshRenderer (pas de SpriteRenderer,
// cf. WorldNameplate.cs) - un Quad n'a pas de notion de pivot comme un
// sprite, donc contrairement a l'ancien systeme (style.width seul), il faut
// recalculer position ET echelle ensemble a chaque frame pour simuler un
// remplissage ancre a gauche (le bord gauche reste fixe, le bord droit
// avance/recule avec le ratio).
public class WorldPlayerNameplate : WorldNameplate
{
    // SetupGaugePacket.GaugeColor -> materiau. BLUE (cast de sort) et CYAN
    // (oxygene/noyade, WaterTaskManager cote serveur) partagent le meme
    // materiau bleu (asset MP) - aucun asset cyan dedie dans le projet, et
    // suffisant pour l'usage demande (jauge d'oxygene "bleue"). GREEN (faim
    // de monture) retombe sur le materiau CP par defaut, faute d'asset vert.
    private const string GaugeMaterialResourceDir = "Data/UI/Assets/WorldNameplate/Materials";
    private static Material _gaugeBgDefaultMat;
    private static Material _gaugeFillDefaultMat;
    private static Material _gaugeBgBlueMat;
    private static Material _gaugeFillBlueMat;
    private static Material _gaugeBgRedMat;
    private static Material _gaugeFillRedMat;
    private static bool _gaugeMaterialsLoaded;

    private readonly Transform _gaugeRoot;
    private readonly MeshRenderer _gaugeBG;
    private readonly MeshRenderer _gaugeFill;
    private readonly Transform _gaugeFillTransform;
    private readonly float _gaugeFullWidth;
    private readonly float _gaugeLeftEdgeX;
    private bool _isGaugeVisible;

    public float GaugeStartTime { get; private set; }
    public float GaugeEndTime { get; private set; }

    private readonly Vector3 _gaugeBasePosition;

    public WorldPlayerNameplate(GameObject root) : base(root)
    {
        _gaugeRoot = root.transform.Find("Gauge");
        _gaugeBasePosition = _gaugeRoot.localPosition;
        _gaugeBG = root.transform.Find("Gauge/GaugeBG").GetComponent<MeshRenderer>();
        _gaugeFill = root.transform.Find("Gauge/GaugeFill").GetComponent<MeshRenderer>();
        _gaugeFillTransform = _gaugeFill.transform;

        // Valeurs de reference lues une fois depuis l'etat "plein" genere
        // par WorldNameplatePrefabGenerator (centre, largeur totale) -
        // s'adapte automatiquement si le prefab est regenere avec d'autres
        // dimensions, sans dupliquer de constantes ici.
        _gaugeFullWidth = _gaugeFillTransform.localScale.x;
        _gaugeLeftEdgeX = _gaugeFillTransform.localPosition.x - _gaugeFullWidth / 2f;

        _gaugeBG.enabled = false;
        _gaugeFill.enabled = false;

        LoadGaugeMaterialsOnce();
    }

    // Charge une seule fois (statique, partagee par toutes les instances) -
    // avec repli sur le materiau deja bake dans le prefab (celui assigne par
    // WorldNameplatePrefabGenerator) si les variantes par couleur n'ont pas
    // encore ete generees, pour ne rien casser en attendant.
    private void LoadGaugeMaterialsOnce()
    {
        if (_gaugeMaterialsLoaded) return;
        _gaugeMaterialsLoaded = true;

        _gaugeBgDefaultMat = Resources.Load<Material>($"{GaugeMaterialResourceDir}/GaugeBG") ?? _gaugeBG.sharedMaterial;
        _gaugeFillDefaultMat = Resources.Load<Material>($"{GaugeMaterialResourceDir}/GaugeFill") ?? _gaugeFill.sharedMaterial;
        _gaugeBgBlueMat = Resources.Load<Material>($"{GaugeMaterialResourceDir}/GaugeBG_Blue") ?? _gaugeBgDefaultMat;
        _gaugeFillBlueMat = Resources.Load<Material>($"{GaugeMaterialResourceDir}/GaugeFill_Blue") ?? _gaugeFillDefaultMat;
        _gaugeBgRedMat = Resources.Load<Material>($"{GaugeMaterialResourceDir}/GaugeBG_Red") ?? _gaugeBgDefaultMat;
        _gaugeFillRedMat = Resources.Load<Material>($"{GaugeMaterialResourceDir}/GaugeFill_Red") ?? _gaugeFillDefaultMat;
    }

    public void ShowGauge(SetupGaugePacket.GaugeColor color, float startTime, int durationMs)
    {
        GaugeStartTime = startTime;
        GaugeEndTime = startTime + durationMs / 1000f;

        switch (color)
        {
            case SetupGaugePacket.GaugeColor.BLUE:
            case SetupGaugePacket.GaugeColor.CYAN:
                _gaugeBG.sharedMaterial = _gaugeBgBlueMat;
                _gaugeFill.sharedMaterial = _gaugeFillBlueMat;
                break;
            case SetupGaugePacket.GaugeColor.RED:
                _gaugeBG.sharedMaterial = _gaugeBgRedMat;
                _gaugeFill.sharedMaterial = _gaugeFillRedMat;
                break;
            default:
                _gaugeBG.sharedMaterial = _gaugeBgDefaultMat;
                _gaugeFill.sharedMaterial = _gaugeFillDefaultMat;
                break;
        }

        if (!_isGaugeVisible)
        {
            _isGaugeVisible = true;
            _gaugeBG.enabled = true;
            _gaugeFill.enabled = true;
        }
    }

    public void HideGauge()
    {
        if (_isGaugeVisible)
        {
            _isGaugeVisible = false;
            _gaugeBG.enabled = false;
            _gaugeFill.enabled = false;
        }
    }

    // Taille ecran constante pour la jauge, independante du zoom : la racine
    // de la nameplate est scalee en clamp(distance * facteur * boost) pour la
    // lisibilite des textes, ce qui fait varier sa taille apparente quand la
    // camera zoome (zones de clamp). La jauge annule cet effet : son echelle
    // locale est recalculee chaque frame pour que (echelle racine x echelle
    // jauge) soit exactement proportionnelle a la distance camera.
    public void UpdateGaugeScreenScale(float cameraDistance, float rootScale, float screenFactor)
    {
        if (!_isGaugeVisible) return;
        float uniform = cameraDistance * screenFactor / Mathf.Max(rootScale, 0.0001f);
        _gaugeRoot.localScale = Vector3.one * uniform;
        // L'offset vertical sous le nom doit rester constant A L'ECRAN lui
        // aussi, sinon il grandit avec l'echelle de la racine et la jauge
        // s'enfonce sous la tete a distance/dezoom.
        _gaugeRoot.localPosition = _gaugeBasePosition * uniform;
    }

    public void UpdateGauge(float currentTime)
    {
        if (!_isGaugeVisible) return;

        float ratio = Mathf.Clamp01((currentTime - GaugeStartTime) / (GaugeEndTime - GaugeStartTime));
        float width = _gaugeFullWidth * ratio;

        Vector3 scale = _gaugeFillTransform.localScale;
        scale.x = width;
        _gaugeFillTransform.localScale = scale;

        Vector3 pos = _gaugeFillTransform.localPosition;
        pos.x = _gaugeLeftEdgeX + width / 2f;
        _gaugeFillTransform.localPosition = pos;
    }
}
