public class SendTradeRequestPacket : ServerPacket
{
    public int SenderId { get; private set; }

    public SendTradeRequestPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        SenderId = ReadI();
    }
}
