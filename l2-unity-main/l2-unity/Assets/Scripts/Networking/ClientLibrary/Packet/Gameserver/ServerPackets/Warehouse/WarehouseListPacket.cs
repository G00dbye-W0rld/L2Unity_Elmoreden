using System.Collections.Generic;

// Liste de depot (0x41, inventaire deposable) ou de retrait (0x42, contenu de
// l'entrepot) : les deux paquets ont la meme structure.
public class WarehouseListPacket : ServerPacket
{
    public WarehouseType Type { get; private set; }
    public int Adena { get; private set; }
    public List<Product> Products { get; private set; }

    public WarehouseListPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        Type = (WarehouseType)ReadH();
        Adena = ReadI();

        int count = ReadH();
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
            ReadI(); // augmentation
            ReadI();
            Products.Add(p);
        }
    }
}
