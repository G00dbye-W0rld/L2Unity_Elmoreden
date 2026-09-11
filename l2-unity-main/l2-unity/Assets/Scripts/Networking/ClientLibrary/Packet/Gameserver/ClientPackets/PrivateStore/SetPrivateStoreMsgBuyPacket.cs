public class SetPrivateStoreMsgBuyPacket : ClientPacket
{
    public SetPrivateStoreMsgBuyPacket(string title) : base((byte)GameClientPacketType.SetPrivateStoreMsgBuy)
    {
        WriteS(title ?? "");
        BuildPacket();
    }
}
