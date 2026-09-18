using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Fenetre de clan, reprise de celle du client d'origine : en-tete, selecteur
// d'unite, liste des membres et actions. Le contenu vient de ClanData, que
// les paquets du serveur tiennent a jour.
public class ClanWindow : L2PopupWindow
{
    private const string StylePath = "Data/UI/_Elements/Game/ClanWindow/ClanWindow";
    private const string AllUnits = "Toutes les unit\u00e9s";

    private ScrollView _memberList;
    private Label _clanName;
    private Label _leaderLine;
    private Label _levelLine;
    private VisualElement _infoClanHall;
    private VisualElement _infoCastle;
    private VisualElement _infoAlliance;
    private VisualElement _infoWar;
    private Label _reputationLine;
    private Label _unitCount;
    private VisualElement _crest;
    private VisualElement _membersPage;
    private Button _membersTab;
    private VisualElement _warsPage;
    private Button _warsTab;
    private ClanWarPanel _wars;
    private VisualElement _allyPage;
    private Button _allyTab;
    private ClanAllyPanel _ally;
    private VisualElement _skillsPage;
    private Button _skillsTab;
    private ClanSkillPanel _skills;
    private Label _emptyLabel;
    private DropdownField _unitFilter;
    private Button _inviteButton;
    private Button _leaveButton;
    private Button _crestButton;

    private readonly List<int> _unitTypes = new List<int>();
    private string _selected = string.Empty;

    private static ClanWindow _instance;
    public static ClanWindow Instance { get { return _instance; } }

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

        ClanData.Changed -= Refresh;
        ClanCrests.Changed -= Refresh;
        ClanWars.Changed -= RefreshWars;
        ClanAlliance.Changed -= RefreshAlly;
    }

    protected override void LoadAssets()
    {
        _windowTemplate = LoadAsset("Data/UI/_Elements/Game/ClanWindow/ClanWindow");
    }

    protected override void InitWindow(VisualElement root)
    {
        base.InitWindow(root);

        // La feuille est aussi declaree dans Lineage2_Game.uxml, mais on ne
        // depend pas de cette resolution : sans style, le contenu sort du cadre.
        StyleSheet sheet = Resources.Load<StyleSheet>(StylePath);
        if (sheet != null)
        {
            _windowEle.styleSheets.Add(sheet);
        }
        else
        {
            Debug.LogError("[Clan] Feuille de style introuvable : " + StylePath);
        }

        VisualElement dragArea = GetElementByClass("drag-area");
        dragArea.AddManipulator(new DragManipulator(dragArea, _windowEle, this));
        RegisterCloseWindowEvent("btn-close-frame");
        RegisterClickWindowEvent(_windowEle, dragArea);

        _memberList = GetElementById("MemberList") as ScrollView;
        _clanName = GetLabelById("ClanName");
        _leaderLine = GetLabelById("LeaderLine");
        _levelLine = GetLabelById("LevelLine");
        _infoClanHall = GetElementById("InfoClanHall");
        _infoCastle = GetElementById("InfoCastle");
        _infoAlliance = GetElementById("InfoAlliance");
        _infoWar = GetElementById("InfoWar");
        _reputationLine = GetLabelById("ReputationLine");
        _unitCount = GetLabelById("UnitCount");
        _emptyLabel = GetLabelById("EmptyLabel");
        _unitFilter = GetElementById("UnitFilter") as DropdownField;
        _crest = GetElementById("Crest");
        // Le blason du bandeau ouvre la fiche du clan, ou s'affiche le grand blason.
        _crest.pickingMode = PickingMode.Position;
        _crest.RegisterCallback<MouseUpEvent>(evt =>
        {
            if (ClanData.HasClan && ClanInfoWindow.Instance != null)
            {
                ClanInfoWindow.Instance.Open(ClanData.ClanId);
            }
        });
        _membersPage = GetElementById("MembersPage");
        _membersTab = GetElementById("MembersTab") as Button;
        _warsPage = GetElementById("WarsPage");
        _warsTab = GetElementById("WarsTab") as Button;
        _wars = new ClanWarPanel(_warsPage);
        _allyPage = GetElementById("AllyPage");
        _allyTab = GetElementById("AllyTab") as Button;
        _ally = new ClanAllyPanel(_allyPage);
        _skillsPage = GetElementById("SkillsPage");
        _skillsTab = GetElementById("SkillsTab") as Button;
        _skills = new ClanSkillPanel(_skillsPage);

        RegisterAction("MembersTab", () => ShowTab(Tab.Members));
        RegisterAction("WarsTab", () => ShowTab(Tab.Wars));
        RegisterAction("AllyTab", () => ShowTab(Tab.Ally));
        RegisterAction("SkillsTab", () => ShowTab(Tab.Skills));

        if (_unitFilter != null)
        {
            _unitFilter.RegisterValueChangedCallback(evt => Refresh());
        }

        RegisterActions();

        ClanData.Changed += Refresh;
        ClanCrests.Changed += Refresh;
        ClanWars.Changed += RefreshWars;
        ClanAlliance.Changed += RefreshAlly;
    }

    private void RegisterActions()
    {
        _inviteButton = RegisterAction("InviteBtn", Invite);
        _leaveButton = RegisterAction("LeaveBtn", LeaveOrDissolve);
        _crestButton = RegisterAction("CrestBtn", () => ChooseCrest(ClanCrests.Kind.Pledge));
        RegisterAction("LargeCrestBtn", () => ChooseCrest(ClanCrests.Kind.Large));
        RegisterAction("RanksBtn", OpenRanks);
    }

    private Button RegisterAction(string id, System.Action action)
    {
        Button button = GetElementById(id) as Button;
        if (button == null)
        {
            return null;
        }

        button.AddManipulator(new ButtonClickSoundManipulator(button));
        button.RegisterCallback<MouseUpEvent>(evt => action(), TrickleDown.TrickleDown);
        return button;
    }

    protected override IEnumerator BuildWindow(VisualElement root)
    {
        InitWindow(root);

        yield return new WaitForEndOfFrame();

        CenterWindow();
        HideWindow(true);

        // Pas de WindowLoadComplete : fenetre ajoutee par code dans L2GameUI.
    }

    public void Open()
    {
        ShowWindow();
        Refresh();

        // La liste n'est pas poussee spontanement : on la demande a l'ouverture.
        if (GameClient.Instance != null && GameClient.Instance.ClientPacketHandler != null)
        {
            GameClient.Instance.ClientPacketHandler.SendRequestPledgeMemberList();
        }
    }

    public override void ToggleHideWindow()
    {
        if (_isWindowHidden)
        {
            Open();
        }
        else
        {
            HideWindow(false);
        }
    }

    // Inviter la cible dans l'unite affichee.
    private void Invite()
    {
        if (!ClanData.HasClan || TargetManager.Instance == null || TargetManager.Instance.Target == null)
        {
            return;
        }

        Entity target = TargetManager.Instance.Target;
        bool isPlayer = target.Identity.EntityType == EntityType.Player || target.Identity.EntityType == EntityType.User;
        if (!isPlayer || target == PlayerEntity.Instance)
        {
            return;
        }

        GameClient.Instance.ClientPacketHandler.SendRequestJoinPledge(target.Identity.Id, SelectedUnit());
    }

    // Le chef ne peut pas quitter son clan : le meme bouton le dissout. Le
    // serveur ne le detruit qu'au terme du delai de dissolution.
    private void LeaveOrDissolve()
    {
        if (!ClanData.HasClan)
        {
            return;
        }

        if (IsLeader())
        {
            L2ConfirmWindow.Instance.ShowWindow(
                "Dissoudre le clan " + ClanData.Name + " ?",
                () => GameClient.Instance.ClientPacketHandler.SendDismissPledge(),
                () => { });
            return;
        }

        L2ConfirmWindow.Instance.ShowWindow(
            "Quitter le clan " + ClanData.Name + " ?",
            () => GameClient.Instance.ClientPacketHandler.SendWithdrawPledge(),
            () => { });
    }

    private static bool IsLeader()
    {
        return ClanData.HasClan && PlayerEntity.Instance != null && PlayerEntity.Instance.Identity.Name == ClanData.LeaderName;
    }

    private enum Tab { Members, Wars, Ally, Skills }

    private void OpenRanks()
    {
        if (ClanData.HasClan && ClanRankWindow.Instance != null)
        {
            ClanRankWindow.Instance.Open();
        }
    }

    /// Cadre de la fenetre, pour poser la fiche d'un membre juste a cote.
    public Rect WindowBounds { get { return _windowEle != null ? _windowEle.worldBound : Rect.zero; } }

    // L'onglet Rangs montre le membre choisi dans la liste, ou le joueur lui-meme.
    private void ShowTab(Tab tab)
    {
        _membersPage.EnableInClassList("hidden", tab != Tab.Members);
        _warsPage.EnableInClassList("hidden", tab != Tab.Wars);
        _allyPage.EnableInClassList("hidden", tab != Tab.Ally);
        _skillsPage.EnableInClassList("hidden", tab != Tab.Skills);
        _membersTab.EnableInClassList("active", tab == Tab.Members);
        _warsTab.EnableInClassList("active", tab == Tab.Wars);
        _allyTab.EnableInClassList("active", tab == Tab.Ally);
        _skillsTab.EnableInClassList("active", tab == Tab.Skills);

        if (tab == Tab.Ally && ClanData.HasClan)
        {
            _ally.Show();
        }

        if (tab == Tab.Wars && ClanData.HasClan)
        {
            _wars.Show();
        }

    }

    private void RefreshAlly()
    {
        if (_ally != null)
        {
            _ally.Refresh();
        }
    }

    private void RefreshWars()
    {
        if (_wars != null)
        {
            _wars.Refresh();
        }
    }

    // Le joueur depose ses images dans le dossier Blasons a cote du jeu (sous-dossiers
    // Alliance et Grand pour les deux autres blasons) : la plus recente est envoyee.
    public static void ChooseCrest(ClanCrests.Kind kind)
    {
        if (!ClanData.HasClan)
        {
            return;
        }

        string folder = CrestFolder(kind);
        string image = LatestImage(folder);

        if (image == null)
        {
            string format = kind == ClanCrests.Kind.Large ? " (256 x 128 conseill\u00e9)" : string.Empty;
            L2ConfirmWindow.Instance.ShowWindow(
                "D\u00e9posez une image PNG ou JPG" + format + " dans :\n" + folder,
                () => { },
                null);
            return;
        }

        string error;
        bool sent = kind == ClanCrests.Kind.Ally ? ClanCrests.UploadAlly(image, out error)
            : kind == ClanCrests.Kind.Large ? ClanCrests.UploadLarge(image, out error)
            : ClanCrests.Upload(image, out error);

        if (!sent)
        {
            L2ConfirmWindow.Instance.ShowWindow(error, () => { }, null);
        }
        else if (kind == ClanCrests.Kind.Large)
        {
            // Le serveur ne rediffuse pas le grand blason : on redemande la fiche.
            ClanCards.Request(ClanData.ClanId);
        }
    }

    private static string CrestFolder(ClanCrests.Kind kind)
    {
        string root = System.IO.Path.Combine(Application.dataPath, "..", "Blasons");
        string sub = kind == ClanCrests.Kind.Ally ? "Alliance" : kind == ClanCrests.Kind.Large ? "Grand" : string.Empty;
        return System.IO.Path.GetFullPath(sub.Length > 0 ? System.IO.Path.Combine(root, sub) : root);
    }

    private static string LatestImage(string folder)
    {
        if (!System.IO.Directory.Exists(folder))
        {
            System.IO.Directory.CreateDirectory(folder);
            return null;
        }

        string latest = null;
        System.DateTime latestTime = System.DateTime.MinValue;

        foreach (string file in System.IO.Directory.GetFiles(folder))
        {
            string extension = System.IO.Path.GetExtension(file).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
            {
                continue;
            }

            System.DateTime time = System.IO.File.GetLastWriteTimeUtc(file);
            if (time > latestTime)
            {
                latestTime = time;
                latest = file;
            }
        }

        return latest;
    }

    private void Refresh()
    {
        if (_memberList == null)
        {
            return;
        }

        RefreshHeader();
        RefreshUnits();
        RefreshMembers();
        RefreshActions();

        if (_skills != null)
        {
            _skills.Refresh();
        }
    }

    private void RefreshHeader()
    {
        string state = ClanData.Dissolution != 0 ? " (dissolution en cours)" : string.Empty;
        _clanName.text = ClanData.HasClan ? ClanData.Name + state : "Sans clan";
        _leaderLine.text = ClanData.HasClan ? ClanData.LeaderName : "Vagabond";
        _levelLine.text = ClanData.HasClan ? "Niveau " + ClanData.Level : string.Empty;
        _reputationLine.text = ClanData.HasClan ? "R\u00e9putation " + ClanData.Reputation.ToString("n0") : string.Empty;

        SetInfo(_infoClanHall, ClanData.ClanHallId != 0, "Salle de clan poss\u00e9d\u00e9e", "Aucune salle de clan");
        SetInfo(_infoCastle, ClanData.CastleId != 0, "Ch\u00e2teau poss\u00e9d\u00e9", "Aucun ch\u00e2teau");
        SetInfo(_infoAlliance, ClanData.AllyId != 0, "Alliance : " + ClanData.AllyName, "Sans alliance");
        SetInfo(_infoWar, ClanData.AtWar, "En guerre", "Aucune guerre en cours");

        RefreshCrest();
    }

    // Case allumee quand l'etat est vrai, avec l'etat ecrit en toutes lettres.
    private static void SetInfo(VisualElement cell, bool on, string onText, string offText)
    {
        if (cell == null)
        {
            return;
        }

        cell.EnableInClassList("on", on && ClanData.HasClan);
        Label text = cell.Q<Label>();
        if (text != null)
        {
            text.text = ClanData.HasClan ? (on ? onText : offText) : string.Empty;
        }
    }

    // Le blason n'est jamais pousse par le serveur : il faut le demander.
    private void RefreshCrest()
    {
        if (_crest == null)
        {
            return;
        }

        ClanCrests.Request(ClanData.CrestId);

        Texture2D crest = ClanCrests.Get(ClanData.CrestId);
        _crest.style.backgroundImage = crest != null ? new StyleBackground(crest) : new StyleBackground();
    }

    // Le selecteur liste le clan principal puis les sous-unites presentes.
    private void RefreshUnits()
    {
        if (_unitFilter == null)
        {
            return;
        }

        _unitTypes.Clear();
        List<string> choices = new List<string>();

        _unitTypes.Add(0);
        choices.Add("Clan principal - " + (ClanData.HasClan ? ClanData.Name : "?"));

        // Une unite tout juste creee n'a pas de membre : elle doit pourtant
        // apparaitre, sinon impossible d'y inviter qui que ce soit.
        foreach (KeyValuePair<int, ClanUnit> unit in ClanData.Units)
        {
            _unitTypes.Add(unit.Key);
            choices.Add(string.IsNullOrEmpty(unit.Value.Name) ? UnitName(unit.Key) : UnitName(unit.Key) + " - " + unit.Value.Name);
        }

        foreach (ClanMemberInfo member in ClanData.Members)
        {
            if (member.PledgeType != 0 && !_unitTypes.Contains(member.PledgeType))
            {
                _unitTypes.Add(member.PledgeType);
                choices.Add(UnitName(member.PledgeType));
            }
        }

        if (_unitTypes.Count > 1)
        {
            _unitTypes.Add(int.MinValue);
            choices.Add(AllUnits);
        }

        int index = Mathf.Clamp(_unitFilter.index, 0, choices.Count - 1);
        _unitFilter.choices = choices;
        _unitFilter.index = index;
    }

    private int SelectedUnit()
    {
        if (_unitFilter == null || _unitFilter.index < 0 || _unitFilter.index >= _unitTypes.Count)
        {
            return 0;
        }

        int unit = _unitTypes[_unitFilter.index];
        return unit == int.MinValue ? 0 : unit;
    }

    private bool ShowsAllUnits()
    {
        return _unitFilter != null
            && _unitFilter.index >= 0
            && _unitFilter.index < _unitTypes.Count
            && _unitTypes[_unitFilter.index] == int.MinValue;
    }

    private void RefreshMembers()
    {
        _memberList.Clear();

        bool allUnits = ShowsAllUnits();
        int unit = SelectedUnit();

        List<ClanMemberInfo> members = new List<ClanMemberInfo>();
        foreach (ClanMemberInfo member in ClanData.Members)
        {
            if (allUnits || member.PledgeType == unit)
            {
                members.Add(member);
            }
        }

        members.Sort(CompareMembers);

        foreach (ClanMemberInfo member in members)
        {
            _memberList.Add(BuildRow(member));
        }

        // Comme le client d'origine : membres en ligne sur effectif de l'unite.
        int online = members.FindAll(m => m.IsOnline).Count;
        _unitCount.text = ClanData.HasClan ? online + " / " + members.Count + " en ligne" : string.Empty;
        _unitCount.tooltip = "Maximum : " + MaxMembers(unit);
        _emptyLabel.text = members.Count == 0
            ? (ClanData.HasClan ? "Aucun membre" : "Vous n'appartenez a aucun clan.")
            : string.Empty;
    }

    private void RefreshActions()
    {
        // Les privileges ne sont pas encore connus du client : on laisse le
        // serveur trancher plutot que de brider a tort un officier.
        if (_inviteButton != null)
        {
            _inviteButton.SetEnabled(ClanData.HasClan);
        }

        if (_leaveButton != null)
        {
            _leaveButton.SetEnabled(ClanData.HasClan);
            _leaveButton.text = IsLeader() ? "Dissoudre le clan" : "Quitter le clan";
        }

        if (_crestButton != null)
        {
            _crestButton.SetEnabled(ClanData.HasClan);
        }
    }

    /// Limites du serveur : 10 a 40 pour le clan selon son niveau, 20 pour
    /// l'academie et les gardes royales, 10 pour les chevaleries.
    private static int MaxMembers(int pledgeType)
    {
        switch (pledgeType)
        {
            case 0:
                switch (ClanData.Level)
                {
                    case 0: return 10;
                    case 1: return 15;
                    case 2: return 20;
                    case 3: return 30;
                    default: return 40;
                }

            case -1:
            case 100:
            case 200:
                return 20;

            default:
                return 10;
        }
    }

    // En ligne d'abord, puis par nom : c'est l'ordre utile en jeu.
    private static int CompareMembers(ClanMemberInfo left, ClanMemberInfo right)
    {
        if (left.IsOnline != right.IsOnline)
        {
            return left.IsOnline ? -1 : 1;
        }

        return string.Compare(left.Name, right.Name, System.StringComparison.OrdinalIgnoreCase);
    }

    private VisualElement BuildRow(ClanMemberInfo member)
    {
        bool isLeader = member.Name == ClanData.LeaderName;

        VisualElement row = new VisualElement();
        row.AddToClassList("clan-member");
        row.EnableInClassList("online", member.IsOnline);
        row.EnableInClassList("leader", isLeader);
        row.EnableInClassList("selected", member.Name == _selected);

        // Un clic ouvre la fiche du membre a cote de la fenetre, comme a l'origine.
        string name = member.Name;
        row.RegisterCallback<MouseUpEvent>(evt =>
        {
            _selected = name;
            Refresh();

            if (ClanMemberWindow.Instance != null)
            {
                ClanMemberWindow.Instance.Open(name);
            }
        });

        row.Add(Cell(member.Name, "clan-col-name"));
        row.Add(Cell(member.Level.ToString(), "clan-col-level"));

        VisualElement classIcon = IconCell("clan-col-class", "clan-class-icon");
        Texture2D mark = ClassMark(member.ClassId, member.Race);
        if (mark != null)
        {
            classIcon.style.backgroundImage = new StyleBackground(mark);
        }
        row.Add(classIcon.parent);

        VisualElement status = IconCell("clan-col-status", "clan-status-icon");
        status.EnableInClassList("online", member.IsOnline);
        row.Add(status.parent);

        return row;
    }

    // Case de colonne contenant une icone centree ; renvoie l'icone.
    private static VisualElement IconCell(string columnClass, string iconClass)
    {
        VisualElement holder = new VisualElement();
        holder.AddToClassList(columnClass);
        holder.pickingMode = PickingMode.Ignore;

        VisualElement icon = new VisualElement();
        icon.AddToClassList(iconClass);
        icon.pickingMode = PickingMode.Ignore;

        holder.Add(icon);
        return icon;
    }

    private static readonly Dictionary<string, Texture2D> _marks = new Dictionary<string, Texture2D>();
    private static readonly string[] RaceMarks = { "human", "elf", "darkelf", "orc", "dwarf" };

    // Embleme de la classe, ou celui de la race pour les classes de depart
    // (le client d'Orfen n'en a pas pour elles).
    public static Texture2D ClassMark(int classId, int race)
    {
        Texture2D mark = LoadMark(classId.ToString());
        if (mark == null && race >= 0 && race < RaceMarks.Length)
        {
            mark = LoadMark(RaceMarks[race]);
        }
        return mark;
    }

    private static Texture2D LoadMark(string key)
    {
        Texture2D mark;
        if (!_marks.TryGetValue(key, out mark))
        {
            mark = Resources.Load<Texture2D>("Data/UI/Assets/Clan/ClassMark/ClassMark_" + key);
            _marks[key] = mark;
        }
        return mark;
    }

    private static Label Cell(string text, string columnClass)
    {
        Label label = new Label(text);
        label.AddToClassList(columnClass);
        label.pickingMode = PickingMode.Ignore;
        return label;
    }

    /// Les sous-unites du serveur : academie, gardes royales, chevaleries.
    public static string UnitName(int pledgeType)
    {
        switch (pledgeType)
        {
            case 0: return "Clan";
            case -1: return "Acad\u00e9mie";
            case 100: return "Garde royale 1";
            case 200: return "Garde royale 2";
            case 1001: return "Chevalerie 1";
            case 1002: return "Chevalerie 2";
            case 2001: return "Chevalerie 3";
            case 2002: return "Chevalerie 4";
            default: return string.Empty;
        }
    }
}
