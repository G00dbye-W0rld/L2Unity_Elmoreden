using System.Collections.Generic;

public class SetPrivateStoreListSellPacket : ClientPacket
{
    public SetPrivateStoreListSellPacket(bool packageSale, List<Product> products) : base((byte)GameClientPacketType.SetPrivateStoreListSell)
    {
        WriteI(packageSale ? 1 : 0);
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
