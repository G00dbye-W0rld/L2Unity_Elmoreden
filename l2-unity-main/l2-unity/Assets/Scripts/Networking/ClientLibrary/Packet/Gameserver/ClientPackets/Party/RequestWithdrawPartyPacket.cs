public class RequestWithdrawPartyPacket : ClientPacket
{
    public RequestWithdrawPartyPacket() : base((byte)GameClientPacketType.RequestWithdrawParty)
    {
        BuildPacket();
    }
}
