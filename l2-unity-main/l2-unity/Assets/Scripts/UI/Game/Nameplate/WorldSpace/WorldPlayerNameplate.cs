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
    }

    public void ShowGauge(SetupGaugePacket.GaugeColor color, float startTime, int durationMs)
    {
        GaugeStartTime = startTime;
        GaugeEndTime = startTime + durationMs / 1000f;

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
