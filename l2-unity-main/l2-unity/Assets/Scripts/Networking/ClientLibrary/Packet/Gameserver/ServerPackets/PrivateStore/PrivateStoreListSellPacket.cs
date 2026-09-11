using System.Collections.Generic;

// Vitrine d'un marchand, envoyee quand on interagit avec lui.
public class PrivateStoreListSellPacket : ServerPacket
{
    public int StoreObjectId { get; private set; }
    public bool Packaged { get; private set; }
    public int Adena { get; private set; }
    public List<Product> Products { get; private set; }

    public PrivateStoreListSellPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        StoreObjectId = ReadI();
        Packaged = ReadI() == 1;
        Adena = ReadI();

        int count = ReadI();
        Products = new List<Product>(count);

        for (int i = 0; i < count; i++)
        {
            Product p = new Product();
            p.Type2 = (ItemType2)ReadI();
            p.ObjectId = ReadI();
            p.ItemId = ReadI();
            p.Count = ReadI();
            ReadH();
            p.Enchant = ReadH();
            ReadH();
            p.BodyPart = (ItemSlot)ReadI();
            p.Price = ReadI();
            p.ReferencePrice = ReadI();
            Products.Add(p);
        }
    }
}
