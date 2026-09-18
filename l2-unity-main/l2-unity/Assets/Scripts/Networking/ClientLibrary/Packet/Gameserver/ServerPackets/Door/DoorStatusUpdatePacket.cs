// Ouverture ou fermeture d'une porte deja connue.
public class DoorStatusUpdatePacket : ServerPacket
{
    public int ObjectId { get; private set; }
    public bool Opened { get; private set; }
    public int DoorId { get; private set; }

    public DoorStatusUpdatePacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ObjectId = ReadI();
        Opened = ReadI() == 0;
        ReadI(); // degats
        ReadI(); // affichage des points de vie
        DoorId = ReadI();
    }
}
