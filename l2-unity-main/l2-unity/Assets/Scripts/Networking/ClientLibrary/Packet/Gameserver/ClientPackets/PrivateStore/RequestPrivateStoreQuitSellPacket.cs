public class RequestPrivateStoreQuitSellPacket : ClientPacket
{
    public RequestPrivateStoreQuitSellPacket() : base((byte)GameClientPacketType.RequestPrivateStoreQuitSell)
    {
        BuildPacket();
    }
}
