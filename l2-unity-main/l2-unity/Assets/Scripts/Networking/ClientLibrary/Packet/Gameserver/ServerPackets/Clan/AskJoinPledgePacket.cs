// Invitation recue : qui invite, et dans quel clan.
public class AskJoinPledgePacket : ServerPacket
{
    public int RequestorId { get; private set; }
    public string PledgeName { get; private set; }

    public AskJoinPledgePacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        RequestorId = ReadI();
        PledgeName = ReadS();
    }
}
