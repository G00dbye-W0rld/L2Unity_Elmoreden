// Un membre a quitte le clan ou en a ete exclu.
public class PledgeShowMemberListDeletePacket : ServerPacket
{
    public string Name { get; private set; }

    public PledgeShowMemberListDeletePacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        Name = ReadS();
    }
}
