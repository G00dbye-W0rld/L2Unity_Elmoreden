using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Filtrage du chat, canal par canal et onglet par onglet, plus les mots
// indesirables et la fenetre des messages systeme. Disposition reprise de la
// boite du client Interlude : rien ne part dans les reglages avant Confirmer.
public class ChatOptionsWindow : L2PopupWindow
{
    private static readonly string[] ToggleNames =
    {
        "ToggleRolePlay", "ToggleHrp", "ToggleShout", "ToggleTrade", "ToggleParty",
        "ToggleClan", "ToggleAlliance", "ToggleHero", "ToggleTell", "ToggleSystem"
    };

    private static readonly L2MessageType[] ToggleChannels =
    {
        L2MessageType.ROLE_PLAY, L2MessageType.HRP, L2MessageType.SHOUT,
        L2MessageType.TRADE, L2MessageType.PARTY, L2MessageType.CLAN,
        L2MessageType.ALLIANCE, L2MessageType.HERO_VOICE, L2MessageType.TELL,
        L2MessageType.SYSTEM_MESSAGE
    };

    private readonly List<Toggle> _toggles = new List<Toggle>();
    private readonly List<Button> _tabButtons = new List<Button>();
    private readonly TextField[] _keywordFields = new TextField[GameSettings.ChatKeywordCount];
    private Toggle _systemWindowToggle;

    // Copie de travail : les reglages ne bougent que sur Confirmer.
    private readonly int[] _draftMasks = new int[GameSettings.ChatTabCount];
    private readonly string[] _draftKeywords = new string[GameSettings.ChatKeywordCount];
    private bool _draftSystemWindow;
    private int _tabIndex;

    private static ChatOptionsWindow _instance;
    public static ChatOptionsWindow Instance { get { return _instance; } }

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
        if (_instance == this)
        {
            _instance = null;
        }
    }

    protected override void LoadAssets()
    {
        _windowTemplate = LoadAsset("Data/UI/_Elements/Game/ChatWindow/ChatOptionsWindow");
    }

    protected override void InitWindow(VisualElement root)
    {
        base.InitWindow(root);

        VisualElement dragArea = GetElementByClass("drag-area");
        dragArea.AddManipulator(new DragManipulator(dragArea, _windowEle, this));

        RegisterCloseWindowEvent("btn-close-frame");
        RegisterClickWindowEvent(_windowEle, dragArea);

        MoveContentIntoFrame();
        BindToggles();
        BindKeywords();
        RegisterButton("ConfirmButton", Confirm);
        RegisterButton("CancelButton", () => HideWindow(false));
    }

    // Le modele de fenetre expose un emplacement "Content" imbrique : y
    // deplacer notre contenu le fait vivre DANS le cadre, comme la fenetre de
    // reglages, au lieu de flotter par-dessus.
    private void MoveContentIntoFrame()
    {
        VisualElement content = GetElementById("OptionsContent");
        VisualElement outer = GetElementById("Content");
        VisualElement slot = outer != null ? outer.Q<VisualElement>("Content") : null;

        if (content == null || slot == null || content.parent == slot)
        {
            return;
        }

        content.RemoveFromHierarchy();
        slot.Add(content);
    }

    // Reconstruit a chaque ouverture : au chargement de l'interface, le chat
    // n'a pas encore remplace les onglets du prefab par les siens.
    private void BuildTabSelector()
    {
        VisualElement selector = GetElementById("TabSelector");
        selector.Clear();
        _tabButtons.Clear();

        List<string> names = ChatWindow.Instance != null ? ChatWindow.Instance.TabNames() : new List<string>();

        for (int i = 0; i < names.Count && i < GameSettings.ChatTabCount; i++)
        {
            int index = i;
            Button button = new Button();
            button.text = names[i];
            button.AddToClassList("chat-opt-tab-btn");
            button.AddManipulator(new ButtonClickSoundManipulator(button));
            button.RegisterCallback<MouseUpEvent>(evt => SelectTab(index), TrickleDown.TrickleDown);

            _tabButtons.Add(button);
            selector.Add(button);
        }
    }

    private void BindToggles()
    {
        for (int i = 0; i < ToggleNames.Length; i++)
        {
            L2MessageType channel = ToggleChannels[i];
            Toggle toggle = _windowEle.Q<Toggle>(ToggleNames[i]);
            _toggles.Add(toggle);

            if (toggle == null)
            {
                continue;
            }

            toggle.RegisterValueChangedCallback(evt =>
            {
                int bit = 1 << (int)channel;
                _draftMasks[_tabIndex] = evt.newValue
                    ? (_draftMasks[_tabIndex] | bit)
                    : (_draftMasks[_tabIndex] & ~bit);
            });
        }

        _systemWindowToggle = _windowEle.Q<Toggle>("SystemWindowToggle");
        if (_systemWindowToggle != null)
        {
            _systemWindowToggle.RegisterValueChangedCallback(evt => _draftSystemWindow = evt.newValue);
        }
    }

    private void BindKeywords()
    {
        for (int i = 0; i < _keywordFields.Length; i++)
        {
            int index = i;
            TextField field = _windowEle.Q<TextField>("Keyword" + i);
            _keywordFields[i] = field;

            if (field == null)
            {
                continue;
            }

            field.RegisterCallback<FocusEvent>(evt => L2GameUI.Instance.IsTyping = true);
            field.RegisterCallback<BlurEvent>(evt => L2GameUI.Instance.IsTyping = false);
            field.RegisterValueChangedCallback(evt => _draftKeywords[index] = evt.newValue);

            RegisterButton("ClearKeyword" + i, () =>
            {
                _draftKeywords[index] = "";
                _keywordFields[index].SetValueWithoutNotify("");
            });
        }
    }

    private void RegisterButton(string id, System.Action action)
    {
        VisualElement holder = GetElementById(id);
        Button button = holder != null ? holder.Q<Button>("L2Button") : null;
        if (button == null)
        {
            return;
        }

        button.AddManipulator(new ButtonClickSoundManipulator(button));
        button.RegisterCallback<MouseUpEvent>(evt => action(), TrickleDown.TrickleDown);
    }

    protected override IEnumerator BuildWindow(VisualElement root)
    {
        InitWindow(root);

        yield return new WaitForEndOfFrame();

        CenterWindow();
        HideWindow(true);

        // Pas de WindowLoadComplete : fenetre ajoutee par code dans L2GameUI,
        // elle ne compte pas dans les fenetres du prefab.
    }

    public void Open()
    {
        for (int i = 0; i < GameSettings.ChatTabCount; i++)
        {
            _draftMasks[i] = GameSettings.GetChatTabChannels(i);
        }

        for (int i = 0; i < _draftKeywords.Length; i++)
        {
            _draftKeywords[i] = GameSettings.ChatKeywords[i];
        }

        _draftSystemWindow = GameSettings.ChatSystemWindow;

        BuildTabSelector();
        SelectTab(_tabIndex);
        RefreshKeywords();

        if (_systemWindowToggle != null)
        {
            _systemWindowToggle.SetValueWithoutNotify(_draftSystemWindow);
        }

        ShowWindow();
    }

    private void SelectTab(int index)
    {
        _tabIndex = Mathf.Clamp(index, 0, GameSettings.ChatTabCount - 1);

        for (int i = 0; i < _tabButtons.Count; i++)
        {
            if (i == _tabIndex)
            {
                _tabButtons[i].AddToClassList("active");
            }
            else
            {
                _tabButtons[i].RemoveFromClassList("active");
            }
        }

        RefreshToggles();
    }

    private void RefreshToggles()
    {
        for (int i = 0; i < _toggles.Count; i++)
        {
            if (_toggles[i] != null)
            {
                int bit = 1 << (int)ToggleChannels[i];
                _toggles[i].SetValueWithoutNotify((_draftMasks[_tabIndex] & bit) != 0);
            }
        }
    }

    private void RefreshKeywords()
    {
        for (int i = 0; i < _keywordFields.Length; i++)
        {
            if (_keywordFields[i] != null)
            {
                _keywordFields[i].SetValueWithoutNotify(_draftKeywords[i]);
            }
        }
    }

    private void Confirm()
    {
        for (int i = 0; i < GameSettings.ChatTabCount; i++)
        {
            GameSettings.SetChatTabChannels(i, _draftMasks[i]);
        }

        for (int i = 0; i < _draftKeywords.Length; i++)
        {
            GameSettings.SetChatKeyword(i, _draftKeywords[i]);
        }

        GameSettings.SetChatSystemWindow(_draftSystemWindow);

        if (SystemChatWindow.Instance != null)
        {
            SystemChatWindow.Instance.SetEnabled(_draftSystemWindow);
        }

        HideWindow(false);
    }
}
