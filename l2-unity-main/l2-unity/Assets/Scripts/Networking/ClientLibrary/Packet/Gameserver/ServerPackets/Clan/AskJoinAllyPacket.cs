// Invitation recue dans une alliance.
public class AskJoinAllyPacket : ServerPacket
{
    public int RequestorId { get; private set; }
    public string RequestorName { get; private set; }

    public AskJoinAllyPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        RequestorId = ReadI();
        RequestorName = ReadS();
    }
}
