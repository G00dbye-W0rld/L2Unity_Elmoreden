// Demande le nom d'un clan a partir de son identifiant.
public class RequestPledgeInfoPacket : ClientPacket
{
    public RequestPledgeInfoPacket(int clanId) : base((byte)GameClientPacketType.RequestPledgeInfo)
    {
        WriteI(clanId);
        BuildPacket();
    }
}
