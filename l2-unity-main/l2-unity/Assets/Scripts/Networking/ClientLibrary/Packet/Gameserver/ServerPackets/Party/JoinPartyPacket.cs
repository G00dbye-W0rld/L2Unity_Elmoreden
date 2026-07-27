public class JoinPartyPacket : ServerPacket
{
    // 1 = l'invitation a ete acceptee, 0 = refusee.
    public bool Accepted { get; private set; }

    public JoinPartyPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        Accepted = ReadI() == 1;
    }
}
