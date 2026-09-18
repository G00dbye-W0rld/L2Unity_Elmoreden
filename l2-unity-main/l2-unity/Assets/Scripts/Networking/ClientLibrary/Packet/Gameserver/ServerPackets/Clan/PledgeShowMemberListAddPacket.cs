// Un membre vient d'entrer dans le clan.
public class PledgeShowMemberListAddPacket : ServerPacket
{
    public ClanMemberInfo Member { get; private set; }

    public PledgeShowMemberListAddPacket(byte[] d) : base(d)
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
    }
}
