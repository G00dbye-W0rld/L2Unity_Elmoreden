using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

// Fiche publique d'un clan : ouverte depuis la plaque de selection d'un joueur
// ou depuis le blason de notre propre clan. Construite comme la fiche membre.
public class ClanInfoWindow : L2PopupWindow
{
    private Label _name;
    private Label _level;
    private Label _leader;
    private Label _members;
    private Label _ally;
    private Label _base;
    private Label _reputation;
    private Label _war;
    private VisualElement _crest;
    private VisualElement _largeCrest;
    private VisualElement _allyCrest;
    private Button _warButton;
    private int _clanId;

    private static ClanInfoWindow _instance;
    public static ClanInfoWindow Instance { get { return _instance; } }

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

        ClanCards.Changed -= OnCard;
        ClanCrests.Changed -= Refresh;
    }

    protected override void LoadAssets()
    {
        _windowTemplate = LoadAsset("Data/UI/_Elements/Game/ClanWindow/ClanInfoWindow");
    }

    protected override void InitWindow(VisualElement root)
    {
        base.InitWindow(root);

        VisualElement dragArea = GetElementByClass("drag-area");
        dragArea.AddManipulator(new DragManipulator(dragArea, _windowEle, this));

        RegisterCloseWindowEvent("btn-close-frame");
        RegisterClickWindowEvent(_windowEle, dragArea);

        MoveContentIntoFrame();

        VisualElement page = GetElementById("InfoContent");
        _name = page.Q<Label>("InfoName");
        _level = page.Q<Label>("InfoLevel");
        _leader = page.Q<Label>("InfoLeader");
        _members = page.Q<Label>("InfoMembers");
        _ally = page.Q<Label>("InfoAlly");
        _base = page.Q<Label>("InfoBase");
        _reputation = page.Q<Label>("InfoReputation");
        _war = page.Q<Label>("InfoWar");
        _crest = page.Q<VisualElement>("InfoCrest");
        _largeCrest = page.Q<VisualElement>("InfoLargeCrest");
        _allyCrest = page.Q<VisualElement>("InfoAllyCrest");

        _warButton = page.Q<Button>("WarBtn");
        _warButton.AddManipulator(new ButtonClickSoundManipulator(_warButton));
        _warButton.RegisterCallback<MouseUpEvent>(evt => ToggleWar(), TrickleDown.TrickleDown);

        Button close = page.Q<Button>("CloseBtn");
        close.AddManipulator(new ButtonClickSoundManipulator(close));
        close.RegisterCallback<MouseUpEvent>(evt => HideWindow(false), TrickleDown.TrickleDown);

        ClanCards.Changed += OnCard;
        ClanCrests.Changed += Refresh;
    }

    // Q() teste aussi l'element de depart : on prend l'enfant direct "Content".
    private void MoveContentIntoFrame()
    {
        VisualElement content = GetElementById("InfoContent");
        VisualElement outer = GetElementById("Content");
        VisualElement slot = null;

        if (outer != null)
        {
            foreach (VisualElement child in outer.Children())
            {
                if (child.name == "Content")
                {
                    slot = child;
                    break;
                }
            }
        }

        if (content == null || slot == null || content.parent == slot)
        {
            return;
        }

        content.RemoveFromHierarchy();
        slot.Add(content);
    }

    protected override IEnumerator BuildWindow(VisualElement root)
    {
        InitWindow(root);

        yield return new WaitForEndOfFrame();

        CenterWindow();
        HideWindow(true);

        // Pas de WindowLoadComplete : fenetre ajoutee par code dans L2GameUI.
    }

    /// La fiche est toujours redemandee : effectif et guerres changent en jeu.
    public void Open(int clanId)
    {
        if (clanId == 0)
        {
            return;
        }

        _clanId = clanId;
        ShowWindow();
        BringToFront();
        ClanCards.Request(clanId);
        Refresh();
    }

    private void OnCard(ClanCard card)
    {
        if (card.ClanId == _clanId)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (_name == null || _isWindowHidden)
        {
            return;
        }

        ClanCard card;
        if (!ClanCards.TryGet(_clanId, out card))
        {
            _name.text = "...";
            _level.text = _leader.text = _members.text = _ally.text = _base.text = _reputation.text = _war.text = string.Empty;
            _warButton.style.display = DisplayStyle.None;
            return;
        }

        ClanCrests.Request(card.CrestId);
        ClanCrests.RequestAlly(card.AllyCrestId);
        ClanCrests.RequestLarge(card.LargeCrestId);

        SetImage(_crest, card.CrestId != 0 ? ClanCrests.Get(card.CrestId) : null, false);
        SetImage(_allyCrest, card.AllyCrestId != 0 ? ClanCrests.GetAlly(card.AllyCrestId) : null, true);
        SetImage(_largeCrest, card.LargeCrestId != 0 ? ClanCrests.GetLarge(card.LargeCrestId) : null, true);

        _name.text = card.Name;
        _level.text = "Clan de niveau " + card.Level;
        _leader.text = card.LeaderName;
        _members.text = card.OnlineMembers + " en ligne sur " + card.Members;
        _ally.text = card.AllyId != 0 ? card.AllyName : "Sans alliance";
        _base.text = card.CastleId != 0 ? "Ch\u00e2teau" : (card.ClanHallId != 0 ? "Salle de clan" : "Aucune");
        _reputation.text = card.Reputation.ToString("n0");

        bool ownClan = card.ClanId == ClanData.ClanId;
        _war.text = ownClan ? "C'est votre clan." : (!ClanData.HasClan ? "Vous n'appartenez \u00e0 aucun clan." : WarText(card.WarState));

        // Declarer ou arreter la guerre depuis la fiche ; le serveur verifie les droits.
        bool canWar = ClanData.HasClan && !ownClan;
        _warButton.style.display = canWar ? DisplayStyle.Flex : DisplayStyle.None;
        _warButton.text = (card.WarState & 1) != 0 ? "Arr\u00eater la guerre" : "D\u00e9clarer la guerre";
    }

    private static string WarText(int state)
    {
        switch (state)
        {
            case 1: return "Votre clan lui a d\u00e9clar\u00e9 la guerre.";
            case 2: return "Ce clan a d\u00e9clar\u00e9 la guerre \u00e0 votre clan.";
            case 3: return "Guerre mutuelle : ses membres sont attaquables sans Ctrl.";
            default: return "Aucune guerre en cours.";
        }
    }

    private static void SetImage(VisualElement element, Texture2D texture, bool hideWhenEmpty)
    {
        element.style.backgroundImage = texture != null ? new StyleBackground(texture) : new StyleBackground();
        if (hideWhenEmpty)
        {
            element.style.display = texture != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void ToggleWar()
    {
        ClanCard card;
        if (!ClanCards.TryGet(_clanId, out card))
        {
            return;
        }

        string name = card.Name;
        bool atWar = (card.WarState & 1) != 0;
        string question = atWar ? "Arr\u00eater la guerre contre " + name + " ?" : "D\u00e9clarer la guerre \u00e0 " + name + " ?";

        L2ConfirmWindow.Instance.ShowWindow(question, () =>
        {
            if (atWar)
            {
                GameClient.Instance.ClientPacketHandler.SendStopPledgeWar(name);
            }
            else
            {
                GameClient.Instance.ClientPacketHandler.SendStartPledgeWar(name);
            }

            ClanCards.Request(_clanId);
        }, () => { });
    }
    public override void ShowWindow()
    {
        base.ShowWindow();
        L2GameUI.Instance.WindowOpened(this);
    }

    public override void HideWindow(bool silent)
    {
        base.HideWindow(silent);
        L2GameUI.Instance.WindowClosed(this);
    }
}
