// Quitter son clan.
public class RequestWithdrawPledgePacket : ClientPacket
{
    public RequestWithdrawPledgePacket() : base((byte)GameClientPacketType.RequestWithdrawPledge)
    {
        BuildPacket();
    }
}
