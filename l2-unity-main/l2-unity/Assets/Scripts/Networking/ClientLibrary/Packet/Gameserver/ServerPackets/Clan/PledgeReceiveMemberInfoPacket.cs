// Fiche d'un membre : titre, rang, unite, parrain. Paquet etendu : 0x3d.
public class PledgeReceiveMemberInfoPacket : ServerPacket
{
    public int PledgeType { get; private set; }
    public string Name { get; private set; }
    public string Title { get; private set; }
    public int PowerGrade { get; private set; }
    public string UnitName { get; private set; }
    public string SponsorName { get; private set; }

    public PledgeReceiveMemberInfoPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ReadH();
        PledgeType = ReadI();
        Name = ReadS();
        Title = ReadS();
        PowerGrade = ReadI();
        UnitName = ReadS();
        SponsorName = ReadS();
    }
}
