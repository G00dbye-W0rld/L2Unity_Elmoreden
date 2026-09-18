using System.Collections.Generic;
using UnityEngine;

// Blasons de clan, d'alliance et grands blasons, demandes au serveur puis gardes
// en memoire. Le serveur ne les envoie jamais spontanement : il faut les reclamer
// par leur identifiant, chaque type par son propre paquet.
public static class ClanCrests
{
    public enum Kind { Pledge, Ally, Large }

    private sealed class Store
    {
        public readonly Dictionary<int, Texture2D> Crests = new Dictionary<int, Texture2D>();
        public readonly HashSet<int> Asked = new HashSet<int>();
    }

    private static readonly Store[] _stores = { new Store(), new Store(), new Store() };

    /// Tailles envoyees. Le jeu d'origine imposait 16 x 12, 8 x 12 et 256 x 128 ;
    /// le serveur ne verifie plus que le poids et un cote maximal.
    private const int CrestSize = 32;
    private const int LargeWidth = 256;
    private const int LargeHeight = 128;
    private const int MaxBytes = 8192;
    private const int MaxLargeBytes = 60000;

    public static event System.Action Changed;

    public static Texture2D Get(int crestId) { return Get(Kind.Pledge, crestId); }
    public static Texture2D GetAlly(int crestId) { return Get(Kind.Ally, crestId); }
    public static Texture2D GetLarge(int crestId) { return Get(Kind.Large, crestId); }

    public static void Set(int crestId, byte[] data) { Set(Kind.Pledge, crestId, data); }
    public static void SetAlly(int crestId, byte[] data) { Set(Kind.Ally, crestId, data); }
    public static void SetLarge(int crestId, byte[] data) { Set(Kind.Large, crestId, data); }

    public static void Request(int crestId) { Request(Kind.Pledge, crestId); }
    public static void RequestAlly(int crestId) { Request(Kind.Ally, crestId); }
    public static void RequestLarge(int crestId) { Request(Kind.Large, crestId); }

    private static Texture2D Get(Kind kind, int crestId)
    {
        Texture2D crest;
        return _stores[(int)kind].Crests.TryGetValue(crestId, out crest) ? crest : null;
    }

    private static void Set(Kind kind, int crestId, byte[] data)
    {
        Texture2D crest = CrestImage.Decode(data);
        if (crest == null)
        {
            return;
        }

        _stores[(int)kind].Crests[crestId] = crest;

        if (Changed != null)
        {
            Changed();
        }
    }

    /// Demande le blason une seule fois par identifiant et par session.
    private static void Request(Kind kind, int crestId)
    {
        Store store = _stores[(int)kind];
        if (crestId == 0 || store.Crests.ContainsKey(crestId) || store.Asked.Contains(crestId))
        {
            return;
        }

        if (GameClient.Instance == null || GameClient.Instance.ClientPacketHandler == null)
        {
            return;
        }

        store.Asked.Add(crestId);
        GameClientPacketHandler client = GameClient.Instance.ClientPacketHandler;

        switch (kind)
        {
            case Kind.Ally:
                client.SendRequestAllyCrest(crestId);
                break;
            case Kind.Large:
                client.SendRequestPledgeCrestLarge(crestId);
                break;
            default:
                client.SendRequestPledgeCrest(crestId);
                break;
        }
    }

    /// Envoie une image locale comme blason du clan, redimensionnee et reencodee en PNG.
    public static bool Upload(string path, out string error)
    {
        byte[] png = Prepare(path, CrestSize, CrestSize, MaxBytes, out error);
        if (png == null)
        {
            return false;
        }

        GameClient.Instance.ClientPacketHandler.SendSetPledgeCrest(png);

        // Le serveur ne renvoie que des UserInfo : on redemande la liste, qui
        // porte le nouvel identifiant de blason.
        GameClient.Instance.ClientPacketHandler.SendRequestPledgeMemberList();
        return true;
    }

    public static bool UploadAlly(string path, out string error)
    {
        byte[] png = Prepare(path, CrestSize, CrestSize, MaxBytes, out error);
        if (png == null)
        {
            return false;
        }

        GameClient.Instance.ClientPacketHandler.SendSetAllyCrest(png);
        return true;
    }

    public static bool UploadLarge(string path, out string error)
    {
        byte[] png = Prepare(path, LargeWidth, LargeHeight, MaxLargeBytes, out error);
        if (png == null)
        {
            return false;
        }

        GameClient.Instance.ClientPacketHandler.SendSetPledgeCrestLarge(png);
        return true;
    }

    private static byte[] Prepare(string path, int width, int height, int maxBytes, out string error)
    {
        error = string.Empty;

        if (!System.IO.File.Exists(path))
        {
            error = "Fichier introuvable : " + path;
            return null;
        }

        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(System.IO.File.ReadAllBytes(path)))
        {
            error = "Image illisible : " + path;
            return null;
        }

        byte[] png = Resize(source, width, height).EncodeToPNG();
        if (png.Length > maxBytes)
        {
            error = "Blason trop lourd (" + png.Length + " octets).";
            return null;
        }

        return png;
    }

    private static Texture2D Resize(Texture2D source, int width, int height)
    {
        Texture2D scaled = new Texture2D(width, height, TextureFormat.RGBA32, false);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                scaled.SetPixel(x, y, source.GetPixelBilinear((x + 0.5f) / width, (y + 0.5f) / height));
            }
        }

        scaled.Apply();
        return scaled;
    }

    public static void Clear()
    {
        foreach (Store store in _stores)
        {
            store.Crests.Clear();
            store.Asked.Clear();
        }
    }
}
