using System.Collections.Generic;

// Etat du clan du joueur, tenu a jour par les paquets du serveur. La fenetre
// de clan se contente de lire ce magasin.
public static class ClanData
{
    private static readonly List<ClanMemberInfo> _members = new List<ClanMemberInfo>();

    private static readonly SortedDictionary<int, ClanUnit> _units = new SortedDictionary<int, ClanUnit>();
    private static readonly SortedDictionary<int, int> _skills = new SortedDictionary<int, int>();

    public static IReadOnlyList<ClanMemberInfo> Members { get { return _members; } }

    /// Sous-unites connues (hors clan principal), meme sans membre.
    public static IEnumerable<KeyValuePair<int, ClanUnit>> Units { get { return _units; } }

    /// Competences du clan : id vers niveau.
    public static IEnumerable<KeyValuePair<int, int>> Skills { get { return _skills; } }

    public static bool HasClan { get { return ClanId != 0; } }
    public static int ClanId { get; private set; }
    public static string Name { get; private set; } = string.Empty;
    public static string LeaderName { get; private set; } = string.Empty;
    public static int CrestId { get; private set; }
    public static int Level { get; private set; }
    public static int CastleId { get; private set; }
    public static int ClanHallId { get; private set; }
    public static int Rank { get; private set; }
    public static int Reputation { get; private set; }
    public static int AllyId { get; private set; }
    public static string AllyName { get; private set; } = string.Empty;
    public static int AllyCrestId { get; private set; }
    public static bool AtWar { get; private set; }
    /// Non nul quand le clan est en cours de dissolution.
    public static int Dissolution { get; private set; }

    /// Prevenue a chaque changement : la fenetre s'y abonne pour se redessiner.
    public static event System.Action Changed;

    public static void SetFromList(PledgeShowMemberListAllPacket packet)
    {
        // Les sous-unites arrivent dans leur propre paquet : on remplace les
        // membres de cette unite sans toucher aux autres.
        _members.RemoveAll(m => m.PledgeType == packet.PledgeType);
        _members.AddRange(packet.Members);

        if (packet.PledgeType != 0)
        {
            _units[packet.PledgeType] = new ClanUnit(packet.PledgeName, packet.LeaderName);
        }

        if (packet.PledgeType == 0)
        {
            ClanId = packet.ClanId;
            Name = packet.PledgeName;
            LeaderName = packet.LeaderName;
            CrestId = packet.CrestId;
            Level = packet.Level;
            CastleId = packet.CastleId;
            ClanHallId = packet.ClanHallId;
            Rank = packet.Rank;
            Reputation = packet.Reputation;
            AllyId = packet.AllyId;
            AllyName = packet.AllyName;
            AllyCrestId = packet.AllyCrestId;
            AtWar = packet.AtWar;
            Dissolution = packet.Dissolution;
        }

        Notify();
    }

    public static void SetInfo(PledgeShowInfoUpdatePacket packet)
    {
        ClanId = packet.ClanId;
        CrestId = packet.CrestId;
        Level = packet.Level;
        CastleId = packet.CastleId;
        ClanHallId = packet.ClanHallId;
        Rank = packet.Rank;
        Reputation = packet.Reputation;
        AllyId = packet.AllyId;
        AllyName = packet.AllyName;
        AllyCrestId = packet.AllyCrestId;
        AtWar = packet.AtWar;
        Dissolution = packet.Dissolution;

        Notify();
    }

    /// Entree dans un clan : la liste complete arrive juste apres.
    public static void SetJoined(int clanId)
    {
        ClanId = clanId;
        Notify();
    }

    public static void AddMember(ClanMemberInfo member)
    {
        RemoveMember(member.Name, false);
        _members.Add(member);
        Notify();
    }

    public static void UpdateMember(ClanMemberInfo member)
    {
        for (int i = 0; i < _members.Count; i++)
        {
            if (_members[i].Name == member.Name)
            {
                _members[i] = member;
                Notify();
                return;
            }
        }

        AddMember(member);
    }

    public static void SetUnit(int pledgeType, string name, string leaderName)
    {
        _units[pledgeType] = new ClanUnit(name, leaderName);
        Notify();
    }

    public static bool TryGetUnit(int pledgeType, out ClanUnit unit)
    {
        return _units.TryGetValue(pledgeType, out unit);
    }

    public static void SetSkills(IEnumerable<KeyValuePair<int, int>> skills, bool addition)
    {
        if (!addition)
        {
            _skills.Clear();
        }

        foreach (KeyValuePair<int, int> skill in skills)
        {
            _skills[skill.Key] = skill.Value;
        }

        Notify();
    }

    public static void RemoveMember(string name, bool notify = true)
    {
        int removed = _members.RemoveAll(m => m.Name == name);
        if (removed > 0 && notify)
        {
            Notify();
        }
    }

    /// "Vider la liste" sert aussi bien au depart du clan qu'a un simple
    /// rafraichissement (nouvelle unite, membre deplace) : dans ce cas les
    /// listes qui suivent recreent membres et unites, mais pas les competences.
    public static void OnListCleared()
    {
        bool stillInClan = PlayerEntity.Instance != null && PlayerEntity.Instance.Identity.ClanId != 0;
        if (!stillInClan)
        {
            Clear();
            return;
        }

        _members.Clear();
        _units.Clear();
        Notify();
    }

    public static void Clear()
    {
        _members.Clear();
        _units.Clear();
        _skills.Clear();
        ClanId = 0;
        Name = string.Empty;
        LeaderName = string.Empty;
        CrestId = 0;
        Level = 0;
        CastleId = 0;
        ClanHallId = 0;
        Rank = 0;
        Reputation = 0;
        AllyId = 0;
        AllyName = string.Empty;
        AllyCrestId = 0;
        AtWar = false;
        Dissolution = 0;

        Notify();
    }

    private static void Notify()
    {
        if (Changed != null)
        {
            Changed();
        }
    }
}

public struct ClanUnit
{
    public string Name;
    public string LeaderName;

    public ClanUnit(string name, string leaderName)
    {
        Name = name;
        LeaderName = leaderName;
    }
}
