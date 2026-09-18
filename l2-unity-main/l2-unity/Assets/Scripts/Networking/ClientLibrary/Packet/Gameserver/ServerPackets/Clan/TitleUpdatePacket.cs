// Nouveau titre d'un joueur, diffuse aux joueurs proches.
public class TitleUpdatePacket : ServerPacket
{
    public int ObjectId { get; private set; }
    public string Title { get; private set; }

    public TitleUpdatePacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ObjectId = ReadI();
        Title = ReadS();
    }
}
