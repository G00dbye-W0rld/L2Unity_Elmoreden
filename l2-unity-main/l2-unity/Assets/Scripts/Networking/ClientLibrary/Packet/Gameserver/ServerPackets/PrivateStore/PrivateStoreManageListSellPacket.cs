using System.Collections.Generic;

public class PrivateStoreManageListSellPacket : ServerPacket
{
    public int ObjectId { get; private set; }
    public bool PackageSale { get; private set; }
    public int Adena { get; private set; }
    public List<Product> Inventory { get; private set; }
    public List<Product> Store { get; private set; }

    public PrivateStoreManageListSellPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ObjectId = ReadI();
        PackageSale = ReadI() == 1;
        Adena = ReadI();

        Inventory = ReadProducts(false);
        Store = ReadProducts(true);
    }

    // Pour l'inventaire, le prix envoye EST le prix de reference ; la liste
    // du magasin porte le prix fixe puis le prix de reference.
    private List<Product> ReadProducts(bool listed)
    {
        int count = ReadI();
        List<Product> products = new List<Product>(count);

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
            p.ReferencePrice = listed ? ReadI() : p.Price;
            products.Add(p);
        }

        return products;
    }
}
