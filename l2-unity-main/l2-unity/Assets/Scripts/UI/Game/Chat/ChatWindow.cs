using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ChatWindow : L2Window
{
    public static int MAXIMUM_INPUT_HISTORY = 25;
    public static int MAXIMUM_MESSAGE_COUNT = 100;

    private VisualTreeAsset _tabTemplate;
    private VisualTreeAsset _tabHeaderTemplate;
    private TextField _chatInput;
    private VisualElement _chatInputContainer;
    private L2TabView _l2TabView;
    private List<string> _history;

    private int _historyIndex = 0;

    [SerializeField] private float _chatWindowMinWidth = 225.0f;
    [SerializeField] private float _chatWindowMaxWidth = 500.0f;
    [SerializeField] private float _chatWindowMinHeight = 175.0f;
    [SerializeField] private float _chatWindowMaxHeight = 600.0f;
    [SerializeField] public ChatTab[] _tabs;
    [SerializeField] private bool _chatOpened = false;
    [SerializeField] private int _chatInputCharacterLimit = 100;

    public bool ChatOpened { get { return _chatOpened; } }

    private static ChatWindow _instance;
    public static ChatWindow Instance { get { return _instance; } }

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

        _history = new List<string>();
    }

    private void OnDestroy()
    {
        _instance = null;
    }

    protected override void LoadAssets()
    {
        _windowTemplate = LoadAsset("Data/UI/_Elements/Game/ChatWindow/ChatWindow");
        _tabTemplate = LoadAsset("Data/UI/_Elements/Game/ChatWindow/ChatTab");
        _tabHeaderTemplate = LoadAsset("Data/UI/_Elements/Game/ChatWindow/ChatTabHeader");
    }

    protected override IEnumerator BuildWindow(VisualElement root)
    {
        InitWindow(root);

        yield return new WaitForEndOfFrame();

        var diagonalResizeHandle = GetElementByClass("resize-diag");

        DiagonalResizeManipulator diagonalResizeManipulator = new DiagonalResizeManipulator(
            diagonalResizeHandle,
            _windowEle,
            _chatWindowMinWidth,
            _chatWindowMaxWidth,
            _chatWindowMinHeight,
            _chatWindowMaxHeight,
            14.5f,
            2f);

        diagonalResizeHandle.AddManipulator(diagonalResizeManipulator);

        VisualElement topEdge = GetElementById("ChatResizeTop");
        topEdge.AddManipulator(new EdgeResizeManipulator(topEdge, _windowEle, EdgeResizeManipulator.Edge.Top, _chatWindowMinHeight, _chatWindowMaxHeight, 14.5f, 2f));

        VisualElement rightEdge = GetElementById("ChatResizeRight");
        rightEdge.AddManipulator(new EdgeResizeManipulator(rightEdge, _windowEle, EdgeResizeManipulator.Edge.Right, _chatWindowMinWidth, _chatWindowMaxWidth, 0f, 0f));

        _chatInput = (TextField)GetElementById("ChatInputField");
        _chatInput.RegisterCallback<FocusEvent>(OnChatInputFocus);
        _chatInput.RegisterCallback<BlurEvent>(OnChatInputBlur);
        _chatInput.maxLength = _chatInputCharacterLimit;

        var enlargeTextBtn = (Button)GetElementById("EnlargeTextBtn");
        enlargeTextBtn.AddManipulator(new ButtonClickSoundManipulator(enlargeTextBtn));
        enlargeTextBtn.RegisterCallback<MouseUpEvent>(evt => CycleFontSize(), TrickleDown.TrickleDown);

        var chatOptionsBtn = (Button)GetElementById("ChatOptionsBtn");
        chatOptionsBtn.AddManipulator(new ButtonClickSoundManipulator(chatOptionsBtn));
        chatOptionsBtn.RegisterCallback<MouseUpEvent>(evt => ChatOptionsWindow.Instance?.Open(), TrickleDown.TrickleDown);

        _chatInput.AddManipulator(new BlinkingCursorManipulator(_chatInput));

        _chatInputContainer = GetElementById("InnerBar");

        CreateTabs();
        ApplyFontSize();

        yield return new WaitForEndOfFrame();
        diagonalResizeManipulator.SnapSize();

        L2GameUI.Instance.WindowLoadComplete();
    }


    // Onglets definis ici plutot que dans UI.prefab : les filtres viennent
    // des reglages, et le prefixe ouvre le nom pour qu'on retrouve d'un coup
    // d'oeil comment ecrire dans le canal.
    private void CreateTabs()
    {
        VisualElement chatTabView = GetElementById("ChatTabView");

        _tabs = new ChatTab[]
        {
            new ChatTab("Role Play", L2MessageType.ROLE_PLAY),
            new ChatTab("HRP", L2MessageType.HRP),
            new ChatTab("+Monde", L2MessageType.TRADE),
            new ChatTab("#Groupe", L2MessageType.PARTY),
            new ChatTab("@Clan", L2MessageType.CLAN),
            new ChatTab("$Alliance", L2MessageType.ALLIANCE)
        };

        _l2TabView = new L2TabView();
        _l2TabView.Initialize(chatTabView, _tabs, _tabTemplate, _tabHeaderTemplate, false);
    }

    void Update()
    {
        if (InputManager.Instance.Validate)
        {
            if (_chatOpened)
            {
                CloseChat(true);
            }
            else
            {
                _historyIndex = _history.Count;
                StartCoroutine(OpenChat());
            }
        }

        if (InputManager.Instance.ArrowDown)
        {
            if (_chatOpened && _history.Count > 0)
            {
                _historyIndex = Mathf.Min(_historyIndex + 1, _history.Count);

                _chatInput.value = _historyIndex < _history.Count ? _history[_historyIndex] : "";
                _chatInput.cursorIndex = _chatInput.value.Length;
            }
        }

        if (InputManager.Instance.ArrowUp)
        {
            if (_chatOpened && _history.Count > 0)
            {
                _historyIndex = Mathf.Max(_historyIndex - 1, 0);

                _chatInput.value = _history[_historyIndex];
                _chatInput.cursorIndex = _chatInput.value.Length;
            }
        }
    }

    IEnumerator OpenChat()
    {
        _chatOpened = true;
        L2GameUI.Instance.BlurFocus();
        yield return new WaitForEndOfFrame();
        _chatInput.Focus();
    }

    public void CloseChat(bool sendMessage)
    {
        _chatOpened = false;

        L2GameUI.Instance.BlurFocus();

        if (sendMessage)
        {
            if (_chatInput.text.Length > 0)
            {
                SendChatMessage(_chatInput.text);

                _history.Add(_chatInput.text);

                // Limit history to 25 messages
                if (_history.Count > MAXIMUM_INPUT_HISTORY)
                {
                    _history.RemoveAt(0);  // Remove oldest entry
                }

                _historyIndex = _history.Count; // Set to end of history

                _chatInput.value = "";
            }
        }
    }

    private void OnChatInputFocus(FocusEvent evt)
    {
        if (!_chatInputContainer.ClassListContains("highlighted"))
        {
            _chatInputContainer.AddToClassList("highlighted");
        }

        if (!_chatOpened)
        {
            _chatOpened = true;
        }
    }

    private void OnChatInputBlur(BlurEvent evt)
    {
        if (_chatInputContainer.ClassListContains("highlighted"))
        {
            _chatInputContainer.RemoveFromClassList("highlighted");
        }

        if (_chatOpened)
        {
            _chatOpened = false;
        }
    }

    public void ClearChat()
    {
        for (int i = 0; i < _tabs.Length; i++)
        {
            ClearTab(i);
        }
    }

    public void ClearTab(int tabIndex)
    {
        if (tabIndex <= _tabs.Length - 1)
        {
            _tabs[tabIndex].Content.text = "";
        }
    }


    public void SendChatMessage(string text)
    {
        if (World.Instance.OfflineMode)
        {
            ChatMessage message = new ChatMessage(PlayerEntity.Instance.Identity.Name, text);
            ReceiveChatMessage(message);
        }
        else
        {
            if (text.StartsWith("//"))
            {
                GameClient.Instance.ClientPacketHandler.SendGMCommand(text.Replace("//", ""));
            }
            else if (text.Equals("/loc", System.StringComparison.OrdinalIgnoreCase))
            {
                // Commande du client d'origine : le serveur repond par un
                // message systeme avec les trois coordonnees.
                GameClient.Instance.ClientPacketHandler.SendUserCommand(UserCommandPacket.Loc);
            }
            else if (text.StartsWith("/invite ", System.StringComparison.OrdinalIgnoreCase))
            {
                // Contrairement aux actions/clic droit, /invite vise un nom
                // tape au clavier - pas besoin d'avoir la cible selectionnee.
                // Le mode de butin n'a d'importance que si ce message cree un
                // nouveau groupe (RequestJoinParty.java l'ignore sinon).
                string targetName = text[8..].Trim().Trim('"');
                if (targetName.Length > 0)
                {
                    int lootRuleId = PartyManager.Instance.IsInParty ? (int)PartyManager.Instance.LootRule : GameSettings.PreferredPartyLootRule;
                    GameClient.Instance.ClientPacketHandler.SendRequestJoinParty(targetName, lootRuleId);
                }
            }
            else if (text.Length > 0)
            {
                // L'onglet actif fixe le canal par defaut (ex. taper sans
                // prefixe pendant que l'onglet "Groupe" est selectionne
                // envoie en Party) - un prefixe explicite (#, !, @...) reste
                // toujours prioritaire sur ce defaut.
                L2MessageType messageType = GetDefaultSendType();
                string target = null;
                bool explicitPrefix = true;

                switch (text[0])
                {
                    case '(':
                        // Ecrire entre parentheses est la convention HRP : on garde
                        // les parentheses dans le texte, elles font partie du ton.
                        messageType = L2MessageType.HRP;
                        explicitPrefix = false;
                        break;
                    case '+':
                        messageType = L2MessageType.TRADE;
                        break;
                    case '!':
                        messageType = L2MessageType.SHOUT;
                        break;
                    case '#':
                        messageType = L2MessageType.PARTY;
                        break;
                    case '@':
                        messageType = L2MessageType.CLAN;
                        break;
                    case '$':
                        messageType = L2MessageType.ALLIANCE;
                        break;
                    case '%':
                        messageType = L2MessageType.HERO_VOICE;
                        break;
                    case '"':
                        // Le destinataire part dans un champ dedie du paquet :
                        // son nom ne doit pas rester dans le texte du message.
                        string rest = text[1..];
                        int separator = rest.IndexOf(' ');
                        target = separator >= 0 ? rest[..separator] : rest;
                        text = separator >= 0 ? rest[(separator + 1)..] : "";
                        messageType = L2MessageType.TELL;
                        explicitPrefix = false;
                        break;
                    default:
                        explicitPrefix = false;
                        break;
                }

                if (explicitPrefix)
                {
                    text = text[1..];
                }

                if (text.Length == 0)
                {
                    return;
                }

                // Depuis l'onglet HRP, la convention est ajoutee toute seule.
                if (messageType == L2MessageType.HRP && !text.StartsWith("("))
                {
                    text = "(" + text + ")";
                }

                GameClient.Instance.ClientPacketHandler.SendMessage(text, messageType, target);
            }
        }
    }

    // Le bouton "Tt" fait tourner la taille du texte, comme dans L2.
    private void CycleFontSize()
    {
        GameSettings.SetChatFontSizeIndex(GameSettings.ChatFontSizeIndex + 1);
        ApplyFontSize();
    }

    private void ApplyFontSize()
    {
        int size = GameSettings.ChatFontSize;
        for (int i = 0; i < _tabs.Length; i++)
        {
            if (_tabs[i].Content != null)
            {
                _tabs[i].Content.style.fontSize = size;
            }
        }
    }

    public List<string> TabNames()
    {
        List<string> names = new List<string>();
        for (int i = 0; i < _tabs.Length; i++)
        {
            names.Add(_tabs[i].TabName);
        }
        return names;
    }

    public VisualElement WindowElement { get { return _windowEle; } }

    private L2MessageType GetDefaultSendType()
    {
        return _l2TabView?.ActiveTab is ChatTab tab ? tab.DefaultSendType : L2MessageType.ROLE_PLAY;
    }

    public void ReceiveChatMessage(ChatMessage message)
    {
        if (message == null)
        {
            return;
        }

        L2MessageType type = message.MessageType;

        if (GameSettings.IsChatFiltered(message.Text))
        {
            return;
        }

        // Annonces et messages des GM : toujours partout, ils ne se reglent pas.
        if (IsAlwaysShown(type))
        {
            AddToEveryTab(message.ToString());
            return;
        }

        // Chaque onglet a sa propre liste de canaux, reglable dans les options.
        for (int i = 0; i < _tabs.Length; i++)
        {
            if (GameSettings.IsChatChannelVisible(i, type))
            {
                _tabs[i].AddMessage(message.ToString());
            }
        }
    }

    private static bool IsAlwaysShown(L2MessageType type)
    {
        return type == L2MessageType.ANNOUNCEMENT
            || type == L2MessageType.CRITICAL_ANNOUNCE
            || type == L2MessageType.GM;
    }

    private void AddToEveryTab(string text)
    {
        for (int i = 0; i < _tabs.Length; i++)
        {
            _tabs[i].AddMessage(text);
        }
    }

    public void ReceiveSystemMessage(SystemMessage message)
    {
        if (message == null)
        {
            return;
        }

        if (GameSettings.ChatSystemWindow && SystemChatWindow.Instance != null)
        {
            SystemChatWindow.Instance.AddMessage(message.ToString());
        }

        for (int i = 0; i < _tabs.Length; i++)
        {
            if (GameSettings.IsChatChannelVisible(i, L2MessageType.SYSTEM_MESSAGE))
            {
                _tabs[i].AddMessage(message.ToString());
            }
        }
    }

    public void ScrollDown(Scroller scroller)
    {
        StartCoroutine(ScrollDownWithDelay(scroller));
    }

    IEnumerator ScrollDownWithDelay(Scroller scroller)
    {
        yield return new WaitForEndOfFrame();
        scroller.value = scroller.highValue > 0 ? scroller.highValue : 0;
    }
}