using System.Collections.Generic;
using UnityEngine;

// Points a afficher sur la minimap et la carte : membres du groupe et
// marqueurs de quete. Le serveur envoie deja les deux, ce magasin ne fait que
// les garder a disposition de l'interface.
public static class MapMarkers
{
    private static readonly Dictionary<int, Vector3> _party = new Dictionary<int, Vector3>();
    private static readonly List<Vector3> _quest = new List<Vector3>();

    public static IReadOnlyDictionary<int, Vector3> Party { get { return _party; } }
    public static IReadOnlyList<Vector3> Quest { get { return _quest; } }

    /// Positions des membres du groupe, rediffusees toutes les 12 s.
    public static void SetPartyPositions(Dictionary<int, Vector3> positions)
    {
        _party.Clear();
        if (positions == null)
        {
            return;
        }

        foreach (KeyValuePair<int, Vector3> entry in positions)
        {
            _party[entry.Key] = entry.Value;
        }
    }

    public static void ClearParty()
    {
        _party.Clear();
    }

    /// Marqueur de quete. Deux marqueurs a moins d'une unite l'un de l'autre
    /// sont consideres identiques : le serveur renvoie parfois les memes.
    public static void AddQuestMarker(Vector3 position)
    {
        if (FindQuestMarker(position) < 0)
        {
            _quest.Add(position);
        }
    }

    public static void RemoveQuestMarker(Vector3 position)
    {
        int index = FindQuestMarker(position);
        if (index >= 0)
        {
            _quest.RemoveAt(index);
        }
    }

    /// Marqueur personnel pose par le joueur sur la carte du monde. Visible
    /// aussi sur le radar, rabattu sur son bord quand il est hors de portee.
    public static bool HasPersonal { get; private set; }
    public static Vector3 Personal { get; private set; }

    public static void SetPersonal(Vector3 position)
    {
        Personal = position;
        HasPersonal = true;
    }

    public static void ClearPersonal()
    {
        HasPersonal = false;
    }

    /// Marqueur pose par un membre du groupe, recu du serveur.
    public static bool HasShared { get; private set; }
    public static Vector3 Shared { get; private set; }

    public static void SetShared(Vector3 position)
    {
        Shared = position;
        HasShared = true;
    }

    public static void ClearShared()
    {
        HasShared = false;
    }

    public static void ClearQuestMarkers()
    {
        _quest.Clear();
    }

    private static int FindQuestMarker(Vector3 position)
    {
        for (int i = 0; i < _quest.Count; i++)
        {
            if ((_quest[i] - position).sqrMagnitude < 1f)
            {
                return i;
            }
        }

        return -1;
    }
}
