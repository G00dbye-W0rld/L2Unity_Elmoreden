public class SetPrivateStoreMsgSellPacket : ClientPacket
{
    public SetPrivateStoreMsgSellPacket(string title) : base((byte)GameClientPacketType.SetPrivateStoreMsgSell)
    {
        WriteS(title ?? "");
        BuildPacket();
    }
}
