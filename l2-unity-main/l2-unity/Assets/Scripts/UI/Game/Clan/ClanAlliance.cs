using System.Collections.Generic;

// Alliance du clan, telle que le serveur la decrit a la demande.
public static class ClanAlliance
{
    public static bool Known { get; private set; }
    public static string Name { get; private set; } = string.Empty;
    public static string LeaderClan { get; private set; } = string.Empty;
    public static string LeaderName { get; private set; } = string.Empty;
    public static int Total { get; private set; }
    public static int Online { get; private set; }
    public static List<AllianceInfoPacket.Member> Clans { get; private set; } = new List<AllianceInfoPacket.Member>();

    public static event System.Action Changed;

    public static void Set(AllianceInfoPacket packet)
    {
        Known = true;
        Name = packet.Name;
        LeaderClan = packet.LeaderClan;
        LeaderName = packet.LeaderName;
        Total = packet.Total;
        Online = packet.Online;
        Clans = packet.Clans;
        Notify();
    }

    public static void Clear()
    {
        Known = false;
        Name = string.Empty;
        LeaderClan = string.Empty;
        LeaderName = string.Empty;
        Total = 0;
        Online = 0;
        Clans = new List<AllianceInfoPacket.Member>();
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
