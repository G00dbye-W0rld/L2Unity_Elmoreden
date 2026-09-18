// Pose un blason sur le clan. Le serveur stocke les octets tels quels : une
// longueur nulle efface le blason.
public class RequestSetPledgeCrestPacket : ClientPacket
{
    public RequestSetPledgeCrestPacket(byte[] image) : base((byte)GameClientPacketType.RequestSetPledgeCrest)
    {
        int length = image != null ? image.Length : 0;

        WriteI(length);
        if (length > 0)
        {
            WriteB(image);
        }

        BuildPacket();
    }
}
