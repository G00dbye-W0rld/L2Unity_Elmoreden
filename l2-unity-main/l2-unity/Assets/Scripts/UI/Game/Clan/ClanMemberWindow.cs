using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Fiche d'un membre, ouverte d'un clic dans la liste du clan et posee a cote
// de la fenetre, comme dans le client d'origine. Construite comme les options
// du chat : contenu glisse dans l'emplacement du cadre.
public class ClanMemberWindow : L2PopupWindow
{
    private const float WindowWidth = 320f;

    private Label _name;
    private Label _class;
    private Label _title;
    private Label _rankLine;
    private Label _unit;
    private Label _sponsor;
    private Label _sponsorKey;
    private Label _sponsorEditKey;
    private Label _unlinkLabel;
    private Label _hint;
    private VisualElement _classIcon;
    private VisualElement _status;
    private VisualElement _sponsorInfoRow;
    private VisualElement _rankRow;
    private VisualElement _unitRow;
    private VisualElement _sponsorRow;
    private VisualElement _unlinkRow;
    private TextField _titleInput;
    private DropdownField _rank;
    private DropdownField _unitSelect;
    private DropdownField _sponsorSelect;
    private Button _dismissButton;

    private readonly List<int> _unitTypes = new List<int>();
    private readonly List<string> _sponsorNames = new List<string>();
    private string _member = string.Empty;
    private int _syncedGrade;
    private bool _titleSynced;

    private static ClanMemberWindow _instance;
    public static ClanMemberWindow Instance { get { return _instance; } }

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

        ClanRanks.Changed -= Refresh;
        ClanData.Changed -= Refresh;
    }

    protected override void LoadAssets()
    {
        _windowTemplate = LoadAsset("Data/UI/_Elements/Game/ClanWindow/ClanMemberWindow");
    }

    protected override void InitWindow(VisualElement root)
    {
        base.InitWindow(root);

        VisualElement dragArea = GetElementByClass("drag-area");
        dragArea.AddManipulator(new DragManipulator(dragArea, _windowEle, this));

        RegisterCloseWindowEvent("btn-close-frame");
        RegisterClickWindowEvent(_windowEle, dragArea);

        MoveContentIntoFrame();

        VisualElement page = GetElementById("MemberContent");
        _name = page.Q<Label>("MemberName");
        _class = page.Q<Label>("MemberClass");
        _title = page.Q<Label>("MemberTitle");
        _rankLine = page.Q<Label>("MemberRankLine");
        _unit = page.Q<Label>("MemberUnit");
        _sponsor = page.Q<Label>("MemberSponsor");
        _sponsorKey = page.Q<Label>("MemberSponsorKey");
        _sponsorEditKey = page.Q<Label>("SponsorKey");
        _unlinkLabel = page.Q<Label>("UnlinkLabel");
        _hint = page.Q<Label>("ManageHint");
        _classIcon = page.Q<VisualElement>("MemberClassIcon");
        _status = page.Q<VisualElement>("MemberStatus");
        _sponsorInfoRow = page.Q<VisualElement>("SponsorInfoRow");
        _rankRow = page.Q<VisualElement>("RankRow");
        _unitRow = page.Q<VisualElement>("UnitRow");
        _sponsorRow = page.Q<VisualElement>("SponsorRow");
        _unlinkRow = page.Q<VisualElement>("UnlinkRow");
        _titleInput = page.Q<VisualElement>("TitleInput").Q<TextField>("L2Input");
        _titleInput.maxLength = 16;
        _rank = page.Q<DropdownField>("MemberRank");
        _unitSelect = page.Q<DropdownField>("MemberUnitSelect");
        _sponsorSelect = page.Q<DropdownField>("SponsorSelect");

        _rank.choices = ClanRankPanel.RankNames();
        _rank.index = 4;

        // Sans cela, taper un titre deplacerait le personnage.
        _titleInput.RegisterCallback<FocusEvent>(evt => L2GameUI.Instance.IsTyping = true);
        _titleInput.RegisterCallback<BlurEvent>(evt => L2GameUI.Instance.IsTyping = false);

        Register(page, "ApplyTitleBtn", GiveTitle);
        Register(page, "ApplyRankBtn", ApplyRank);
        Register(page, "MoveUnitBtn", MoveUnit);
        Register(page, "LinkSponsorBtn", () => SetSponsor(true));
        Register(page, "UnlinkSponsorBtn", () => SetSponsor(false));
        _dismissButton = Register(page, "DismissBtn", Dismiss);
        Register(page, "CloseBtn", () => HideWindow(false));

        ClanRanks.Changed += Refresh;
        ClanData.Changed += Refresh;
    }

    private static Button Register(VisualElement page, string name, System.Action action)
    {
        Button button = page.Q<Button>(name);
        button.AddManipulator(new ButtonClickSoundManipulator(button));
        button.RegisterCallback<MouseUpEvent>(evt => action(), TrickleDown.TrickleDown);
        return button;
    }

    // Q() teste aussi l'element de depart : il renvoyait le conteneur externe au
    // lieu de l'emplacement, et laissait un cadre vide en haut de la fenetre.
    private static VisualElement InnerSlot(VisualElement outer)
    {
        if (outer == null)
        {
            return null;
        }

        foreach (VisualElement child in outer.Children())
        {
            if (child.name == "Content")
            {
                return child;
            }
        }

        return null;
    }

    // Meme deplacement que les options du chat : le contenu vit DANS le cadre.
    private void MoveContentIntoFrame()
    {
        VisualElement content = GetElementById("MemberContent");
        VisualElement outer = GetElementById("Content");
        VisualElement slot = InnerSlot(outer);

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

    /// La fenetre de clan revient au premier plan au clic : on repasse devant.
    public void Open(string member)
    {
        bool wasHidden = _isWindowHidden;

        if (member != _member)
        {
            _member = member;
            _syncedGrade = 0;
            _titleSynced = false;
            _titleInput.SetValueWithoutNotify(string.Empty);
        }

        ShowWindow();
        BringToFront();

        if (wasHidden)
        {
            PlaceBesideClanWindow();
        }

        GameClient.Instance.ClientPacketHandler.SendRequestPledgeMemberInfo(0, member);
        Refresh();
    }

    // A droite de la fenetre de clan, ou a gauche si l'ecran manque de place.
    // CenterWindow laisse une translation de -50 % : on l'annule avant de poser.
    private void PlaceBesideClanWindow()
    {
        if (ClanWindow.Instance == null || _windowEle.parent == null)
        {
            return;
        }

        Rect clan = ClanWindow.Instance.WindowBounds;
        float screenWidth = _windowEle.parent.layout.width;
        if (clan.width <= 0f || float.IsNaN(screenWidth))
        {
            return;
        }

        float left = clan.xMax + 4f;
        if (left + WindowWidth > screenWidth)
        {
            left = Mathf.Max(0f, clan.xMin - WindowWidth - 4f);
        }

        _windowEle.style.translate = new StyleTranslate(new Translate(0, 0));
        _windowEle.style.left = left;
        _windowEle.style.top = clan.yMin;
        _windowEle.style.right = StyleKeyword.Null;
        _windowEle.style.bottom = StyleKeyword.Null;
    }

    private void Refresh()
    {
        if (_name == null || _isWindowHidden)
        {
            return;
        }

        ClanMemberInfo member = FindMember(_member);
        if (member == null)
        {
            HideWindow(true);
            return;
        }

        ClanRanks.Member info = ClanRanks.Selected;
        bool known = info.Name == _member;
        bool isLeader = _member == ClanData.LeaderName;
        bool isSelf = PlayerEntity.Instance != null && PlayerEntity.Instance.Identity.Name == _member;
        bool academy = member.PledgeType == -1;
        ClanUnit academyUnit;
        bool hasAcademy = ClanData.TryGetUnit(-1, out academyUnit);
        string partner = known ? info.SponsorName : string.Empty;
        bool linked = !string.IsNullOrEmpty(partner);
        string role = academy ? "Parrain" : "Apprenti";

        _name.text = _member;
        _class.text = ClassNames.Get(member.ClassId) + "  -  niveau " + member.Level + (member.IsOnline ? string.Empty : "  -  hors ligne");
        Texture2D mark = ClanWindow.ClassMark(member.ClassId, member.Race);
        _classIcon.style.backgroundImage = mark != null ? new StyleBackground(mark) : new StyleBackground();
        _status.EnableInClassList("online", member.IsOnline);

        _title.text = !known ? "..." : (string.IsNullOrEmpty(info.Title) ? "aucun" : info.Title);
        _rankLine.text = isLeader ? "Chef du clan" : (known && info.PowerGrade >= 1 && info.PowerGrade <= 9 ? _rank.choices[info.PowerGrade - 1] : "...");
        _unit.text = UnitLabel(member.PledgeType);
        _sponsorKey.text = role;
        _sponsor.text = linked ? partner : "aucun";
        SetVisible(_sponsorInfoRow, hasAcademy || linked);

        // On ne recale les champs que sur une information nouvelle du serveur,
        // sinon chaque rafraichissement effacerait la saisie en cours.
        if (known && info.PowerGrade >= 1 && info.PowerGrade <= 9 && info.PowerGrade != _syncedGrade)
        {
            _syncedGrade = info.PowerGrade;
            _rank.SetValueWithoutNotify(_rank.choices[info.PowerGrade - 1]);
        }

        if (known && !_titleSynced)
        {
            _titleSynced = true;
            _titleInput.SetValueWithoutNotify(info.Title ?? string.Empty);
        }

        RefreshUnitChoices(member);
        RefreshSponsorChoices(member);

        // Une action impossible est masquee plutot que grisee : la fiche ne
        // montre que ce qu'on peut vraiment faire sur ce membre.
        SetVisible(_rankRow, !isLeader);
        SetVisible(_unitRow, !isLeader && !academy && _unitTypes.Count > 0);
        _sponsorEditKey.text = role;
        SetVisible(_sponsorRow, hasAcademy && !linked && _sponsorNames.Count > 0);
        _unlinkLabel.text = linked ? "Lien avec " + partner : string.Empty;
        SetVisible(_unlinkRow, linked);
        SetVisible(_dismissButton, !isLeader && !isSelf);

        bool hasUnits = new List<KeyValuePair<int, ClanUnit>>(ClanData.Units).Count > 0;
        _hint.text = hasUnits ? string.Empty : "Acad\u00e9mie, gardes royales et chevaliers se cr\u00e9ent aupr\u00e8s d'un ma\u00eetre de village.";
    }

    private static void SetVisible(VisualElement element, bool visible)
    {
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static string UnitLabel(int pledgeType)
    {
        if (pledgeType == 0)
        {
            return "Clan principal - " + ClanData.Name;
        }

        ClanUnit unit;
        string name = ClanData.TryGetUnit(pledgeType, out unit) ? unit.Name : string.Empty;
        return string.IsNullOrEmpty(name) ? ClanWindow.UnitName(pledgeType) : ClanWindow.UnitName(pledgeType) + " - " + name;
    }

    // Unites de destination : le clan principal et les gardes ou chevaleries
    // existantes, jamais l'academie ni l'unite actuelle.
    private void RefreshUnitChoices(ClanMemberInfo member)
    {
        int previous = _unitSelect.index >= 0 && _unitSelect.index < _unitTypes.Count ? _unitTypes[_unitSelect.index] : int.MinValue;

        _unitTypes.Clear();
        List<string> choices = new List<string>();

        if (member.PledgeType != 0)
        {
            _unitTypes.Add(0);
            choices.Add("Clan principal");
        }

        foreach (KeyValuePair<int, ClanUnit> unit in ClanData.Units)
        {
            if (unit.Key != -1 && unit.Key != member.PledgeType)
            {
                _unitTypes.Add(unit.Key);
                choices.Add(ClanWindow.UnitName(unit.Key) + (string.IsNullOrEmpty(unit.Value.Name) ? string.Empty : " - " + unit.Value.Name));
            }
        }

        _unitSelect.choices = choices;
        int index = _unitTypes.IndexOf(previous);
        _unitSelect.SetValueWithoutNotify(choices.Count > 0 ? choices[index >= 0 ? index : 0] : string.Empty);
    }

    // Un apprenti de l'academie se lie a un membre du clan, et inversement ;
    // seuls les membres encore libres sont proposes.
    private void RefreshSponsorChoices(ClanMemberInfo member)
    {
        bool academy = member.PledgeType == -1;
        string previous = _sponsorSelect.value;

        _sponsorNames.Clear();
        foreach (ClanMemberInfo other in ClanData.Members)
        {
            bool otherAcademy = other.PledgeType == -1;
            if (other.Name != member.Name && otherAcademy != academy && !other.HasSponsor)
            {
                _sponsorNames.Add(other.Name);
            }
        }

        _sponsorSelect.choices = new List<string>(_sponsorNames);
        _sponsorSelect.SetValueWithoutNotify(_sponsorNames.Contains(previous) ? previous : (_sponsorNames.Count > 0 ? _sponsorNames[0] : string.Empty));
    }

    private static ClanMemberInfo FindMember(string name)
    {
        foreach (ClanMemberInfo member in ClanData.Members)
        {
            if (member.Name == name)
            {
                return member;
            }
        }
        return null;
    }

    private void RequestInfo()
    {
        GameClient.Instance.ClientPacketHandler.SendRequestPledgeMemberInfo(0, _member);
    }

    // Un titre vide retire le titre actuel ; le serveur verifie le droit.
    private void GiveTitle()
    {
        GameClient.Instance.ClientPacketHandler.SendGiveNickName(_member, _titleInput.value.Trim());
        _titleSynced = false;
        RequestInfo();
    }

    private void ApplyRank()
    {
        GameClient.Instance.ClientPacketHandler.SendSetMemberPowerGrade(_member, _rank.index + 1);
        GameClient.Instance.ClientPacketHandler.SendRequestPledgePowerGradeList();
        RequestInfo();
    }

    private void MoveUnit()
    {
        int index = _unitSelect.index;
        if (index < 0 || index >= _unitTypes.Count)
        {
            return;
        }

        GameClient.Instance.ClientPacketHandler.SendPledgeReorganizeMember(_member, _unitTypes[index], null);
        RequestInfo();
    }

    // Delier : le serveur retrouve lui-meme le partenaire de ce membre.
    private void SetSponsor(bool link)
    {
        ClanRanks.Member info = ClanRanks.Selected;
        string other = link ? _sponsorSelect.value : (info.Name == _member ? info.SponsorName : string.Empty);
        if (string.IsNullOrEmpty(other))
        {
            return;
        }

        GameClient.Instance.ClientPacketHandler.SendPledgeSetAcademyMaster(link, _member, other);
        RequestInfo();
    }

    private void Dismiss()
    {
        string name = _member;
        L2ConfirmWindow.Instance.ShowWindow(
            "Exclure " + name + " du clan ?",
            () =>
            {
                GameClient.Instance.ClientPacketHandler.SendOustPledgeMember(name);
                HideWindow(false);
            },
            () => { });
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
