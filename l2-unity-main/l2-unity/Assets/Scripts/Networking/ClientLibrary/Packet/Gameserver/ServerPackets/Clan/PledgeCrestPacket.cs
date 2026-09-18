// Blason d'un clan, en DDS compresse. La longueur vaut zero quand le clan
// n'en a pas.
public class PledgeCrestPacket : ServerPacket
{
    public int CrestId { get; private set; }
    public byte[] Data { get; private set; }

    public PledgeCrestPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        CrestId = ReadI();

        int length = ReadI();
        Data = length > 0 ? ReadB(length) : null;
    }
}
