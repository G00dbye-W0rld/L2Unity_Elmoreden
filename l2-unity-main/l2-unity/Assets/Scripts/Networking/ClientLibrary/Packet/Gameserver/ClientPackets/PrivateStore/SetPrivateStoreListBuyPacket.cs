using System;
using System.Collections.Generic;

public class SetPrivateStoreListBuyPacket : ClientPacket
{
    public SetPrivateStoreListBuyPacket(List<Product> products) : base((byte)GameClientPacketType.SetPrivateStoreListBuy)
    {
        WriteI(products.Count);

        products.ForEach((p) =>
        {
            WriteI(p.ItemId);
            WriteB(BitConverter.GetBytes((short)p.Enchant));
            WriteB(new byte[2]);
            WriteI(p.Count);
            WriteI(p.Price);
        });

        BuildPacket();
    }
}
