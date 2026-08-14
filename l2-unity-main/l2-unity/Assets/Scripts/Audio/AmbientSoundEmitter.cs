using FMODUnity;
using System.Collections;
using UnityEngine;

public class AmbientSoundEmitter : EventHandler {
    private FMOD.Studio.EventDescription _eventDescription;
    private FMOD.Studio.EventInstance _instance;

    [SerializeField] private EventReference _eventReference;
    [SerializeField] private EmitterGameEvent _playEvent = EmitterGameEvent.None;
    [SerializeField] private EmitterGameEvent _stopEvent = EmitterGameEvent.None;
    [SerializeField] private AmbientSoundType _ambientSoundType;
    [SerializeField] private bool _allowFadeout = true;
    [SerializeField] private bool _overrideAttenuation = false;
    [SerializeField] private float _loopDelaySeconds = 1;
    [SerializeField] private float _playChancePercent = 100;
    [SerializeField] private float _soundPitch = 0;
    [SerializeField] private bool _loop = true;
    [SerializeField] private float _overrideMinDistance = -1.0f;
    [SerializeField] private float _overrideMaxDistance = -1.0f;
    [SerializeField] private float _clipLengthSeconds = 0;
    [SerializeField] private bool _isSound3D = true;

    public EventReference EventReference { set { _eventReference = value; } }
    public EmitterGameEvent PlayEvent { set { _playEvent = value; } }
    public EmitterGameEvent StopEvent { set { _stopEvent = value; } }
    public AmbientSoundType AmbientSoundType { set { _ambientSoundType = value; } }
    public bool AllowFadeout { set { _allowFadeout = value; } }
    public bool OverrideAttenuation { set { _overrideAttenuation = value; } }
    public float LoopDelaySeconds { set { _loopDelaySeconds = value; } }
    public float PlayChancePercent { set { _playChancePercent = value; } }
    public float SoundPitch { set { _soundPitch = value; } }
    public bool Loop { set { _loop = value; } }
    public float OverrideMinDistance { set { _overrideMinDistance = value; } }
    public float OverrideMaxDistance { set { _overrideMaxDistance = value; } }

    private void Awake() {
        if(!_eventDescription.isValid()) {
            Lookup();
        }

        // Un evenement absent laisse une description invalide : l'interroger
        // ici propagerait l'echec dans tout le reste du composant.
        //
        // On ne s'inscrit PAS aupres du culler dans ce cas : il rallumerait
        // periodiquement un emetteur qui n'a rien a jouer.
        if(_missing || !_eventDescription.isValid()) {
            enabled = false;
            return;
        }

        // A partir d'ici l'emetteur est viable : le culler decide quand il doit
        // etre actif. Voir AmbientSoundCuller - environ 1180 emetteurs par
        // region, dont une poignee sont a portee d'oreille.
        AmbientSoundCuller.Register(this);

        int lengthMs = 0;
        _eventDescription.getLength(out lengthMs);
        _clipLengthSeconds = lengthMs / 1000f;
        _clipLengthSeconds = _clipLengthSeconds / _soundPitch;

        _eventDescription.is3D(out _isSound3D);
    }

    private float MaxDistance {
        get {
            if(_overrideAttenuation) {
                return _overrideMaxDistance;
            }

            if(!_eventDescription.isValid()) {
                Lookup();
            }

            if(_missing || !_eventDescription.isValid()) {
                return 0f;
            }

            float minDistance, maxDistance;
            _eventDescription.getMinMaxDistance(out minDistance, out maxDistance);
            return maxDistance;
        }
    }

    /// La recherche a echoue : l'evenement n'existe pas dans les banques
    /// chargees. On ne retentera pas.
    private bool _missing;

    /// Evenements deja signales, tous emetteurs confondus.
    ///
    /// Sans ce filtre, un meme son absent produit un avertissement par
    /// emetteur ET par declenchement : mesure du 2026-08-14, 2405 exceptions
    /// FMOD en un quart d'heure, chacune retenue par la Console de l'editeur
    /// avec sa trace de pile.
    private static readonly System.Collections.Generic.HashSet<string> _reported
        = new System.Collections.Generic.HashSet<string>();

    /// ETAT DES BANQUES, 2026-08-14
    /// Master.strings.bank ne contient d'evenements d'ambiance que pour la
    /// region 17_25 - les banques datent d'une epoque ou une seule region
    /// existait. Toutes les autres regions demandent donc des evenements
    /// absents. Le projet FMOD Studio (.fspro) n'est pas dans le depot : les
    /// reconstruire est un chantier a part, avec les sources audio.
    ///
    /// En attendant, un evenement manquant doit rester silencieux, pas
    /// bruyant : l'emetteur se desactive au lieu de lever une exception a
    /// chaque entree dans son declencheur.
    private void Lookup() {
        if(_missing) {
            return;
        }

        try {
            _eventDescription = RuntimeManager.GetEventDescription(_eventReference);
        }
        catch(EventNotFoundException) {
            _missing = true;

            string path = _eventReference.IsNull ? "(vide)" : _eventReference.Path;
            if(_reported.Add(path)) {
                Debug.LogWarning($"[AmbientSound] Evenement absent des banques : {path}. "
                                 + "Emetteur desactive. Signale une seule fois.");
            }
        }
    }

    public void Play() {
        if(_eventReference.IsNull) {
            return;
        }

        if(!_eventDescription.isValid()) {
            Lookup();
        }

        // Un composant desactive ne recoit plus OnTriggerEnter, mais Play peut
        // aussi etre appele directement : on garde la porte fermee des deux
        // cotes plutot que de dependre d'un detail du cycle de vie Unity.
        if(_missing || !_eventDescription.isValid()) {
            return;
        }

        if (_loop) {
            StopCoroutine(StartPlayLoop());
            StartCoroutine(StartPlayLoop());
            return;
        }

        if (_isSound3D && ShouldStop()) {
            Stop();
        } else if (ShouldPlayEvent()) {
            PlayInstance();
        }
    }


    IEnumerator StartPlayLoop() {
        while(true) {
            if (_isSound3D && ShouldStop()) {
                Stop();
                yield break;
            }
            if(ShouldPlayEvent()) {
                PlayInstance();
            }
            yield return new WaitForSeconds(_clipLengthSeconds * 0.99f); // 1% for error margin
            if (_loopDelaySeconds > 0) {
                yield return new WaitForSeconds(_loopDelaySeconds);
            }
        }
    }

    private bool ShouldPlayEvent() {
        if(_ambientSoundType == AmbientSoundType.AST1_Day && WorldClock.Instance.IsNightTime()) {
            return false;
        }
        if(_ambientSoundType == AmbientSoundType.AST1_Night && !WorldClock.Instance.IsNightTime()) {
            return false;
        }
        if(Random.Range(1, 100) <= _playChancePercent) {
            return true;
        }

        return false;
    }

    private bool ShouldStop() {
        if(ThirdPersonListener.Instance.Player == null) {
            return true;
        }

        float distance = Vector3.Distance(transform.position, ThirdPersonListener.Instance.Player.transform.position);
        return distance > MaxDistance;
    }

    private void PlayInstance() {
        if (!_instance.isValid()) {
            _instance.clearHandle();
        }

        // Let previous oneshot instances play out
        if (_instance.isValid()) {
            _instance.release();
            _instance.clearHandle();
        }

        bool is3D;
        _eventDescription.is3D(out is3D);

        if(!_instance.isValid()) {
            _eventDescription.createInstance(out _instance);

            // Only want to update if we need to set 3D attributes
            if(is3D) {
                _instance.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
            }
        }

        if(is3D && _overrideAttenuation) {
            _instance.setProperty(FMOD.Studio.EVENT_PROPERTY.MINIMUM_DISTANCE, _overrideMinDistance);
            _instance.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, _overrideMaxDistance);
        }

        _instance.setPitch(_soundPitch);
        _instance.start();
    }

    public void Stop() {
        StopCoroutine(StartPlayLoop());
        StopInstance();
    }

    /// Le culler garde une liste statique : sans desinscription, decharger une
    /// region y laisserait des references detruites. Il les nettoie aussi de
    /// son cote, mais autant ne pas les y mettre.
    ///
    /// OVERRIDE, pas une nouvelle methode : EventHandler declare OnDestroy en
    /// protected virtual et y notifie ObjectDestroy. La masquer couperait ce
    /// signal a FMOD.
    protected override void OnDestroy() {
        AmbientSoundCuller.Unregister(this);
        base.OnDestroy();
    }

    private void StopInstance() {
        if(_instance.isValid()) {
            _instance.stop(_allowFadeout ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);
            _instance.release();
            if(!_allowFadeout) {
                _instance.clearHandle();
            }
        }
    }

    public bool IsPlaying() {
        if(_instance.isValid()) {
            FMOD.Studio.PLAYBACK_STATE playbackState;
            _instance.getPlaybackState(out playbackState);
            return (playbackState != FMOD.Studio.PLAYBACK_STATE.STOPPED);
        }
        return false;
    }

    protected override void HandleGameEvent(EmitterGameEvent gameEvent) {
        if(_playEvent == gameEvent) {
            Play();
        }
        if(_stopEvent == gameEvent) {
            Stop();
        }
    }
}
