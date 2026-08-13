using FMOD.Studio;
using FMODUnity;
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

    // Boucle d'ambiance a assigner dans l'Inspector une fois l'evenement cree
    // cote FMOD Studio (aucun evenement d'ambiance sous-marine n'existait avant -
    // seul le fichier .wav brut etait importe, jamais assemble en Event).
    [Header("Ambiance sonore")]
    [SerializeField] private EventReference _underwaterAmbienceEvent;

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

    /// Densite de surface, pour les modes exponentiels.
    private float _capturedFogDensity;

    /// Une valeur de surface a ete capturee : le brouillard nous appartient
    /// jusqu'a ce qu'on l'ait rendue a l'identique. Sans ce drapeau, le bloc
    /// s'appliquerait des la premiere image avec des valeurs capturees a zero.
    private bool _fogCaptured;

    /// Immersion en cours ou en cours de resorption : couleur et brouillard
    /// sont a nous, et surtout ne doivent PAS etre recaptures.
    private bool _captured;

    private EventInstance _ambienceInstance;
    private bool _ambienceInstanceValid;
    private bool _ambiencePlaying;

    private void Start()
    {
        if (!_underwaterAmbienceEvent.IsNull)
        {
            try
            {
                _ambienceInstance = RuntimeManager.CreateInstance(_underwaterAmbienceEvent);
                _ambienceInstanceValid = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"UnderwaterEffect: impossible de creer l'instance FMOD pour {_underwaterAmbienceEvent} ({e.Message}). Banks pas a jour dans cette session Play ? Il faut re-Build cote FMOD Studio PUIS sortir/rentrer en Play Mode, les banks ne se rechargent pas a chaud.");
            }
        }
        else
        {
            Debug.LogWarning("UnderwaterEffect: aucun event d'ambiance sous-marine assigne dans l'Inspector (champ Underwater Ambience Event).");
        }

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

    private void OnDestroy()
    {
        if (_ambiencePlaying)
        {
            _ambienceInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        }
    }

    private void Update()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector3 camPos = mainCamera.transform.position;
        bool underwater = WaterSurfaceQuery.TryGetSurfaceHeight(camPos, out float surfaceY) && camPos.y < surfaceY;

        // UNE SEULE CAPTURE PAR IMMERSION, couleur ET brouillard.
        //
        // La condition !_captured est le point important. Replonger avant la
        // fin du fondu enregistrerait comme "surface" des valeurs deja teintees
        // par l'eau : chaque aller-retour rapide decalerait un peu plus la
        // reference vers le fond, sans retour possible.
        //
        // Le drapeau doit couvrir les DEUX familles. Ne proteger que le
        // brouillard laissait deriver filtre, contraste et saturation - c'est
        // ce qui rendait les plongeons rapides incoherents.
        if (underwater && !_wasUnderwater && !_captured && _colorAdjustments != null)
        {
            _capturedColorFilter = _colorAdjustments.colorFilter.value;
            _capturedContrast = _colorAdjustments.contrast.value;
            _capturedSaturation = _colorAdjustments.saturation.value;

            // Brouillard URP depuis le retrait d'Atmospheric Height Fog, qui
            // n'affectait pas les terrains MicroSplat. Voir DayNightCycle.
            if (RenderSettings.fog)
            {
                if (RenderSettings.fogMode == FogMode.Linear)
                {
                    _capturedFogStart = RenderSettings.fogStartDistance;
                    _capturedFogEnd = RenderSettings.fogEndDistance;
                }
                else
                {
                    _capturedFogDensity = RenderSettings.fogDensity;
                }

                _fogCaptured = true;
            }

            _captured = true;
        }
        _wasUnderwater = underwater;

        float targetBlend = underwater ? Mathf.Clamp01((surfaceY - camPos.y) / _transitionDistance) : 0f;
        _blend = Mathf.MoveTowards(_blend, targetBlend, Time.deltaTime * _blendSpeed);

        UpdateVisuals();
        UpdateAmbience(camPos);
    }

    private void UpdateVisuals()
    {
        if (_colorAdjustments == null) return;

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

        // Sous l'eau, le brouillard se resserre pour donner la sensation de
        // faible visibilite.
        // NE PAS conditionner ce bloc a _blend > 0.
        //
        // A blend = 0 le Lerp rend exactement la valeur de surface : c'est LUI
        // qui restaure le brouillard. Le sauter des que le fondu touche zero
        // fige le reglage sur l'avant-derniere image, et le brouillard reste
        // celui du fond de l'eau une fois revenu a l'air libre.
        //
        // Le defaut etait discret en lineaire - quelques pourcents d'ecart -
        // mais criant en exponentiel : entre 0,00715 en surface et 0,268 sous
        // l'eau, l'ecart est de 37x, donc 7 % du trajet laissent une densite
        // 3,5 fois trop forte.
        if (_fogCaptured && RenderSettings.fog)
        {
            if (RenderSettings.fogMode == FogMode.Linear)
            {
                RenderSettings.fogStartDistance = Mathf.Lerp(_capturedFogStart, _underwaterFogStart, _blend);
                RenderSettings.fogEndDistance = Mathf.Lerp(_capturedFogEnd, _underwaterFogEnd, _blend);
            }
            else
            {
                // Meme intention en exponentiel : on raisonne en distance de
                // visibilite, convertie en densite. _underwaterFogEnd reste
                // donc le seul reglage a toucher, quel que soit le mode.
                float target = RegionStreamer.DensityForHorizon(
                    RenderSettings.fogMode, _underwaterFogEnd);

                RenderSettings.fogDensity = Mathf.Lerp(_capturedFogDensity, target, _blend);
            }

            // Surface retrouvee et valeurs rendues a l'identique : on lache le
            // brouillard, pour ne pas ecraser en continu ce que le cycle
            // jour/nuit ou les reglages video pourraient y ecrire.
            if (_blend <= 0f)
            {
                _fogCaptured = false;
            }
        }

        // La capture n'est rouverte qu'une fois le fondu entierement resorbe :
        // les Lerp ci-dessus ont alors rendu couleur ET brouillard a leur
        // valeur de surface exacte. Hors de ce bloc pour que la liberation ait
        // lieu meme quand le brouillard est desactive.
        if (_blend <= 0f)
        {
            _captured = false;
        }
    }

    // Demarre des que le blend quitte 0 (donc des l'entree dans la zone de
    // transition, pas besoin d'attendre l'immersion complete) et s'arrete
    // seulement une fois le blend revenu exactement a 0 (MoveTowards l'atteint
    // pile, contrairement a un Lerp qui ne fait que s'en approcher) - le volume
    // de l'instance suit le blend en direct pour un fondu au meme rythme que le visuel.
    // Position 3D calee en permanence sur la camera/auditeur : que l'event ait ete
    // authore en 2D ou en 3D cote FMOD Studio, une distance nulle avec l'auditeur
    // annule toute attenuation par distance (evite de dependre d'un reglage FMOD
    // Studio pas toujours evident a verifier/retirer depuis l'UI).
    private void UpdateAmbience(Vector3 listenerPosition)
    {
        if (!_ambienceInstanceValid) return;

        if (_blend > 0f && !_ambiencePlaying)
        {
            _ambienceInstance.set3DAttributes(RuntimeUtils.To3DAttributes(listenerPosition));
            _ambienceInstance.start();
            _ambiencePlaying = true;
        }
        else if (_blend <= 0f && _ambiencePlaying)
        {
            _ambienceInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _ambiencePlaying = false;
        }

        if (_ambiencePlaying)
        {
            _ambienceInstance.set3DAttributes(RuntimeUtils.To3DAttributes(listenerPosition));
            _ambienceInstance.setVolume(_blend);
        }
    }
}
