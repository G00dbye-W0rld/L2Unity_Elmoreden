using System.Collections.Generic;

// Depot (0x31) ou retrait (0x32) : l'entrepot vise est celui que le gardien a
// ouvert cote serveur, seuls les objets et quantites partent.
public class SendWarehouseListPacket : ClientPacket
{
    public SendWarehouseListPacket(bool deposit, List<Product> products)
        : base((byte)(deposit ? GameClientPacketType.SendWarehouseDepositList : GameClientPacketType.SendWarehouseWithdrawList))
    {
        WriteI(products.Count);

        products.ForEach((p) =>
        {
            WriteI(p.ObjectId);
            WriteI(p.Count);
        });

        BuildPacket();
    }
}
