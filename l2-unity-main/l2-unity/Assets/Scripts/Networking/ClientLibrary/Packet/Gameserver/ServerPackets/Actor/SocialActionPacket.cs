public class SocialActionPacket : ServerPacket
{
    // Hors de la plage des emotes (2 a 21) : ActionName numerote la timidite 15.
    public const int LEVELUP_ACTION = 100;

    public int ObjectId { get; private set; }
    public int Action { get; private set; }

    public SocialActionPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ObjectId = ReadI();
        Action = ReadB();
    }
}