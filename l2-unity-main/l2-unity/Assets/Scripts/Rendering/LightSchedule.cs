using UnityEngine;

// Allume une lumiere selon l'heure du monde. Se combine avec LightLOD, qui
// decide de la distance : c'est LightLOD qui ecrit Light.enabled quand il est
// present, sinon ce composant s'en charge.
[RequireComponent(typeof(Light))]
public class LightSchedule : MonoBehaviour
{
    [Tooltip("Cochee, la lumiere ne s'allume qu'a la tombee de la nuit.")]
    [SerializeField] private bool _nightOnly = true;

    [Tooltip("Duree du fondu, en secondes. 0 pour un allumage sec.")]
    [SerializeField] private float _fadeSeconds = 4f;

    [Header("Halo")]
    // Une lumiere n'eclaire pas sa propre lampe : sa face visible tourne le dos
    // a la source. C'est ce renderer emissif qui la fait paraitre allumee.
    [Tooltip("Quad emissif de la lanterne. Optionnel.")]
    [SerializeField] private Renderer _glow;

    [Tooltip("Couleur HDR du halo. Au-dela de 1, le bloom la diffuse.")]
    [SerializeField] [ColorUsage(false, true)] private Color _glowColor = new Color(4f, 2.2f, 0.8f);

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Light _light;
    private LightLOD _lod;
    private MaterialPropertyBlock _block;
    private float _baseIntensity;
    private float _factor = -1f;

    public bool IsOn { get; private set; }

    private void Awake()
    {
        _light = GetComponent<Light>();
        _lod = GetComponent<LightLOD>();
        _baseIntensity = _light.intensity;
    }

    private void Update()
    {
        if (WorldClock.Instance == null)
        {
            return;
        }

        float target = !_nightOnly || WorldClock.Instance.IsNightTime() ? 1f : 0f;

        // Premiere image : on prend l'etat cible sans fondu, sinon toutes les
        // lumieres de la ville s'allumeraient au chargement.
        if (_factor < 0f)
        {
            _factor = target;
        }
        else
        {
            _factor = _fadeSeconds > 0f
                ? Mathf.MoveTowards(_factor, target, Time.deltaTime / _fadeSeconds)
                : target;
        }

        _light.intensity = _baseIntensity * _factor;

        ApplyGlow();

        bool on = _factor > 0f;

        if (on != IsOn)
        {
            IsOn = on;
            Apply();
        }
    }

    // MaterialPropertyBlock plutot que .material : ce dernier creerait une copie
    // de materiau par lampadaire. On ecrit les deux proprietes pour marcher
    // aussi bien avec un shader Lit emissif qu'avec un Unlit additif.
    private void ApplyGlow()
    {
        if (_glow == null)
        {
            return;
        }

        bool visible = _factor > 0f;

        if (_glow.enabled != visible)
        {
            _glow.enabled = visible;
        }

        if (!visible)
        {
            return;
        }

        _block ??= new MaterialPropertyBlock();

        Color c = _glowColor * _factor;

        _glow.GetPropertyBlock(_block);
        _block.SetColor(EmissionColorId, c);
        _block.SetColor(BaseColorId, c);
        _glow.SetPropertyBlock(_block);
    }

    private void Apply()
    {
        if (_lod != null)
        {
            _lod.RefreshEnabled();
        }
        else
        {
            _light.enabled = IsOn;
        }
    }
}
