using System.Collections.Generic;

// Noms de clan et d'alliance, appris du serveur a la demande. Les paquets
// d'apparition ne portent que des identifiants, et une meme reponse sert a
// tous les joueurs du meme clan.
public static class ClanNames
{
    public struct Entry
    {
        public string ClanName;
        public string AllyName;
    }

    private static readonly Dictionary<int, Entry> _known = new Dictionary<int, Entry>();
    private static readonly HashSet<int> _asked = new HashSet<int>();

    /// Prevenu quand un nom arrive : les fenetres ouvertes se redessinent.
    public static event System.Action Changed;

    public static bool TryGet(int clanId, out Entry entry)
    {
        return _known.TryGetValue(clanId, out entry);
    }

    public static void Set(int clanId, string clanName, string allyName)
    {
        _known[clanId] = new Entry { ClanName = clanName, AllyName = allyName };

        if (Changed != null)
        {
            Changed();
        }
    }

    /// Demande le nom une seule fois par clan et par session.
    public static void Request(int clanId)
    {
        if (clanId == 0 || _known.ContainsKey(clanId) || _asked.Contains(clanId))
        {
            return;
        }

        if (GameClient.Instance == null || GameClient.Instance.ClientPacketHandler == null)
        {
            return;
        }

        _asked.Add(clanId);
        GameClient.Instance.ClientPacketHandler.SendRequestPledgeInfo(clanId);
    }

    public static void Clear()
    {
        _known.Clear();
        _asked.Clear();
    }
}
