using System;
using System.Collections.Generic;

// Vente a un magasin d'achat : le serveur verifie objet, enchantement et prix.
public class RequestPrivateStoreSellPacket : ClientPacket
{
    public RequestPrivateStoreSellPacket(int storeObjectId, List<Product> products) : base((byte)GameClientPacketType.RequestPrivateStoreSell)
    {
        WriteI(storeObjectId);
        WriteI(products.Count);

        products.ForEach((p) =>
        {
            WriteI(p.ObjectId);
            WriteI(p.ItemId);
            WriteB(BitConverter.GetBytes((short)p.Enchant));
            WriteB(new byte[2]);
            WriteI(p.Count);
            WriteI(p.Price);
        });

        BuildPacket();
    }
}
