public class RequestAnswerJoinPartyPacket : ClientPacket
{
    public RequestAnswerJoinPartyPacket(bool accept) : base((byte)GameClientPacketType.RequestAnswerJoinParty)
    {
        WriteI(accept ? 1 : 0);
        BuildPacket();
    }
}
