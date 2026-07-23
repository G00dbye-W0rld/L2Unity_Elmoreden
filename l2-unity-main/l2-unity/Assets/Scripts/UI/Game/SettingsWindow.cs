using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

// Fenetre de reglages systeme.
// - Le CADRE vient de WindowTemplate (bordures + fond + bouton fermer).
// - Le CONTENU (onglets, lignes, pied) vit dans SettingsWindowBody.uxml,
//   injecte ici dans le slot "Content" du template.
// - L'APPARENCE est entierement pilotee par SettingsWindow.uss.
// Les 3 onglets (Video/Audio/Jeu) sont de simples panneaux qu'on montre /
// masque en basculant la classe "hidden" (pas de dependance a L2TabView, qui
// s'etait revele fragile ici). Chaque controle applique en direct via
// GameSettings.SetXxx ; un instantane permet a "Annuler" de revenir en arriere.
public class SettingsWindow : L2PopupWindow
{
    private VisualTreeAsset _bodyTemplate;

    private Resolution[] _resolutions;

    private VisualElement _videoPanel;
    private VisualElement _audioPanel;
    private VisualElement _gamePanel;
    private Button _videoTabBtn;
    private Button _audioTabBtn;
    private Button _gameTabBtn;

    private struct SettingsSnapshot
    {
        public float Master, Music, SFX, UI, Ambient;
        public int Quality;
        public int Resolution;
        public bool Fullscreen;
        public int AntiAliasing;
        public bool Shadows;
        public bool GraphicCursor;
    }

    private SettingsSnapshot _snapshot;

    private static SettingsWindow _instance;
    public static SettingsWindow Instance
    {
        get { return _instance; }
    }

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
    }

    private void OnDestroy()
    {
        _instance = null;
    }

    protected override void LoadAssets()
    {
        _windowTemplate = LoadAsset("Data/UI/_Elements/Game/SettingsWindow/SettingsWindow");
        _bodyTemplate = LoadAsset("Data/UI/_Elements/Game/SettingsWindow/SettingsWindowBody");
    }

    protected override void InitWindow(VisualElement root)
    {
        base.InitWindow(root);

        Label windowName = (Label)GetElementById("windows-name-label");
        windowName.text = "Réglages";

        // WindowTemplate imbrique deux VisualElement "Content" : l'exterieur
        // porte le fond de la fenetre, l'interieur est le slot vide a peupler.
        VisualElement outerContent = GetElementById("Content");
        VisualElement innerContent = outerContent.Q<VisualElement>("Content");
        // CloneTree() renvoie un TemplateContainer dont le flex-grow vaut 0 par
        // defaut : il epouse la taille de son contenu au lieu de remplir la
        // fenetre (hauteur fixe). D'ou l'effet "fenetre dans la fenetre" sur les
        // onglets courts (Audio/Jeu). On force ce conteneur a s'etirer. On garde
        // le TemplateContainer (et non son enfant [0]) car c'est lui qui porte la
        // feuille de style SettingsWindow.uss referencee par le body UXML.
        VisualElement body = _bodyTemplate.CloneTree();
        body.style.flexGrow = 1;
        innerContent.Add(body);

        var dragArea = GetElementByClass("drag-area");
        DragManipulator drag = new DragManipulator(dragArea, _windowEle, this);
        dragArea.AddManipulator(drag);

        RegisterCloseWindowEvent("btn-close-frame");
        RegisterClickWindowEvent(_windowEle, dragArea);
    }

    protected override IEnumerator BuildWindow(VisualElement root)
    {
        InitWindow(root);

        yield return new WaitForEndOfFrame();

        CenterWindow();

        SetupTabs();

        GameSettings.ApplyAll();
        BindControls();
        RefreshControlsFromGameSettings();
        BindFooter();

        _snapshot = CaptureSnapshot();

        HideWindow(true);

        L2GameUI.Instance.WindowLoadComplete();
    }

    private void SetupTabs()
    {
        _videoPanel = GetElementById("VideoPanel");
        _audioPanel = GetElementById("AudioPanel");
        _gamePanel = GetElementById("GamePanel");

        _videoTabBtn = (Button)GetElementById("VideoTabBtn");
        _audioTabBtn = (Button)GetElementById("AudioTabBtn");
        _gameTabBtn = (Button)GetElementById("GameTabBtn");

        _videoTabBtn.AddManipulator(new ButtonClickSoundManipulator(_videoTabBtn));
        _audioTabBtn.AddManipulator(new ButtonClickSoundManipulator(_audioTabBtn));
        _gameTabBtn.AddManipulator(new ButtonClickSoundManipulator(_gameTabBtn));

        _videoTabBtn.RegisterCallback<ClickEvent>(evt => ShowTab(0));
        _audioTabBtn.RegisterCallback<ClickEvent>(evt => ShowTab(1));
        _gameTabBtn.RegisterCallback<ClickEvent>(evt => ShowTab(2));

        ShowTab(0);
    }

    private void ShowTab(int index)
    {
        SetPanelVisible(_videoPanel, _videoTabBtn, index == 0);
        SetPanelVisible(_audioPanel, _audioTabBtn, index == 1);
        SetPanelVisible(_gamePanel, _gameTabBtn, index == 2);
    }

    private void SetPanelVisible(VisualElement panel, Button tabButton, bool visible)
    {
        panel.EnableInClassList("hidden", !visible);
        tabButton.EnableInClassList("active", visible);
    }

    private void BindControls()
    {
        DropdownField resolutionDropdown = (DropdownField)GetElementById("ResolutionDropdown");
        PopulateResolutionDropdown(resolutionDropdown);

        Toggle fullscreenToggle = (Toggle)GetElementById("FullscreenToggle");
        fullscreenToggle.RegisterValueChangedCallback(evt => GameSettings.SetFullscreen(evt.newValue));

        DropdownField qualityDropdown = (DropdownField)GetElementById("QualityDropdown");
        qualityDropdown.RegisterValueChangedCallback(evt =>
        {
            GameSettings.SetQualityLevel(qualityDropdown.choices.IndexOf(evt.newValue));
        });

        DropdownField aaDropdown = (DropdownField)GetElementById("AntiAliasingDropdown");
        aaDropdown.RegisterValueChangedCallback(evt =>
        {
            GameSettings.SetAntiAliasingLevel(aaDropdown.choices.IndexOf(evt.newValue));
        });

        Toggle shadowsToggle = (Toggle)GetElementById("ShadowsToggle");
        shadowsToggle.RegisterValueChangedCallback(evt => GameSettings.SetShadowsEnabled(evt.newValue));

        Toggle cursorToggle = (Toggle)GetElementById("GraphicCursorToggle");
        cursorToggle.RegisterValueChangedCallback(evt => GameSettings.SetGraphicCursorEnabled(evt.newValue));

        BindAudioSlider("MasterVolumeSlider", GameSettings.SetMasterVolume, "Master");
        BindAudioSlider("MusicVolumeSlider", GameSettings.SetMusicVolume, "Music");
        BindAudioSlider("SFXVolumeSlider", GameSettings.SetSFXVolume, "SFX");
        BindAudioSlider("UIVolumeSlider", GameSettings.SetUIVolume, "UI");
        BindAudioSlider("AmbientVolumeSlider", GameSettings.SetAmbientVolume, "Ambient");
    }

    // previewChannel : bus FMOD dont on joue un son temoin quand on RELACHE le
    // slider (PointerUpEvent), pour entendre le niveau regle. Sur release et non
    // a chaque changement de valeur, sinon le son serait spamme pendant le glissa.
    private void BindAudioSlider(string elementName, System.Action<float> onChanged, string previewChannel)
    {
        Slider slider = (Slider)GetElementById(elementName);
        slider.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
        slider.RegisterCallback<PointerUpEvent>(evt => AudioManager.Instance.PlayVolumePreview(previewChannel));
    }

    private void PopulateResolutionDropdown(DropdownField dropdown)
    {
        _resolutions = Screen.resolutions;

        var choices = new System.Collections.Generic.List<string>();
        foreach (Resolution r in _resolutions)
        {
            string label = $"{r.width} x {r.height}";
            if (!choices.Contains(label))
            {
                choices.Add(label);
            }
        }
        dropdown.choices = choices;

        dropdown.RegisterValueChangedCallback(evt =>
        {
            int index = System.Array.FindIndex(_resolutions, r => $"{r.width} x {r.height}" == evt.newValue);
            if (index >= 0)
            {
                GameSettings.SetResolutionIndex(index);
            }
        });
    }

    private void RefreshControlsFromGameSettings()
    {
        ((Toggle)GetElementById("FullscreenToggle")).SetValueWithoutNotify(GameSettings.Fullscreen);
        ((Toggle)GetElementById("ShadowsToggle")).SetValueWithoutNotify(GameSettings.ShadowsEnabled);
        ((Toggle)GetElementById("GraphicCursorToggle")).SetValueWithoutNotify(GameSettings.GraphicCursorEnabled);

        DropdownField qualityDropdown = (DropdownField)GetElementById("QualityDropdown");
        qualityDropdown.SetValueWithoutNotify(qualityDropdown.choices[Mathf.Clamp(GameSettings.QualityLevel, 0, qualityDropdown.choices.Count - 1)]);

        DropdownField aaDropdown = (DropdownField)GetElementById("AntiAliasingDropdown");
        aaDropdown.SetValueWithoutNotify(aaDropdown.choices[Mathf.Clamp(GameSettings.AntiAliasingLevel, 0, aaDropdown.choices.Count - 1)]);

        DropdownField resolutionDropdown = (DropdownField)GetElementById("ResolutionDropdown");
        if (resolutionDropdown.choices != null && resolutionDropdown.choices.Count > 0)
        {
            Resolution[] resolutions = _resolutions ?? Screen.resolutions;
            int savedIndex = GameSettings.ResolutionIndex;
            string currentLabel = savedIndex >= 0 && savedIndex < resolutions.Length
                ? $"{resolutions[savedIndex].width} x {resolutions[savedIndex].height}"
                : $"{Screen.currentResolution.width} x {Screen.currentResolution.height}";
            resolutionDropdown.SetValueWithoutNotify(resolutionDropdown.choices.Contains(currentLabel) ? currentLabel : resolutionDropdown.choices[resolutionDropdown.choices.Count - 1]);
        }

        ((Slider)GetElementById("MasterVolumeSlider")).SetValueWithoutNotify(GameSettings.MasterVolume);
        ((Slider)GetElementById("MusicVolumeSlider")).SetValueWithoutNotify(GameSettings.MusicVolume);
        ((Slider)GetElementById("SFXVolumeSlider")).SetValueWithoutNotify(GameSettings.SFXVolume);
        ((Slider)GetElementById("UIVolumeSlider")).SetValueWithoutNotify(GameSettings.UIVolume);
        ((Slider)GetElementById("AmbientVolumeSlider")).SetValueWithoutNotify(GameSettings.AmbientVolume);
    }

    private void BindFooter()
    {
        Button confirmButton = (Button)GetElementById("ConfirmButton");
        confirmButton.AddManipulator(new ButtonClickSoundManipulator(confirmButton));
        confirmButton.RegisterCallback<ClickEvent>(evt => HandleConfirmClick());

        Button cancelButton = (Button)GetElementById("CancelButton");
        cancelButton.AddManipulator(new ButtonClickSoundManipulator(cancelButton));
        cancelButton.RegisterCallback<ClickEvent>(evt => HandleCancelClick());

        Button applyButton = (Button)GetElementById("ApplyButton");
        applyButton.AddManipulator(new ButtonClickSoundManipulator(applyButton));
        applyButton.RegisterCallback<ClickEvent>(evt => HandleApplyClick());
    }

    private void HandleConfirmClick()
    {
        _snapshot = CaptureSnapshot();
        HideWindow(false);
    }

    private void HandleCancelClick()
    {
        ApplySnapshot(_snapshot);
        RefreshControlsFromGameSettings();
        HideWindow(false);
    }

    private void HandleApplyClick()
    {
        _snapshot = CaptureSnapshot();
    }

    private SettingsSnapshot CaptureSnapshot()
    {
        return new SettingsSnapshot
        {
            Master = GameSettings.MasterVolume,
            Music = GameSettings.MusicVolume,
            SFX = GameSettings.SFXVolume,
            UI = GameSettings.UIVolume,
            Ambient = GameSettings.AmbientVolume,
            Quality = GameSettings.QualityLevel,
            Resolution = GameSettings.ResolutionIndex,
            Fullscreen = GameSettings.Fullscreen,
            AntiAliasing = GameSettings.AntiAliasingLevel,
            Shadows = GameSettings.ShadowsEnabled,
            GraphicCursor = GameSettings.GraphicCursorEnabled,
        };
    }

    private void ApplySnapshot(SettingsSnapshot snap)
    {
        GameSettings.SetMasterVolume(snap.Master);
        GameSettings.SetMusicVolume(snap.Music);
        GameSettings.SetSFXVolume(snap.SFX);
        GameSettings.SetUIVolume(snap.UI);
        GameSettings.SetAmbientVolume(snap.Ambient);
        GameSettings.SetQualityLevel(snap.Quality);
        GameSettings.SetResolutionIndex(snap.Resolution);
        GameSettings.SetFullscreen(snap.Fullscreen);
        GameSettings.SetAntiAliasingLevel(snap.AntiAliasing);
        GameSettings.SetShadowsEnabled(snap.Shadows);
        GameSettings.SetGraphicCursorEnabled(snap.GraphicCursor);
    }

    public override void ShowWindow()
    {
        base.ShowWindow();
        AudioManager.Instance.PlayUISound("system_open_01");
        L2GameUI.Instance.WindowOpened(this);
        _snapshot = CaptureSnapshot();
    }

    public override void HideWindow(bool silent)
    {
        base.HideWindow(silent);

        if (!silent)
            AudioManager.Instance.PlayUISound("system_close_01");

        L2GameUI.Instance.WindowClosed(this);
    }
}
