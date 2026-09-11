public class RequestPrivateStoreQuitBuyPacket : ClientPacket
{
    public RequestPrivateStoreQuitBuyPacket() : base((byte)GameClientPacketType.RequestPrivateStoreQuitBuy)
    {
        BuildPacket();
    }
}
