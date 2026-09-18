using System.Collections.Generic;

// Objets de l'inventaire qui peuvent partir en colis vers TargetId.
public class PackageSendableListPacket : ServerPacket
{
    public int TargetId { get; private set; }
    public int Adena { get; private set; }
    public List<Product> Products { get; private set; }

    public PackageSendableListPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        TargetId = ReadI();
        Adena = ReadI();

        int count = ReadI();
        Products = new List<Product>(count);

        for (int i = 0; i < count; i++)
        {
            Product p = new Product();
            p.Type1 = (ItemType1)ReadH();
            p.ObjectId = ReadI();
            p.ItemId = ReadI();
            p.Count = ReadI();
            p.Type2 = (ItemType2)ReadH();
            ReadH(); // custom type 1
            p.BodyPart = (ItemSlot)ReadI();
            p.Enchant = ReadH();
            ReadH(); // custom type 2
            ReadH();
            ReadI(); // objectId repete
            Products.Add(p);
        }
    }
}
