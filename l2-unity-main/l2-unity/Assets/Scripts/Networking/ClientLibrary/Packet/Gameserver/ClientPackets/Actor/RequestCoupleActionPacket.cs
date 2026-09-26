// Emote a deux : le serveur demande son accord a la cible avant de la jouer.
public class RequestCoupleActionPacket : ClientPacket
{
    public RequestCoupleActionPacket(int targetId, int actionId) : base((byte)GameClientPacketType.RequestCoupleAction)
    {
        WriteI(targetId);
        WriteI(actionId);
        BuildPacket();
    }
}
