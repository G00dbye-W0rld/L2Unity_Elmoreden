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
    private const string ChatTabChannelsKey = "Settings_ChatTabChannels3_";
    private const string ChatFontSizeKey = "Settings_ChatFontSize";
    private const string ChatSystemWindowKey = "Settings_ChatSystemWindow";
    private const string ChatKeywordsKey = "Settings_ChatKeywords";

    private static readonly int[] MsaaSamples = { 1, 2, 4, 8 };
    // Index aligne sur les choix du dropdown "Distance des ombres" (Desactivees/Proches/Moyennes/Lointaines).
    private static readonly float[] ShadowDistances = { 0f, 25f, 50f, 100f };
    // Index aligne sur "Distance d'affichage" (Faible/Moyenne/Elevee/Tres elevee). Facteurs
    // d'ECHELLE relatifs, pas des distances absolues : chaque carte a son propre brouillard
    // (Atmospheric Height Fog) deja calibre par les artistes (ex. la scene Game.unity a
    // fogDistanceStart=179.49/fogDistanceEnd=381) - un chiffre en dur ecrasait ce reglage et
    // pouvait meme inverser start>end (brouillard partout des le palier par defaut). Le far
    // clip de la camera suit la meme echelle, MAIS il est desormais borne par l'horizon du
    // brouillard : monter la distance de vue eclaircit le brouillard sans jamais faire
    // dessiner au-dela de ce qu'il laisse voir. Voir ApplyCameraReach.
    private static readonly float[] ViewDistanceScale = { 0.6f, 1f, 1.6f, 2.4f };

    /// De combien la camera porte au-dela de l'horizon du brouillard. Voir
    /// ApplyCameraReach : juste assez pour que la coupe reste invisible.
    private const float HorizonMargin = 1.15f;
    // Index aligne sur "Detail de l'eau" (Basse/Moyenne/Haute). Moyenne = valeurs par defaut du
    // materiau (WaterVolume-URP.shadergraph) : _DetailStrength/_RefractStrength/_BumpStrength.
    private static readonly float[] WaterDetailStrengths = { 0.05f, 0.2f, 0.4f };
    private static readonly float[] WaterRefractStrengths = { 0.03f, 0.08f, 0.16f };
    private static readonly float[] WaterBumpStrengths = { 0.25f, 0.5f, 0.8f };

    private static bool _loaded;
    private static float _cachedFarClipPlane = -1f;
    private static float _cachedFogDistanceStart = -1f;
    private static float _cachedFogDistanceEnd = -1f;

    /// Densite d'origine, pour les modes exponentiels. Capturee une seule fois :
    /// la relire apres coup rendrait le reglage cumulatif.
    private static float _cachedFogDensity = -1f;

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
    /// Canaux affiches dans chaque onglet de chat, un bit par L2MessageType,
    /// reglables onglet par onglet.
    public const int ChatTabCount = 6;

    /// Taille du texte du chat, cyclee par le bouton "Tt".
    private static readonly int[] ChatFontSizes = { 11, 13, 16 };
    public static int ChatFontSizeIndex { get; private set; }
    public static int ChatFontSize { get { Load(); return ChatFontSizes[ChatFontSizeIndex]; } }

    /// Fenetre separee au-dessus du chat pour les messages systeme.
    public static bool ChatSystemWindow { get; private set; }
    public const int ChatKeywordCount = 3;

    /// Mots indesirables : un message qui en contient un n'apparait nulle part.
    public static string[] ChatKeywords { get; private set; } = new string[ChatKeywordCount];

    // Canaux proposes a la configuration, dans l'ordre d'affichage.
    private static readonly L2MessageType[] FilterableChannels =
    {
        L2MessageType.ROLE_PLAY,
        L2MessageType.HRP,
        L2MessageType.SHOUT,
        L2MessageType.TRADE,
        L2MessageType.PARTY,
        L2MessageType.CLAN,
        L2MessageType.ALLIANCE,
        L2MessageType.HERO_VOICE,
        L2MessageType.TELL,
        L2MessageType.SYSTEM_MESSAGE
    };

    // Par defaut, le Role Play recoit aussi les cris, la voix des heros et les
    // messages systeme ; les autres onglets restent propres.
    private static readonly L2MessageType[][] DefaultTabChannels =
    {
        new[] { L2MessageType.ROLE_PLAY, L2MessageType.SHOUT, L2MessageType.HERO_VOICE, L2MessageType.TELL, L2MessageType.SYSTEM_MESSAGE },
        new[] { L2MessageType.HRP, L2MessageType.TELL },
        new[] { L2MessageType.TRADE, L2MessageType.SHOUT, L2MessageType.HERO_VOICE, L2MessageType.TELL },
        new[] { L2MessageType.PARTY, L2MessageType.TELL },
        new[] { L2MessageType.CLAN, L2MessageType.TELL },
        new[] { L2MessageType.ALLIANCE, L2MessageType.TELL }
    };

    private static int[] _chatTabChannels;

    public static L2MessageType[] ChatFilterableChannels { get { return FilterableChannels; } }

    private static int BuildChannelMask(L2MessageType[] channels)
    {
        int mask = 0;
        foreach (L2MessageType channel in channels)
        {
            mask |= 1 << (int)channel;
        }
        return mask;
    }

    private static void LoadChatTabChannels()
    {
        _chatTabChannels = new int[ChatTabCount];
        for (int i = 0; i < ChatTabCount; i++)
        {
            _chatTabChannels[i] = PlayerPrefs.GetInt(ChatTabChannelsKey + i, BuildChannelMask(DefaultTabChannels[i]));
        }
    }

    public static bool IsChatChannelVisible(int tabIndex, L2MessageType channel)
    {
        Load();
        if (tabIndex < 0 || tabIndex >= ChatTabCount)
        {
            return false;
        }

        return (_chatTabChannels[tabIndex] & (1 << (int)channel)) != 0;
    }

    public static void SetChatChannelVisible(int tabIndex, L2MessageType channel, bool visible)
    {
        Load();
        if (tabIndex < 0 || tabIndex >= ChatTabCount)
        {
            return;
        }

        int bit = 1 << (int)channel;
        _chatTabChannels[tabIndex] = visible
            ? (_chatTabChannels[tabIndex] | bit)
            : (_chatTabChannels[tabIndex] & ~bit);

        PlayerPrefs.SetInt(ChatTabChannelsKey + tabIndex, _chatTabChannels[tabIndex]);
        PlayerPrefs.Save();
    }

    public static int GetChatTabChannels(int tabIndex)
    {
        Load();
        return (tabIndex < 0 || tabIndex >= ChatTabCount) ? 0 : _chatTabChannels[tabIndex];
    }

    public static void SetChatTabChannels(int tabIndex, int mask)
    {
        Load();
        if (tabIndex < 0 || tabIndex >= ChatTabCount)
        {
            return;
        }

        _chatTabChannels[tabIndex] = mask;
        PlayerPrefs.SetInt(ChatTabChannelsKey + tabIndex, mask);
        PlayerPrefs.Save();
    }

    public static void SetChatSystemWindow(bool value)
    {
        Load();
        ChatSystemWindow = value;
        PlayerPrefs.SetInt(ChatSystemWindowKey, ChatSystemWindow ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void SetChatFontSizeIndex(int index)
    {
        Load();
        ChatFontSizeIndex = ((index % ChatFontSizes.Length) + ChatFontSizes.Length) % ChatFontSizes.Length;
        PlayerPrefs.SetInt(ChatFontSizeKey, ChatFontSizeIndex);
        PlayerPrefs.Save();
    }

    public static void SetChatKeyword(int index, string keyword)
    {
        Load();
        if (index < 0 || index >= ChatKeywordCount)
        {
            return;
        }

        ChatKeywords[index] = string.IsNullOrWhiteSpace(keyword) ? "" : keyword.Trim();
        PlayerPrefs.SetString(ChatKeywordsKey, string.Join("\n", ChatKeywords));
        PlayerPrefs.Save();
    }

    /// Vrai si le message contient un mot indesirable.
    public static bool IsChatFiltered(string text)
    {
        Load();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (int i = 0; i < ChatKeywords.Length; i++)
        {
            string keyword = ChatKeywords[i];
            if (!string.IsNullOrEmpty(keyword) && text.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

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
        LoadChatTabChannels();
        ChatFontSizeIndex = Mathf.Clamp(PlayerPrefs.GetInt(ChatFontSizeKey, 0), 0, ChatFontSizes.Length - 1);
        ChatSystemWindow = PlayerPrefs.GetInt(ChatSystemWindowKey, 0) == 1;
        ChatKeywords = SplitKeywords(PlayerPrefs.GetString(ChatKeywordsKey, ""));
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

        // La distance de vue etire aussi le brouillard, pour qu'il reste
        // coherent avec la portee de la camera.
        //
        // Le plugin Atmospheric Height Fog a ete retire : il n'affectait aucun
        // terrain (les shaders MicroSplat ne lisaient pas ses variables) et ne
        // laissait qu'un maillage flottant. On pilote desormais le brouillard
        // integre a URP, applique par le pipeline a toutes les surfaces.
        // Modes exponentiels : pas de distance de fin, c'est la densite qui
        // porte le reglage. Une vue plus lointaine amincit le brouillard, mais
        // jamais au point de depasser la fenetre de streaming.
        if (RenderSettings.fog && RenderSettings.fogMode != FogMode.Linear)
        {
            if (_cachedFogDensity < 0f)
            {
                _cachedFogDensity = RenderSettings.fogDensity;
            }

            RenderSettings.fogDensity = Mathf.Max(
                _cachedFogDensity / viewScale,
                RegionStreamer.MinFogDensity(RenderSettings.fogMode));
        }

        if (RenderSettings.fog && RenderSettings.fogMode == FogMode.Linear)
        {
            if (_cachedFogDistanceStart < 0f)
            {
                _cachedFogDistanceStart = RenderSettings.fogStartDistance;
                _cachedFogDistanceEnd = RenderSettings.fogEndDistance;
            }
            // Le brouillard ne doit JAMAIS porter plus loin que la fenetre de
            // streaming : au-dela, le terrain n'existe pas et le joueur voit le
            // vide a travers un brouillard encore transparent. Le plafond est
            // donc celui du streamer, pas un choix esthetique.
            //
            // Consequence assumee : passe un certain niveau, augmenter la
            // distance de vue n'eloigne plus le brouillard. Seule la portee de
            // la camera continue de croitre. Tant que le cout par region n'aura
            // pas baisse (LOD, imposteurs), c'est la contrainte reelle.
            float end = Mathf.Min(_cachedFogDistanceEnd * viewScale, RegionStreamer.MaxHorizon);

            RenderSettings.fogStartDistance = Mathf.Min(_cachedFogDistanceStart * viewScale, end * 0.6f);
            RenderSettings.fogEndDistance = end;
        }

        // APRES le brouillard, jamais avant : la portee de la camera est bornee
        // par l'horizon, qui depend de la densite qu'on vient d'appliquer.
        ApplyCameraReach(viewScale);

        ApplyWaterDetail();
    }

    // Bornee par l'horizon du brouillard : au-dela, la surface dessinee est
    // invisible. Resolue via CameraController et non Camera.main, qui peut
    // renvoyer la LoadingCamera et figer une reference de 1000 au lieu de 500.
    private static void ApplyCameraReach(float viewScale)
    {
        if (CameraController.Instance == null)
        {
            return;
        }

        Camera cam = CameraController.Instance.GetComponent<Camera>();

        if (cam == null)
        {
            return;
        }

        if (_cachedFarClipPlane < 0f)
        {
            _cachedFarClipPlane = cam.farClipPlane;
        }

        // La marge laisse le brouillard achever de fermer la vue avant la
        // coupe : pile a l'horizon il subsiste 1 % de visibilite, et le bord
        // se verrait comme une ligne nette sur le ciel.
        float limit = RegionStreamer.HorizonDistance(0f) * HorizonMargin;

        cam.farClipPlane = Mathf.Min(_cachedFarClipPlane * viewScale, limit);
    }

    /// Rappel de la portee de camera une fois le joueur en jeu.
    ///
    /// ApplyAll est declenchee a la construction de l'interface, alors que la
    /// camera du joueur n'existe pas encore : ApplyCameraReach renonce donc, a
    /// dessein. Il faut un second passage quand elle est la, sans quoi la
    /// borne ne s'appliquerait qu'a la premiere ouverture des options.
    public static void RefreshCameraReach()
    {
        float viewScale = ViewDistanceScale[Mathf.Clamp(ViewDistanceLevel, 0, ViewDistanceScale.Length - 1)];

        ApplyCameraReach(viewScale);
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

    private static string[] SplitKeywords(string stored)
    {
        string[] keywords = new string[ChatKeywordCount];
        string[] parts = stored.Split('\n');
        for (int i = 0; i < ChatKeywordCount; i++)
        {
            keywords[i] = i < parts.Length ? parts[i] : "";
        }
        return keywords;
    }

    public static void SetDenyPartyRequests(bool value)
    {
        Load();
        DenyPartyRequests = value;
        PlayerPrefs.SetInt(DenyPartyRequestsKey, DenyPartyRequests ? 1 : 0);
        PlayerPrefs.Save();
    }
}
