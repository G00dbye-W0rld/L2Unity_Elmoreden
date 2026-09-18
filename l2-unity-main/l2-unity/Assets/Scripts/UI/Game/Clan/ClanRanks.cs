using System.Collections.Generic;

// Rangs et privileges du clan, appris a la demande. Le serveur ne les pousse
// pas : la fiche d'un membre et les privileges d'un rang se reclament.
public static class ClanRanks
{
    public struct Member
    {
        public string Name;
        public string Title;
        public int PowerGrade;
        public int PledgeType;
        public string UnitName;
        public string SponsorName;
    }

    private static readonly Dictionary<int, int> _privileges = new Dictionary<int, int>();

    public static Member Selected { get; private set; }
    public static int[] MembersPerRank { get; private set; } = new int[10];

    public static event System.Action Changed;

    public static bool TryGetPrivileges(int rank, out int privileges)
    {
        return _privileges.TryGetValue(rank, out privileges);
    }

    public static void SetPrivileges(int rank, int privileges)
    {
        _privileges[rank] = privileges;
        Notify();
    }

    public static void SetMember(Member member)
    {
        Selected = member;
        Notify();
    }

    public static void SetMembersPerRank(int[] counts)
    {
        MembersPerRank = counts;
        Notify();
    }

    public static void Clear()
    {
        _privileges.Clear();
        Selected = new Member();
        MembersPerRank = new int[10];
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
