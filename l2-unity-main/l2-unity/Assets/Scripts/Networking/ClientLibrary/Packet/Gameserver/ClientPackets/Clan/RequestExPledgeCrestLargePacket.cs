// Demande le grand blason d'un clan par son identifiant.
public class RequestExPledgeCrestLargePacket : ClientPacket
{
    public RequestExPledgeCrestLargePacket(int crestId) : base((byte)GameClientPacketType.DoubleOPCode)
    {
        WriteB((byte)GameClientPacketDoubleType.RequestExPledgeCrestLarge);
        WriteB(0);
        WriteI(crestId);
        BuildPacket();
    }
}
