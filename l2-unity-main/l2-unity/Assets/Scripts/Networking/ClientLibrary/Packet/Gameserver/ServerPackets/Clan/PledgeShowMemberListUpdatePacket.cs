// Un membre change d'etat : connexion, niveau, classe, sous-unite.
public class PledgeShowMemberListUpdatePacket : ServerPacket
{
    public ClanMemberInfo Member { get; private set; }

    public PledgeShowMemberListUpdatePacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        Member = new ClanMemberInfo
        {
            Name = ReadS(),
            Level = ReadI(),
            ClassId = ReadI(),
            Sex = ReadI(),
            Race = ReadI(),
            ObjectId = ReadI(),
            PledgeType = ReadI()
        };
        Member.HasSponsor = ReadI() != 0;
    }
}
