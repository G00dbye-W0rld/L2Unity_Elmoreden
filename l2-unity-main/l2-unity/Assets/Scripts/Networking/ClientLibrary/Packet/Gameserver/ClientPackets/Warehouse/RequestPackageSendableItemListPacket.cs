public class RequestPackageSendableItemListPacket : ClientPacket
{
    public RequestPackageSendableItemListPacket(int targetId) : base((byte)GameClientPacketType.RequestPackageSendableItemList)
    {
        WriteI(targetId);
        BuildPacket();
    }
}
