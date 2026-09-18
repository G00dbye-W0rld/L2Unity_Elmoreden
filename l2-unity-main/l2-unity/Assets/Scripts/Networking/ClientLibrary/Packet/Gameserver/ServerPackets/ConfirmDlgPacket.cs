// Question oui / non du serveur. Meme corps qu'un message systeme, suivi du
// delai de reponse et de l'objet a l'origine de la demande.
public class ConfirmDlgPacket : SystemMessagePacket
{
    public int Time { get; private set; }
    public int RequesterId { get; private set; }

    public ConfirmDlgPacket(byte[] d) : base(d)
    {
    }

    public override void Parse()
    {
        base.Parse();

        if (_packetData.Length - _iterator >= 4) Time = ReadI();
        if (_packetData.Length - _iterator >= 4) RequesterId = ReadI();
    }
}
