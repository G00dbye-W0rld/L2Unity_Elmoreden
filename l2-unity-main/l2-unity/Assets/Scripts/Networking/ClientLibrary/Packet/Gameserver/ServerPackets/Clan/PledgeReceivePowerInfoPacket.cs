// Rang et privileges d'un membre. Paquet etendu : sous-code 0x3c.
public class PledgeReceivePowerInfoPacket : ServerPacket
{
    public int PowerGrade { get; private set; }
    public string Name { get; private set; }
    public int Privileges { get; private set; }

    public PledgeReceivePowerInfoPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ReadH();
        PowerGrade = ReadI();
        Name = ReadS();
        Privileges = ReadI();
    }
}
