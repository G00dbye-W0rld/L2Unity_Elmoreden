using System.Collections.Generic;
using UnityEngine.UIElements;

// Privileges accordes a chaque rang du clan. Le serveur decide de ce qui est
// permis ; le panneau se contente de demander et d'afficher.
public class ClanRankPanel
{
    private struct Privilege
    {
        public string Group;
        public string Label;
        public int Mask;
    }

    private static readonly Privilege[] Privileges =
    {
        new Privilege { Group = "Clan", Label = "Inviter des membres", Mask = 2 },
        new Privilege { Group = "Clan", Label = "G\u00e9rer les titres", Mask = 4 },
        new Privilege { Group = "Clan", Label = "Voir l'entrep\u00f4t", Mask = 8 },
        new Privilege { Group = "Clan", Label = "G\u00e9rer les rangs", Mask = 16 },
        new Privilege { Group = "Clan", Label = "D\u00e9clarer la guerre", Mask = 32 },
        new Privilege { Group = "Clan", Label = "Exclure des membres", Mask = 64 },
        new Privilege { Group = "Clan", Label = "Modifier le blason", Mask = 128 },
        new Privilege { Group = "Clan", Label = "Droits du chef", Mask = 256 },
        new Privilege { Group = "Clan", Label = "G\u00e9rer les niveaux", Mask = 512 },
        new Privilege { Group = "Salle de clan", Label = "Entrer et sortir", Mask = 1024 },
        new Privilege { Group = "Salle de clan", Label = "Utiliser les fonctions", Mask = 2048 },
        new Privilege { Group = "Salle de clan", Label = "Ench\u00e8res", Mask = 4096 },
        new Privilege { Group = "Salle de clan", Label = "Expulser", Mask = 8192 },
        new Privilege { Group = "Salle de clan", Label = "R\u00e9gler les fonctions", Mask = 16384 },
        new Privilege { Group = "Ch\u00e2teau", Label = "Entrer et sortir", Mask = 32768 },
        new Privilege { Group = "Ch\u00e2teau", Label = "Administrer le manoir", Mask = 65536 },
        new Privilege { Group = "Ch\u00e2teau", Label = "G\u00e9rer les si\u00e8ges", Mask = 131072 },
        new Privilege { Group = "Ch\u00e2teau", Label = "Utiliser les fonctions", Mask = 262144 },
        new Privilege { Group = "Ch\u00e2teau", Label = "Expulser", Mask = 524288 },
        new Privilege { Group = "Ch\u00e2teau", Label = "G\u00e9rer les taxes", Mask = 1048576 },
        new Privilege { Group = "Ch\u00e2teau", Label = "Mercenaires", Mask = 2097152 },
        new Privilege { Group = "Ch\u00e2teau", Label = "R\u00e9gler les fonctions", Mask = 4194304 }
    };

    private readonly Label _rankCount;
    private readonly DropdownField _privilegeRank;
    private readonly ScrollView _privilegeList;
    private readonly Button _saveButton;
    private readonly List<Toggle> _toggles = new List<Toggle>();

    public ClanRankPanel(VisualElement page)
    {
        _rankCount = page.Q<Label>("RankCount");
        _privilegeRank = page.Q<DropdownField>("PrivilegeRank");
        _privilegeList = page.Q<ScrollView>("PrivilegeList");
        _saveButton = page.Q<Button>("SavePrivilegesBtn");

        _privilegeRank.choices = RankNames();
        _privilegeRank.index = 4;
        _privilegeRank.RegisterValueChangedCallback(evt => RequestPrivileges());

        BuildPrivilegeList();

        _saveButton.AddManipulator(new ButtonClickSoundManipulator(_saveButton));
        _saveButton.RegisterCallback<MouseUpEvent>(evt => SavePrivileges(), TrickleDown.TrickleDown);
    }

    /// Les neuf rangs du serveur ; le neuvieme est reserve a l'academie.
    public static List<string> RankNames()
    {
        List<string> ranks = new List<string>();
        for (int rank = 1; rank <= 9; rank++)
        {
            ranks.Add(rank == 9 ? "9 - Acad\u00e9mie" : "Rang " + rank);
        }
        return ranks;
    }

    // Meme construction que les options du chat : case sans texte puis libelle.
    private void BuildPrivilegeList()
    {
        string group = string.Empty;

        foreach (Privilege privilege in Privileges)
        {
            if (privilege.Group != group)
            {
                group = privilege.Group;
                Label header = new Label(group);
                header.AddToClassList("rank-group");
                _privilegeList.Add(header);
            }

            VisualElement row = new VisualElement();
            row.AddToClassList("chat-opt-check-row");

            Toggle toggle = new Toggle();
            toggle.AddToClassList("chat-opt-check");
            toggle.userData = privilege.Mask;
            row.Add(toggle);

            Label label = new Label(privilege.Label);
            label.AddToClassList("chat-opt-check-label");
            label.AddToClassList("l2-color-3");
            row.Add(label);

            _privilegeList.Add(row);
            _toggles.Add(toggle);
        }
    }

    /// Les effectifs par rang et les privileges du rang choisi arrivent du serveur.
    public void Show()
    {
        GameClient.Instance.ClientPacketHandler.SendRequestPledgePowerGradeList();
        RequestPrivileges();
    }

    private void RequestPrivileges()
    {
        if (GameClient.Instance == null || GameClient.Instance.ClientPacketHandler == null)
        {
            return;
        }

        GameClient.Instance.ClientPacketHandler.SendRequestPledgePower(_privilegeRank.index + 1, 1, 0);
        Refresh();
    }

    private void SavePrivileges()
    {
        int mask = 0;
        foreach (Toggle toggle in _toggles)
        {
            if (toggle.value)
            {
                mask |= (int)toggle.userData;
            }
        }

        int rank = _privilegeRank.index + 1;
        GameClient.Instance.ClientPacketHandler.SendRequestPledgePower(rank, 2, mask);
        ClanRanks.SetPrivileges(rank, mask);
    }

    public void Refresh()
    {
        int privilegeRank = _privilegeRank.index + 1;
        _rankCount.text = ClanRanks.MembersPerRank[privilegeRank] + " membre(s)";

        int privileges;
        bool loaded = ClanRanks.TryGetPrivileges(privilegeRank, out privileges);
        bool leader = ClanData.HasClan && PlayerEntity.Instance != null && PlayerEntity.Instance.Identity.Name == ClanData.LeaderName;

        foreach (Toggle toggle in _toggles)
        {
            toggle.SetValueWithoutNotify(loaded && (privileges & (int)toggle.userData) != 0);
            toggle.SetEnabled(leader);
        }

        _saveButton.SetEnabled(leader);
    }
}
