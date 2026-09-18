// Privileges d'un rang du clan, en masque de bits.
public class ManagePledgePowerPacket : ServerPacket
{
    public int Rank { get; private set; }
    public int Action { get; private set; }
    public int Privileges { get; private set; }

    public ManagePledgePowerPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        Rank = ReadI();
        Action = ReadI();
        Privileges = ReadI();
    }
}
