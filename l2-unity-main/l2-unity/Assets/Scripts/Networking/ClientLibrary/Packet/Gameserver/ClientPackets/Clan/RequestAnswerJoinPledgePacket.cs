// Reponse a une invitation de clan.
public class RequestAnswerJoinPledgePacket : ClientPacket
{
    public RequestAnswerJoinPledgePacket(bool accept) : base((byte)GameClientPacketType.RequestAnswerJoinPledge)
    {
        WriteI(accept ? 1 : 0);
        BuildPacket();
    }
}
