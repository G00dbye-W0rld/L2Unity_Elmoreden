using System;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Header("Volume")]
    [Range(0, 1)]
    [SerializeField] private float _masterVolume = 1;
    [Range(0, 1)]
    [SerializeField] private float _musicVolume = 1;
    [Range(0, 1)]
    [SerializeField] private float _SFXVolume = 1;
    [Range(0, 1)]
    [SerializeField] private float _UIVolume = 1;
    [Range(0, 1)]
    [SerializeField] private float _ambientVolume = 1;
    [SerializeField] private bool _muteWhenNotFocused = true;

    private Bus _masterBus;
    private Bus _musicBus;
    private Bus _SFXBus;
    private Bus _UIBus;
    private Bus _ambientBus;

    private EventReference[] _weaponSwishes;

    // Boucle de nage : instance longue duree geree a la main (start/stop), sur le
    // meme principe que MusicManager - contrairement aux autres sons de ce fichier
    // qui sont tous des PlayOneShot.
    private EventInstance _swimLoopInstance;
    private bool _swimLoopInstanceValid;
    private bool _swimLoopPlaying;

    private static AudioManager _instance;
    public static AudioManager Instance { get { return _instance; } }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Destroy(this);
        }

        SetBuses();
        CacheEvents();
    }

    private void OnDestroy()
    {
        if (_swimLoopPlaying)
        {
            _swimLoopInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        }
    }

    private void SetBuses()
    {
        _masterBus = RuntimeManager.GetBus("bus:/");
        _musicBus = RuntimeManager.GetBus("bus:/Music");
        _SFXBus = RuntimeManager.GetBus("bus:/SFX");
        _UIBus = RuntimeManager.GetBus("bus:/UI");
        _ambientBus = RuntimeManager.GetBus("bus:/Ambient");
    }

    private void CacheEvents()
    {
        _weaponSwishes = new EventReference[12];
        _weaponSwishes[(int)WeaponType.none] = RuntimeManager.PathToEventReference("event:/ItemSound/fist");
        _weaponSwishes[(int)WeaponType.hand] = RuntimeManager.PathToEventReference("event:/ItemSound/fist");
        _weaponSwishes[(int)WeaponType.sword] = RuntimeManager.PathToEventReference("event:/ItemSound/sword_mid");
        _weaponSwishes[(int)WeaponType.bigword] = RuntimeManager.PathToEventReference("event:/ItemSound/sword_great");
        _weaponSwishes[(int)WeaponType.blunt] = RuntimeManager.PathToEventReference("event:/ItemSound/axe");
        _weaponSwishes[(int)WeaponType.bigblunt] = RuntimeManager.PathToEventReference("event:/ItemSound/hammer");
        _weaponSwishes[(int)WeaponType.bow] = RuntimeManager.PathToEventReference("event:/ItemSound/bow_small");
        _weaponSwishes[(int)WeaponType.dagger] = RuntimeManager.PathToEventReference("event:/ItemSound/dagger");
        _weaponSwishes[(int)WeaponType.fist] = RuntimeManager.PathToEventReference("event:/ItemSound/fist");
        _weaponSwishes[(int)WeaponType.dual] = RuntimeManager.PathToEventReference("event:/ItemSound/sword_mid");
        _weaponSwishes[(int)WeaponType.dualfist] = RuntimeManager.PathToEventReference("event:/ItemSound/fist");
        _weaponSwishes[(int)WeaponType.pole] = RuntimeManager.PathToEventReference("event:/ItemSound/spear");

        EventReference swimLoopRef = RuntimeManager.PathToEventReference("event:/ChrSound/swim_loop");
        if (!swimLoopRef.IsNull)
        {
            try
            {
                _swimLoopInstance = RuntimeManager.CreateInstance(swimLoopRef);
                _swimLoopInstanceValid = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"AudioManager: impossible de creer l'instance FMOD pour event:/ChrSound/swim_loop ({e.Message}).");
            }
        }
    }

    // Demarre/arrete la boucle de nage et suit la position du joueur tant qu'elle
    // joue (son 3D, pour que les autres joueurs proches l'entendent positionne).
    public void SetSwimLoopActive(bool active, Vector3 position)
    {
        if (!_swimLoopInstanceValid) return;

        if (active && !_swimLoopPlaying)
        {
            _swimLoopInstance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
            _swimLoopInstance.start();
            _swimLoopPlaying = true;
        }
        else if (!active && _swimLoopPlaying)
        {
            _swimLoopInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _swimLoopPlaying = false;
        }

        if (_swimLoopPlaying)
        {
            _swimLoopInstance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
        }
    }

    private void Update()
    {
        if (_muteWhenNotFocused && !Application.isFocused)
        {
            _masterBus.setVolume(0);
            _musicBus.setVolume(0);
            _SFXBus.setVolume(0);
            _UIBus.setVolume(0);
            _ambientBus.setVolume(0);
        }
        else
        {
            _masterBus.setVolume(_masterVolume * 0.75f);
            _musicBus.setVolume(_musicVolume * 0.7f);
            _SFXBus.setVolume(_SFXVolume);
            _UIBus.setVolume(_UIVolume);
            _ambientBus.setVolume(_ambientVolume * 0.45f);
        }
    }

    public void Play3DSoundByReferenceName(string referenceName, Vector3 position)
    {
        EventReference er = RuntimeManager.PathToEventReference("event:/" + referenceName);
        if (!er.IsNull)
        {
            PlaySound(er, position);
        }
        else
        {
            Debug.LogWarning($"FMOD event not found: event:/{referenceName} (present in the FMOD Studio project but missing from the exported banks?).");
        }
    }

    public void PlayMonsterSound(EntitySoundEvent monsterSoundEvent, string npcClassName, Vector3 position)
    {
        string eventKey = monsterSoundEvent.ToString().ToLower();
        EventReference er = RuntimeManager.PathToEventReference("event:/MonSound/" + npcClassName + "/" + eventKey);
        if (!er.IsNull)
        {
            PlaySound(er, position);
        }
    }

    public void PlayCharacterSound(EntitySoundEvent characterSoundEvent, CharacterModelSound characterRace, Vector3 position)
    {
        string eventKey = characterSoundEvent.ToString();
        EventReference er = RuntimeManager.PathToEventReference("event:/ChrSound/" + characterRace + "/" + characterRace + "_" + eventKey);
        if (!er.IsNull)
        {
            PlaySound(er, position);
        }
    }

    public void PlayUISound(string soundName)
    {
        EventReference er = RuntimeManager.PathToEventReference("event:/InterfaceSound/" + soundName);
        if (!er.IsNull)
        {
            PlaySound(er);
        }
    }

    public void PlayEquipSound(string soundName)
    {
        EventReference er = RuntimeManager.PathToEventReference("event:/ItemSound/" + soundName);
        if (!er.IsNull)
        {
            PlaySound(er);
        }
    }

    public void PlayStepSound(string surfaceTag, Vector3 position)
    {
        string eventKey;
        surfaceTag = surfaceTag.ToLower();

        switch (surfaceTag)
        {
            case "dirt":
                eventKey = surfaceTag + "_run";
                break;
            case "stone":
                eventKey = surfaceTag + "_run";
                break;
            case "wood":
                eventKey = surfaceTag + "_run";
                break;
            default:
                eventKey = "default_run";
                break;

        }

        EventReference er = RuntimeManager.PathToEventReference("event:/StepSound/" + eventKey);
        if (!er.IsNull)
        {
            PlaySound(er, position);
        }
    }

    public void PlayItemSound(ItemSoundEvent itemSoundEvent, Vector3 position)
    {
        EventReference er;
        er = RuntimeManager.PathToEventReference("event:/ItemSound/" + itemSoundEvent.ToString());


        if (!er.IsNull)
        {
            PlaySound(er, position);
        }
    }

    public void PlaySound(EventReference sound, Vector3 postition)
    {
        if (!sound.IsNull)
        {
            RuntimeManager.PlayOneShot(sound, postition);
        }
        else
        {
            Debug.LogWarning("Trying to play a null EventReference sound.");
        }
    }

    public void PlaySound(EventReference sound)
    {
        if (!sound.IsNull)
        {
            RuntimeManager.PlayOneShot(sound);
        }
        else
        {
            Debug.LogWarning("Trying to play a null EventReference sound.");
        }
    }

    public void PlaySwishSound(WeaponType weaponType, Vector3 position)
    {
        PlaySound(_weaponSwishes[(int)weaponType], position);
    }

    // Joue un court son temoin pour previsualiser le niveau d'un bus depuis la
    // fenetre de reglages. Chaque canal passe par SON bus FMOD, donc le son
    // entendu reflete bien le volume qu'on est en train d'ajuster. Musique et
    // Ambiance ne declenchent pas de temoin : ces bus jouent deja en continu en
    // jeu, leur volume change donc en direct a l'oreille quand on bouge le slider.
    public void PlayVolumePreview(string channel)
    {
        switch (channel)
        {
            case "SFX":
                PlaySwishSound(WeaponType.sword, GetListenerPosition());
                break;
            case "UI":
            case "Master":
                PlayUISound("click_01");
                break;
        }
    }

    private Vector3 GetListenerPosition()
    {
        return Camera.main != null ? Camera.main.transform.position : Vector3.zero;
    }

    public float MasterVolume => _masterVolume;
    public float MusicVolume => _musicVolume;
    public float SFXVolume => _SFXVolume;
    public float UIVolume => _UIVolume;
    public float AmbientVolume => _ambientVolume;

    public void SetMasterVolume(float value) => _masterVolume = Mathf.Clamp01(value);
    public void SetMusicVolume(float value) => _musicVolume = Mathf.Clamp01(value);
    public void SetSFXVolume(float value) => _SFXVolume = Mathf.Clamp01(value);
    public void SetUIVolume(float value) => _UIVolume = Mathf.Clamp01(value);
    public void SetAmbientVolume(float value) => _ambientVolume = Mathf.Clamp01(value);
}
