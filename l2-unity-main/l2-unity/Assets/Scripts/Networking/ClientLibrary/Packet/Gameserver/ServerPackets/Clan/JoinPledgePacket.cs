// Entree effective dans un clan.
public class JoinPledgePacket : ServerPacket
{
    public int PledgeId { get; private set; }

    public JoinPledgePacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        PledgeId = ReadI();
    }
}
