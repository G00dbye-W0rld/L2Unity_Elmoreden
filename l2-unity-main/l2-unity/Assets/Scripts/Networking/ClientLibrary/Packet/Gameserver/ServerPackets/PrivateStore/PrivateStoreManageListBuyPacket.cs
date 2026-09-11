using System.Collections.Generic;

// Preparation du magasin d'achat : objets possedes (un par modele), puis liste d'achat actuelle.
public class PrivateStoreManageListBuyPacket : ServerPacket
{
    public int ObjectId { get; private set; }
    public int Adena { get; private set; }
    public List<Product> Inventory { get; private set; }
    public List<Product> Store { get; private set; }

    public PrivateStoreManageListBuyPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ObjectId = ReadI();
        Adena = ReadI();

        int count = ReadI();
        Inventory = new List<Product>(count);
        for (int i = 0; i < count; i++)
        {
            Product p = ReadCommon();
            p.Price = p.ReferencePrice;
            Inventory.Add(p);
        }

        count = ReadI();
        Store = new List<Product>(count);
        for (int i = 0; i < count; i++)
        {
            Product p = ReadCommon();
            p.Price = ReadI();
            p.ReferencePrice = ReadI();
            Store.Add(p);
        }
    }

    private Product ReadCommon()
    {
        Product p = new Product();
        p.ItemId = ReadI();
        p.Enchant = ReadH();
        p.Count = ReadI();
        p.ReferencePrice = ReadI();
        ReadH();
        p.BodyPart = (ItemSlot)ReadI();
        p.Type2 = (ItemType2)ReadH();
        return p;
    }
}
