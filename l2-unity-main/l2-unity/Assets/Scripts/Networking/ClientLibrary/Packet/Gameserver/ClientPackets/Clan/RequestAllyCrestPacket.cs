// Demande le blason d'une alliance par son identifiant.
public class RequestAllyCrestPacket : ClientPacket
{
    public RequestAllyCrestPacket(int crestId) : base((byte)GameClientPacketType.RequestAllyCrest)
    {
        WriteI(crestId);
        BuildPacket();
    }
}
