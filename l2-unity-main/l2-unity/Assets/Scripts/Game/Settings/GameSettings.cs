using UnityEngine;
using UnityEngine.Rendering.Universal;

// Persistance des reglages systeme (audio + video) via PlayerPrefs - premiere
// utilisation de PlayerPrefs dans ce projet, aucun autre mecanisme de
// sauvegarde client-side n'existait jusqu'ici. Les valeurs ici representent
// l'etat CONFIRME (Apply/Confirm cote SettingsWindow) ; la fenetre gere elle
// meme un instantane pour permettre Annuler.
public static class GameSettings
{
    private const string MasterVolumeKey = "Settings_MasterVolume";
    private const string MusicVolumeKey = "Settings_MusicVolume";
    private const string SFXVolumeKey = "Settings_SFXVolume";
    private const string UIVolumeKey = "Settings_UIVolume";
    private const string AmbientVolumeKey = "Settings_AmbientVolume";
    private const string QualityLevelKey = "Settings_QualityLevel";
    private const string ResolutionIndexKey = "Settings_ResolutionIndex";
    private const string FullscreenKey = "Settings_Fullscreen";
    private const string AntiAliasingKey = "Settings_AntiAliasing";
    private const string ShadowsKey = "Settings_ShadowsEnabled";
    private const string GraphicCursorKey = "Settings_GraphicCursor";

    private static readonly int[] MsaaSamples = { 1, 2, 4, 8 };

    private static bool _loaded;
    private static float _cachedShadowDistance = -1f;

    public static float MasterVolume { get; private set; } = 1f;
    public static float MusicVolume { get; private set; } = 1f;
    public static float SFXVolume { get; private set; } = 1f;
    public static float UIVolume { get; private set; } = 1f;
    public static float AmbientVolume { get; private set; } = 1f;
    public static int QualityLevel { get; private set; } = 1;

    // -1 = pas encore choisi -> on garde la resolution courante au premier lancement
    public static int ResolutionIndex { get; private set; } = -1;
    public static bool Fullscreen { get; private set; } = true;
    // Index dans MsaaSamples (0 = desactive, 1 = x2, 2 = x4, 3 = x8)
    public static int AntiAliasingLevel { get; private set; } = 0;
    public static bool ShadowsEnabled { get; private set; } = true;
    public static bool GraphicCursorEnabled { get; private set; } = true;

    private static void Load()
    {
        if (_loaded) return;
        _loaded = true;

        MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        SFXVolume = PlayerPrefs.GetFloat(SFXVolumeKey, 1f);
        UIVolume = PlayerPrefs.GetFloat(UIVolumeKey, 1f);
        AmbientVolume = PlayerPrefs.GetFloat(AmbientVolumeKey, 1f);
        QualityLevel = PlayerPrefs.GetInt(QualityLevelKey, QualitySettings.GetQualityLevel());

        ResolutionIndex = PlayerPrefs.GetInt(ResolutionIndexKey, -1);
        Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        AntiAliasingLevel = PlayerPrefs.GetInt(AntiAliasingKey, 0);
        ShadowsEnabled = PlayerPrefs.GetInt(ShadowsKey, 1) == 1;
        GraphicCursorEnabled = PlayerPrefs.GetInt(GraphicCursorKey, 1) == 1;
    }

    // Applique les valeurs sauvegardees au demarrage (audio + video). A appeler
    // une fois quand l'UI de jeu se construit.
    public static void ApplyAll()
    {
        Load();
        ApplyAudio();
        ApplyVideo();
    }

    private static void ApplyAudio()
    {
        if (AudioManager.Instance == null) return;

        AudioManager.Instance.SetMasterVolume(MasterVolume);
        AudioManager.Instance.SetMusicVolume(MusicVolume);
        AudioManager.Instance.SetSFXVolume(SFXVolume);
        AudioManager.Instance.SetUIVolume(UIVolume);
        AudioManager.Instance.SetAmbientVolume(AmbientVolume);
    }

    private static void ApplyVideo()
    {
        // La qualite doit etre appliquee AVANT l'anti-aliasing/les ombres : chaque
        // palier de qualite pointe vers un asset URP distinct (cf. QualitySettings.asset,
        // "Ulta Low"/"Ultra"), donc QualitySettings.renderPipeline ne renvoie l'asset
        // qu'on veut ajuster qu'une fois le palier selectionne.
        QualitySettings.SetQualityLevel(QualityLevel, true);

        if (Fullscreen && ResolutionIndex >= 0 && ResolutionIndex < Screen.resolutions.Length)
        {
            Resolution r = Screen.resolutions[ResolutionIndex];
            Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
        }
        else if (ResolutionIndex >= 0 && ResolutionIndex < Screen.resolutions.Length)
        {
            Resolution r = Screen.resolutions[ResolutionIndex];
            Screen.SetResolution(r.width, r.height, FullScreenMode.Windowed);
        }
        else
        {
            Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset urpAsset)
        {
            urpAsset.msaaSampleCount = MsaaSamples[Mathf.Clamp(AntiAliasingLevel, 0, MsaaSamples.Length - 1)];

            if (_cachedShadowDistance < 0f)
            {
                _cachedShadowDistance = urpAsset.shadowDistance > 0f ? urpAsset.shadowDistance : 50f;
            }
            urpAsset.shadowDistance = ShadowsEnabled ? _cachedShadowDistance : 0f;
        }

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetGraphicCursorEnabled(GraphicCursorEnabled);
        }
    }

    public static void SetMasterVolume(float value)
    {
        Load();
        MasterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
        AudioManager.Instance?.SetMasterVolume(MasterVolume);
        PlayerPrefs.Save();
    }

    public static void SetMusicVolume(float value)
    {
        Load();
        MusicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        AudioManager.Instance?.SetMusicVolume(MusicVolume);
        PlayerPrefs.Save();
    }

    public static void SetSFXVolume(float value)
    {
        Load();
        SFXVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SFXVolumeKey, SFXVolume);
        AudioManager.Instance?.SetSFXVolume(SFXVolume);
        PlayerPrefs.Save();
    }

    public static void SetUIVolume(float value)
    {
        Load();
        UIVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(UIVolumeKey, UIVolume);
        AudioManager.Instance?.SetUIVolume(UIVolume);
        PlayerPrefs.Save();
    }

    public static void SetAmbientVolume(float value)
    {
        Load();
        AmbientVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(AmbientVolumeKey, AmbientVolume);
        AudioManager.Instance?.SetAmbientVolume(AmbientVolume);
        PlayerPrefs.Save();
    }

    public static void SetQualityLevel(int level)
    {
        Load();
        QualityLevel = level;
        PlayerPrefs.SetInt(QualityLevelKey, QualityLevel);
        PlayerPrefs.Save();
        ApplyVideo();
    }

    public static void SetResolutionIndex(int index)
    {
        Load();
        ResolutionIndex = index;
        PlayerPrefs.SetInt(ResolutionIndexKey, ResolutionIndex);
        PlayerPrefs.Save();
        ApplyVideo();
    }

    public static void SetFullscreen(bool value)
    {
        Load();
        Fullscreen = value;
        PlayerPrefs.SetInt(FullscreenKey, Fullscreen ? 1 : 0);
        PlayerPrefs.Save();
        ApplyVideo();
    }

    public static void SetAntiAliasingLevel(int level)
    {
        Load();
        AntiAliasingLevel = level;
        PlayerPrefs.SetInt(AntiAliasingKey, AntiAliasingLevel);
        PlayerPrefs.Save();
        ApplyVideo();
    }

    public static void SetShadowsEnabled(bool value)
    {
        Load();
        ShadowsEnabled = value;
        PlayerPrefs.SetInt(ShadowsKey, ShadowsEnabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplyVideo();
    }

    public static void SetGraphicCursorEnabled(bool value)
    {
        Load();
        GraphicCursorEnabled = value;
        PlayerPrefs.SetInt(GraphicCursorKey, GraphicCursorEnabled ? 1 : 0);
        PlayerPrefs.Save();
        CursorManager.Instance?.SetGraphicCursorEnabled(GraphicCursorEnabled);
    }
}
