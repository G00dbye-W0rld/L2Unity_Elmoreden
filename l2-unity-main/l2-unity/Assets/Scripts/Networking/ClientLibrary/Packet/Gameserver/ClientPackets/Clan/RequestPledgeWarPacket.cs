// Declarer (0x4d), arreter (0x4f) ou capituler (0x51) une guerre contre un clan
// designe par son nom : les trois paquets n'ont que ce nom pour contenu.
public class RequestPledgeWarPacket : ClientPacket
{
    public RequestPledgeWarPacket(GameClientPacketType type, string clanName) : base((byte)type)
    {
        WriteS(clanName);
        BuildPacket();
    }
}
