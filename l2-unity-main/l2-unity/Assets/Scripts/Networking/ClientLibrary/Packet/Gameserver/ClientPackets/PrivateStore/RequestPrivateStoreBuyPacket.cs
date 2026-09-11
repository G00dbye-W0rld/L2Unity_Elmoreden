using System.Collections.Generic;

// Le serveur verifie que chaque prix correspond toujours a celui du marchand.
public class RequestPrivateStoreBuyPacket : ClientPacket
{
    public RequestPrivateStoreBuyPacket(int storeObjectId, List<Product> products) : base((byte)GameClientPacketType.RequestPrivateStoreBuy)
    {
        WriteI(storeObjectId);
        WriteI(products.Count);

        products.ForEach((p) =>
        {
            WriteI(p.ObjectId);
            WriteI(p.Count);
            WriteI(p.Price);
        });

        BuildPacket();
    }
}
