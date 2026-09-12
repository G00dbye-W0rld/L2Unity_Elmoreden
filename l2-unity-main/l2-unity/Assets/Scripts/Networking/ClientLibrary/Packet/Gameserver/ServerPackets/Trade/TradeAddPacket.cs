// Objet ajoute a une offre (TradeOwnAdd ou TradeOtherAdd) ; Count = quantite ajoutee.
public class TradeAddPacket : TradeItemPacket
{
    public Product Item { get; private set; }

    public TradeAddPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ReadH();
        Item = ReadTradeItem();
    }
}
