// Grand blason d'un clan. Paquet etendu : sous-code 0x28.
public class ExPledgeCrestLargePacket : ServerPacket
{
    public int CrestId { get; private set; }
    public byte[] Data { get; private set; }

    public ExPledgeCrestLargePacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ReadH();
        ReadI();
        CrestId = ReadI();

        int length = ReadI();
        Data = length > 0 && _iterator + length <= _packetData.Length ? ReadB(length) : null;
    }
}
