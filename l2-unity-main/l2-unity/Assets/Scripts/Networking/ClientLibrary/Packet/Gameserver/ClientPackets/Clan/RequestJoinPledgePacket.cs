// Invitation d'un joueur cible dans le clan, dans l'unite donnee (0 = clan).
public class RequestJoinPledgePacket : ClientPacket
{
    public RequestJoinPledgePacket(int targetId, int pledgeType) : base((byte)GameClientPacketType.RequestJoinPledge)
    {
        WriteI(targetId);
        WriteI(pledgeType);
        BuildPacket();
    }
}
