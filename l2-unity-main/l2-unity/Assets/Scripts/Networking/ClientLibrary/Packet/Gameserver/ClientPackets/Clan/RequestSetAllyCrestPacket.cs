// Pose le blason de l'alliance (chef d'alliance seulement). Longueur nulle : efface.
public class RequestSetAllyCrestPacket : ClientPacket
{
    public RequestSetAllyCrestPacket(byte[] image) : base((byte)GameClientPacketType.RequestSetAllyCrest)
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
