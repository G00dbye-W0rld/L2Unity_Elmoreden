// Paquet du projet (0xFE:0x5e) : etat du crochetage d'une porte de salle de clan.
public class ExLockpickPacket : ServerPacket
{
    public int DoorObjectId { get; private set; }
    public string HallName { get; private set; }
    public int State { get; private set; }
    public int Pins { get; private set; }
    public int PinsTotal { get; private set; }
    public int Difficulty { get; private set; }
    public int Bonus { get; private set; }
    public int Die { get; private set; }
    public int Result { get; private set; }
    public int Lockpicks { get; private set; }

    public ExLockpickPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ReadH();
        DoorObjectId = ReadI();
        HallName = ReadS();
        State = ReadI();
        Pins = ReadI();
        PinsTotal = ReadI();
        Difficulty = ReadI();
        Bonus = ReadI();
        Die = ReadI();
        Result = ReadI();
        Lockpicks = ReadI();
    }
}
