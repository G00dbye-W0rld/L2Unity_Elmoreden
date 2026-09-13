// Marqueur pose sur la carte et partage avec le groupe : le serveur le
// rediffuse aux membres sous forme de point de radar.
public class RequestPartyMarkerPacket : ClientPacket
{
    public RequestPartyMarkerPacket(int x, int y, int z) : base((byte)GameClientPacketType.DoubleOPCode)
    {
        WriteB((byte)GameClientPacketDoubleType.RequestPartyMarker);
        WriteB(0);
        WriteI(x);
        WriteI(y);
        WriteI(z);
        BuildPacket();
    }
}
