// Pose le grand blason du clan (256 x 128 a l'origine). Longueur nulle : efface.
public class RequestExSetPledgeCrestLargePacket : ClientPacket
{
    public RequestExSetPledgeCrestLargePacket(byte[] image) : base((byte)GameClientPacketType.DoubleOPCode)
    {
        int length = image != null ? image.Length : 0;

        WriteB((byte)GameClientPacketDoubleType.RequestExSetPledgeCrestLarge);
        WriteB(0);
        WriteI(length);
        if (length > 0)
        {
            WriteB(image);
        }

        BuildPacket();
    }
}
