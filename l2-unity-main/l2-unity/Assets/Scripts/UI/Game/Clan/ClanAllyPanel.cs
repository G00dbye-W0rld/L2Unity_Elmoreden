using UnityEngine.UIElements;

// Onglet Alliance de la fenetre de clan. La creation passe par le Village
// Master, comme dans le jeu d'origine ; ici on invite, exclut, quitte ou
// dissout. Le serveur reserve ces actions au chef de l'alliance.
public class ClanAllyPanel
{
    private readonly Label _name;
    private readonly Label _leader;
    private readonly Label _count;
    private readonly ScrollView _list;
    private readonly Button _inviteButton;
    private readonly Button _dismissButton;
    private readonly Button _leaveButton;
    private readonly Button _dissolveButton;

    private string _selected = string.Empty;

    public ClanAllyPanel(VisualElement page)
    {
        _name = page.Q<Label>("AllyName");
        _leader = page.Q<Label>("AllyLeader");
        _count = page.Q<Label>("AllyCount");
        _list = page.Q<ScrollView>("AllyList");
        _inviteButton = page.Q<Button>("AllyInviteBtn");
        _dismissButton = page.Q<Button>("AllyDismissBtn");
        _leaveButton = page.Q<Button>("AllyLeaveBtn");
        _dissolveButton = page.Q<Button>("AllyDissolveBtn");
        Button crestButton = page.Q<Button>("AllyCrestBtn");

        RegisterButton(_inviteButton, Invite);
        RegisterButton(crestButton, () => ClanWindow.ChooseCrest(ClanCrests.Kind.Ally));
        RegisterButton(_dismissButton, () => Confirm("Exclure le clan " + _selected + " de l'alliance ?", () => GameClient.Instance.ClientPacketHandler.SendAllyDismiss(_selected), !string.IsNullOrEmpty(_selected)));
        RegisterButton(_leaveButton, () => Confirm("Quitter l'alliance " + ClanAlliance.Name + " ?", () => GameClient.Instance.ClientPacketHandler.SendAllyLeave(), true));
        RegisterButton(_dissolveButton, () => Confirm("Dissoudre l'alliance " + ClanAlliance.Name + " ?", () => GameClient.Instance.ClientPacketHandler.SendDismissAlly(), true));
    }

    private static void RegisterButton(Button button, System.Action action)
    {
        button.AddManipulator(new ButtonClickSoundManipulator(button));
        button.RegisterCallback<MouseUpEvent>(evt => action(), TrickleDown.TrickleDown);
    }

    private static void Confirm(string question, System.Action action, bool allowed)
    {
        if (!allowed)
        {
            return;
        }

        L2ConfirmWindow.Instance.ShowWindow(question, () => action(), () => { });
    }

    public void Show()
    {
        _selected = string.Empty;

        if (ClanData.AllyId != 0)
        {
            GameClient.Instance.ClientPacketHandler.SendRequestAllyInfo();
        }
        else
        {
            ClanAlliance.Clear();
        }

        Refresh();
    }

    // L'invitation vise le joueur cible, qui doit etre chef de son clan.
    private void Invite()
    {
        if (TargetManager.Instance == null || TargetManager.Instance.Target == null)
        {
            return;
        }

        Entity target = TargetManager.Instance.Target;
        bool isPlayer = target.Identity.EntityType == EntityType.Player || target.Identity.EntityType == EntityType.User;
        if (isPlayer && target != PlayerEntity.Instance)
        {
            GameClient.Instance.ClientPacketHandler.SendJoinAlly(target.Identity.Id);
        }
    }

    public void Refresh()
    {
        bool inAlly = ClanData.AllyId != 0 && ClanAlliance.Known;

        _name.text = inAlly ? ClanAlliance.Name : "Sans alliance";
        _leader.text = inAlly ? "Chef : " + ClanAlliance.LeaderName + " (" + ClanAlliance.LeaderClan + ")" : "Une alliance se fonde aupr\u00e8s d'un Village Master.";
        _count.text = inAlly ? "Membres en ligne : " + ClanAlliance.Online + " / " + ClanAlliance.Total : string.Empty;

        _list.Clear();
        if (inAlly)
        {
            foreach (AllianceInfoPacket.Member clan in ClanAlliance.Clans)
            {
                _list.Add(BuildRow(clan));
            }
        }

        bool leader = inAlly && ClanData.Name == ClanAlliance.LeaderClan
            && PlayerEntity.Instance != null && PlayerEntity.Instance.Identity.Name == ClanData.LeaderName;

        _inviteButton.SetEnabled(leader);
        _dismissButton.SetEnabled(leader && !string.IsNullOrEmpty(_selected) && _selected != ClanData.Name);
        _leaveButton.SetEnabled(inAlly && !leader);
        _dissolveButton.SetEnabled(leader);
    }

    private VisualElement BuildRow(AllianceInfoPacket.Member clan)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("clan-member");
        row.AddToClassList("online");
        row.EnableInClassList("leader", clan.ClanName == ClanAlliance.LeaderClan);
        row.EnableInClassList("selected", clan.ClanName == _selected);

        string name = clan.ClanName;
        row.RegisterCallback<MouseUpEvent>(evt =>
        {
            _selected = _selected == name ? string.Empty : name;
            Refresh();
        });

        row.Add(Cell(clan.ClanName, "clan-col-name"));
        row.Add(Cell(clan.Level.ToString(), "clan-col-level"));
        row.Add(Cell(clan.LeaderName, "clan-col-class"));
        row.Add(Cell(clan.Online + "/" + clan.Total, "clan-col-status"));
        return row;
    }

    private static Label Cell(string text, string columnClass)
    {
        Label label = new Label(text);
        label.AddToClassList(columnClass);
        label.pickingMode = PickingMode.Ignore;
        return label;
    }
}
