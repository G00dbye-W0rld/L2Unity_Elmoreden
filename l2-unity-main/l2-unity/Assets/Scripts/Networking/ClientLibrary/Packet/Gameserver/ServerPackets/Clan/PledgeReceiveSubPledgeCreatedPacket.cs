// Sous-unite creee chez le maitre de village. Paquet etendu : sous-code 0x3f.
public class PledgeReceiveSubPledgeCreatedPacket : ServerPacket
{
    public int PledgeType { get; private set; }
    public string Name { get; private set; }
    public string LeaderName { get; private set; }

    public PledgeReceiveSubPledgeCreatedPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ReadH();
        ReadI();
        PledgeType = ReadI();
        Name = ReadS();
        LeaderName = ReadS();
    }
}
