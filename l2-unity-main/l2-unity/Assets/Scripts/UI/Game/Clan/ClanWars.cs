using System.Collections.Generic;

// Guerres du clan : celles qu'il a declarees, et les clans qui lui ont declare
// la guerre. Redemandees au serveur a l'ouverture et apres chaque changement.
public static class ClanWars
{
    private static readonly List<string> _declared = new List<string>();
    private static readonly List<string> _attackers = new List<string>();

    public static IReadOnlyList<string> Declared { get { return _declared; } }
    public static IReadOnlyList<string> Attackers { get { return _attackers; } }

    public static event System.Action Changed;

    public static void Set(int tab, List<string> clans)
    {
        List<string> target = tab == 0 ? _declared : _attackers;
        target.Clear();
        target.AddRange(clans);

        if (Changed != null)
        {
            Changed();
        }
    }

    public static void Request()
    {
        if (GameClient.Instance == null || GameClient.Instance.ClientPacketHandler == null)
        {
            return;
        }

        GameClient.Instance.ClientPacketHandler.SendRequestPledgeWarList(0, 0);
        GameClient.Instance.ClientPacketHandler.SendRequestPledgeWarList(0, 1);
    }

    public static void Clear()
    {
        _declared.Clear();
        _attackers.Clear();

        if (Changed != null)
        {
            Changed();
        }
    }
}
