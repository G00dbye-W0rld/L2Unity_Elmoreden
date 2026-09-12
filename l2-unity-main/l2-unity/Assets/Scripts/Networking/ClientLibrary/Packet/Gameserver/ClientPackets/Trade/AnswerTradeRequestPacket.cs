public class AnswerTradeRequestPacket : ClientPacket
{
    public AnswerTradeRequestPacket(bool accept) : base((byte)GameClientPacketType.AnswerTradeRequest)
    {
        WriteI(accept ? 1 : 0);
        BuildPacket();
    }
}
