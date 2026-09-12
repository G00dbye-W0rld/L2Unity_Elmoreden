public class TradeRequestPacket : ClientPacket
{
    public TradeRequestPacket(int targetId) : base((byte)GameClientPacketType.TradeRequest)
    {
        WriteI(targetId);
        BuildPacket();
    }
}
