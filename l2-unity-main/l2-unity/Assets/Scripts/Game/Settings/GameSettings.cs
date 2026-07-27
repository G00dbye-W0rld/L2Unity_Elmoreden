using AtmosphericHeightFog;
using Bitgem.VFX.StylisedWater;
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
    private const string ShadowDistanceKey = "Settings_ShadowDistanceLevel";
    private const string GraphicCursorKey = "Settings_GraphicCursor";
    private const string ViewDistanceKey = "Settings_ViewDistanceLevel";
    private const string WaterDetailKey = "Settings_WaterDetailLevel";
    private const string PreferredPartyLootRuleKey = "Settings_PreferredPartyLootRule";
    private const string DenyPartyRequestsKey = "Settings_DenyPartyRequests";

    private static readonly int[] MsaaSamples = { 1, 2, 4, 8 };
    // Index aligne sur les choix du dropdown "Distance des ombres" (Desactivees/Proches/Moyennes/Lointaines).
    private static readonly float[] ShadowDistances = { 0f, 25f, 50f, 100f };
    // Index aligne sur "Distance d'affichage" (Faible/Moyenne/Elevee/Tres elevee). Facteurs
    // d'ECHELLE relatifs, pas des distances absolues : chaque carte a son propre brouillard
    // (Atmospheric Height Fog) deja calibre par les artistes (ex. la scene Game.unity a
    // fogDistanceStart=179.49/fogDistanceEnd=381) - un chiffre en dur ecrasait ce reglage et
    // pouvait meme inverser start>end (brouillard partout des le palier par defaut). Le far
    // clip de la camera suit la meme echelle pour rester cohérent avec le brouillard.
    private static readonly float[] ViewDistanceScale = { 0.6f, 1f, 1.6f, 2.4f };
    // Index aligne sur "Detail de l'eau" (Basse/Moyenne/Haute). Moyenne = valeurs par defaut du
    // materiau (WaterVolume-URP.shadergraph) : _DetailStrength/_RefractStrength/_BumpStrength.
    private static readonly float[] WaterDetailStrengths = { 0.05f, 0.2f, 0.4f };
    private static readonly float[] WaterRefractStrengths = { 0.03f, 0.08f, 0.16f };
    private static readonly float[] WaterBumpStrengths = { 0.25f, 0.5f, 0.8f };

    private static bool _loaded;
    private static float _cachedFarClipPlane = -1f;
    private static float _cachedFogDistanceStart = -1f;
    private static float _cachedFogDistanceEnd = -1f;

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
    public static int ShadowDistanceLevel { get; private set; } = 2;
    public static bool GraphicCursorEnabled { get; private set; } = true;
    public static int ViewDistanceLevel { get; private set; } = 1;
    public static int WaterDetailLevel { get; private set; } = 1;
    // Valeur du mode de butin utilisee comme lootRuleId lors de la toute
    // premiere invitation (RequestJoinParty), qui cree le groupe avec ce mode.
    // Index aligne sur l'enum PartyLootRule (0=ItemLooter...4=ItemOrderSpoil),
    // lui-meme aligne sur les ordinaux du LootRule.java cote serveur.
    public static int PreferredPartyLootRule { get; private set; } = 0;
    public static bool DenyPartyRequests { get; private set; } = false;

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
        ShadowDistanceLevel = PlayerPrefs.GetInt(ShadowDistanceKey, 2);
        GraphicCursorEnabled = PlayerPrefs.GetInt(GraphicCursorKey, 1) == 1;
        ViewDistanceLevel = PlayerPrefs.GetInt(ViewDistanceKey, 1);
        WaterDetailLevel = PlayerPrefs.GetInt(WaterDetailKey, 1);
        PreferredPartyLootRule = PlayerPrefs.GetInt(PreferredPartyLootRuleKey, 0);
        DenyPartyRequests = PlayerPrefs.GetInt(DenyPartyRequestsKey, 0) == 1;
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
            urpAsset.shadowDistance = ShadowDistances[Mathf.Clamp(ShadowDistanceLevel, 0, ShadowDistances.Length - 1)];
        }

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetGraphicCursorEnabled(GraphicCursorEnabled);
        }

        float viewScale = ViewDistanceScale[Mathf.Clamp(ViewDistanceLevel, 0, ViewDistanceScale.Length - 1)];

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            if (_cachedFarClipPlane < 0f)
            {
                _cachedFarClipPlane = mainCamera.farClipPlane;
            }
            mainCamera.farClipPlane = _cachedFarClipPlane * viewScale;
        }

        if (HeightFogGlobal.Instance != null)
        {
            if (_cachedFogDistanceStart < 0f)
            {
                _cachedFogDistanceStart = HeightFogGlobal.Instance.fogDistanceStart;
                _cachedFogDistanceEnd = HeightFogGlobal.Instance.fogDistanceEnd;
            }
            HeightFogGlobal.Instance.fogDistanceStart = _cachedFogDistanceStart * viewScale;
            HeightFogGlobal.Instance.fogDistanceEnd = _cachedFogDistanceEnd * viewScale;
        }

        ApplyWaterDetail();
    }

    // Noms de propriete reels du shader (WaterVolume-URP.shadergraph) : Shader Graph ne genere
    // un nom lisible ("_WaveFrequency" etc.) QUE si la propriete a une "Reference" surchargee
    // dans le Blackboard. _DetailStrength/_RefractStrength/_BumpStrength n'en ont pas -> le
    // materiau les expose seulement sous leur nom auto-genere (Vector1_XXXXXXXX). Un premier
    // essai avec les noms "propres" ne faisait donc rien (Material.SetFloat sur une propriete
    // inexistante echoue silencieusement, pas d'erreur ni d'exception).
    private const string DetailStrengthProperty = "Vector1_46E42935";
    private const string RefractStrengthProperty = "Vector1_A6A0BC26";
    private const string BumpStrengthProperty = "Vector1_B9F56378";

    // Cherche tous les plans d'eau de la scene (meme approche que WaterSurfaceQuery :
    // tous les WaterVolumeBase actifs, pas un singleton, au cas ou une carte en ait
    // plusieurs disjoints) et applique le palier de detail/refraction sur une copie
    // d'instance du materiau (.material, pas .sharedMaterial, pour ne pas modifier l'asset).
    private static void ApplyWaterDetail()
    {
        int idx = Mathf.Clamp(WaterDetailLevel, 0, WaterDetailStrengths.Length - 1);
        WaterVolumeBase[] volumes = Object.FindObjectsByType<WaterVolumeBase>(FindObjectsSortMode.None);
        foreach (WaterVolumeBase volume in volumes)
        {
            MeshRenderer meshRenderer = volume.GetComponent<MeshRenderer>();
            if (meshRenderer == null) continue;

            Material material = meshRenderer.material;
            if (!material.HasProperty(DetailStrengthProperty)) continue;

            material.SetFloat(DetailStrengthProperty, WaterDetailStrengths[idx]);
            material.SetFloat(RefractStrengthProperty, WaterRefractStrengths[idx]);
            material.SetFloat(BumpStrengthProperty, WaterBumpStrengths[idx]);
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

    public static void SetShadowDistanceLevel(int level)
    {
        Load();
        ShadowDistanceLevel = level;
        PlayerPrefs.SetInt(ShadowDistanceKey, ShadowDistanceLevel);
        PlayerPrefs.Save();
        ApplyVideo();
    }

    public static void SetViewDistanceLevel(int level)
    {
        Load();
        ViewDistanceLevel = level;
        PlayerPrefs.SetInt(ViewDistanceKey, ViewDistanceLevel);
        PlayerPrefs.Save();
        ApplyVideo();
    }

    public static void SetWaterDetailLevel(int level)
    {
        Load();
        WaterDetailLevel = level;
        PlayerPrefs.SetInt(WaterDetailKey, WaterDetailLevel);
        PlayerPrefs.Save();
        ApplyWaterDetail();
    }

    public static void SetGraphicCursorEnabled(bool value)
    {
        Load();
        GraphicCursorEnabled = value;
        PlayerPrefs.SetInt(GraphicCursorKey, GraphicCursorEnabled ? 1 : 0);
        PlayerPrefs.Save();
        CursorManager.Instance?.SetGraphicCursorEnabled(GraphicCursorEnabled);
    }

    public static void SetPreferredPartyLootRule(int value)
    {
        Load();
        PreferredPartyLootRule = value;
        PlayerPrefs.SetInt(PreferredPartyLootRuleKey, PreferredPartyLootRule);
        PlayerPrefs.Save();
    }

    public static void SetDenyPartyRequests(bool value)
    {
        Load();
        DenyPartyRequests = value;
        PlayerPrefs.SetInt(DenyPartyRequestsKey, DenyPartyRequests ? 1 : 0);
        PlayerPrefs.Save();
    }
}
