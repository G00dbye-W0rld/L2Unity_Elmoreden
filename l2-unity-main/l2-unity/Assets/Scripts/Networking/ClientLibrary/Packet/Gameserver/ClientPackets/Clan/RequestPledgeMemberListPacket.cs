// Demande la liste des membres : le serveur repond par une liste complete
// pour le clan puis pour chaque sous-unite.
public class RequestPledgeMemberListPacket : ClientPacket
{
    public RequestPledgeMemberListPacket() : base((byte)GameClientPacketType.RequestPledgeMemberList)
    {
        BuildPacket();
    }
}
