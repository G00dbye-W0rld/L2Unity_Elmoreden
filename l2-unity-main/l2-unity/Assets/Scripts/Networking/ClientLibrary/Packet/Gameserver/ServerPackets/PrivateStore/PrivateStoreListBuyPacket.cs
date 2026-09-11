using System.Collections.Generic;

// Vitrine d'un magasin d'achat, vue par un vendeur. Count vaut deja le minimum
// entre ce que le vendeur possede et ce que l'acheteur veut ; 0 = il ne l'a pas.
public class PrivateStoreListBuyPacket : ServerPacket
{
    public int StoreObjectId { get; private set; }
    public int Adena { get; private set; }
    public List<Product> Products { get; private set; }

    public PrivateStoreListBuyPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        StoreObjectId = ReadI();
        Adena = ReadI();

        int count = ReadI();
        Products = new List<Product>(count);

        for (int i = 0; i < count; i++)
        {
            Product p = new Product();
            p.ObjectId = ReadI();
            p.ItemId = ReadI();
            p.Enchant = ReadH();
            p.Count = ReadI();
            p.ReferencePrice = ReadI();
            ReadH();
            p.BodyPart = (ItemSlot)ReadI();
            p.Type2 = (ItemType2)ReadH();
            p.Price = ReadI();
            ReadI(); // quantite voulue, deja incluse dans Count
            Products.Add(p);
        }
    }
}
