// Demande le blason d'un clan par son identifiant.
public class RequestPledgeCrestPacket : ClientPacket
{
    public RequestPledgeCrestPacket(int crestId) : base((byte)GameClientPacketType.RequestPledgeCrest)
    {
        WriteI(crestId);
        BuildPacket();
    }
}
