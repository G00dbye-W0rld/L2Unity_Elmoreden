// Nom de magasin diffuse aux joueurs proches (vente, achat et atelier ont le meme format).
public class PrivateStoreMsgPacket : ServerPacket
{
    public int ObjectId { get; private set; }
    public string Message { get; private set; }

    public PrivateStoreMsgPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ObjectId = ReadI();
        Message = ReadS();
    }
}
