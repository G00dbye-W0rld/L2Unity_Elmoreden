using AtmosphericHeightFog;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Effet visuel quand la camera passe sous la surface de l'eau : teinte
// bleu-vert + brouillard rapproche + vignette + une legere distorsion
// animee (l'eau "ondule" l'image). Detecte la profondeur via
// WaterSurfaceQuery (deja ecrit pour la nage, cf. PlayerController).
// Modifie en direct les valeurs du Volume Profile global partage
// (L2PostProcess), meme principe que DayNightCycle qui modifie deja ce
// profil pour l'exposition/le brouillard jour-nuit - pas de profil separe
// a creer, juste ce profil a glisser dans l'Inspector.
public class UnderwaterEffect : MonoBehaviour
{
    [SerializeField] private VolumeProfile _postProcessProfile;

    [Header("Transition")]
    // Distance (en unites monde) sous la surface sur laquelle l'effet monte
    // progressivement a pleine intensite - evite un "pop" brutal a la ligne d'eau,
    // mais doit rester courte pour que l'effet soit visible tout de suite.
    [SerializeField] private float _transitionDistance = 0.4f;
    [SerializeField] private float _blendSpeed = 8f;

    [Header("Teinte")]
    [SerializeField] private Color _underwaterColorFilter = new Color(0.4f, 0.72f, 0.85f, 1f);
    [SerializeField] private float _underwaterContrastDelta = -18f;
    [SerializeField] private float _underwaterSaturationDelta = -25f;

    [Header("Vignette")]
    [SerializeField] private float _underwaterVignetteIntensity = 0.55f;

    [Header("Distorsion (ondulation)")]
    // Echelle native de LensDistortion.intensity (-1..1), pas de pourcentage.
    [SerializeField] private float _wobbleAmplitude = 0.25f;
    [SerializeField] private float _wobbleSpeed = 0.6f;

    [Header("Brouillard sous-marin")]
    [SerializeField] private float _underwaterFogStart = 0f;
    [SerializeField] private float _underwaterFogEnd = 12f;

    private ColorAdjustments _colorAdjustments;
    private Vignette _vignette;
    private LensDistortion _lensDistortion;

    private float _blend;
    private bool _wasUnderwater;
    private Color _capturedColorFilter = Color.white;
    private float _capturedContrast;
    private float _capturedSaturation;
    private float _capturedFogStart;
    private float _capturedFogEnd;

    private void Start()
    {
        if (_postProcessProfile == null) return;

        _postProcessProfile.TryGet(out _colorAdjustments);
        _postProcessProfile.TryGet(out _vignette);
        _postProcessProfile.TryGet(out _lensDistortion);

        // Les deux composants existent deja dans le profil (juste inactifs, valeurs
        // a 0) - on les active une fois pour toutes, le blend ci-dessous les ramene
        // a un effet nul hors de l'eau donc aucun changement visuel sur la terre ferme.
        if (_vignette != null) _vignette.active = true;
        if (_lensDistortion != null) _lensDistortion.active = true;
    }

    private void Update()
    {
        if (_colorAdjustments == null) return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector3 camPos = mainCamera.transform.position;
        bool underwater = WaterSurfaceQuery.TryGetSurfaceHeight(camPos, out float surfaceY) && camPos.y < surfaceY;

        if (underwater && !_wasUnderwater)
        {
            _capturedColorFilter = _colorAdjustments.colorFilter.value;
            _capturedContrast = _colorAdjustments.contrast.value;
            _capturedSaturation = _colorAdjustments.saturation.value;
            if (HeightFogGlobal.Instance != null)
            {
                _capturedFogStart = HeightFogGlobal.Instance.fogDistanceStart;
                _capturedFogEnd = HeightFogGlobal.Instance.fogDistanceEnd;
            }
        }
        _wasUnderwater = underwater;

        float targetBlend = underwater ? Mathf.Clamp01((surfaceY - camPos.y) / _transitionDistance) : 0f;
        _blend = Mathf.MoveTowards(_blend, targetBlend, Time.deltaTime * _blendSpeed);

        _colorAdjustments.colorFilter.value = Color.Lerp(_capturedColorFilter, _underwaterColorFilter, _blend);
        _colorAdjustments.contrast.value = _capturedContrast + _underwaterContrastDelta * _blend;
        _colorAdjustments.saturation.value = _capturedSaturation + _underwaterSaturationDelta * _blend;

        if (_vignette != null)
        {
            _vignette.intensity.value = _underwaterVignetteIntensity * _blend;
        }

        if (_lensDistortion != null)
        {
            float wobble = Mathf.Sin(Time.time * _wobbleSpeed * Mathf.PI * 2f) * _wobbleAmplitude;
            _lensDistortion.intensity.value = wobble * _blend;
        }

        if (HeightFogGlobal.Instance != null && _blend > 0f)
        {
            HeightFogGlobal.Instance.fogDistanceStart = Mathf.Lerp(_capturedFogStart, _underwaterFogStart, _blend);
            HeightFogGlobal.Instance.fogDistanceEnd = Mathf.Lerp(_capturedFogEnd, _underwaterFogEnd, _blend);
        }
    }
}
