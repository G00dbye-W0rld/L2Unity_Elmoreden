// Lecture commune des objets d'echange (TradeStart, TradeOwnAdd/OtherAdd, TradeUpdate).
public abstract class TradeItemPacket : ServerPacket
{
    protected TradeItemPacket(byte[] d) : base(d)
    {
    }

    protected Product ReadTradeItem()
    {
        Product p = new Product();
        p.Type1 = (ItemType1)ReadH();
        p.ObjectId = ReadI();
        p.ItemId = ReadI();
        p.Count = ReadI();
        p.Type2 = (ItemType2)ReadH();
        ReadH();
        p.BodyPart = (ItemSlot)ReadI();
        p.Enchant = ReadH();
        ReadH();
        ReadH();
        return p;
    }
}
