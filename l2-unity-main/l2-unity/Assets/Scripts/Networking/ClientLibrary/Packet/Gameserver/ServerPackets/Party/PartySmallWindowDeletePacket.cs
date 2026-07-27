// Un membre quitte le groupe (depart volontaire, exclusion ou deconnexion).
public class PartySmallWindowDeletePacket : ServerPacket
{
    public int ObjectId { get; private set; }
    public string Name { get; private set; }

    public PartySmallWindowDeletePacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ObjectId = ReadI();
        Name = ReadS();
    }
}
