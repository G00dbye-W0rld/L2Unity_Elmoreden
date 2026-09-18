using System.Collections.Generic;

// Fiches publiques des clans, demandees a l'ouverture de la fenetre d'infos.
// Toujours redemandees : niveau, effectif et guerres changent en cours de jeu.
public static class ClanCards
{
    private static readonly Dictionary<int, ClanCard> _cards = new Dictionary<int, ClanCard>();

    public static event System.Action<ClanCard> Changed;

    public static bool TryGet(int clanId, out ClanCard card)
    {
        return _cards.TryGetValue(clanId, out card);
    }

    public static void Request(int clanId)
    {
        if (clanId != 0 && GameClient.Instance != null && GameClient.Instance.ClientPacketHandler != null)
        {
            GameClient.Instance.ClientPacketHandler.SendRequestClanCard(clanId);
        }
    }

    public static void Set(ClanCard card)
    {
        _cards[card.ClanId] = card;

        if (Changed != null)
        {
            Changed(card);
        }
    }
}
