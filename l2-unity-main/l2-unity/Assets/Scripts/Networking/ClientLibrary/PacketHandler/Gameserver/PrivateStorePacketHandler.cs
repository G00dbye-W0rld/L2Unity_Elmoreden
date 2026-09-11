// Paquets du magasin d'achat, gardes a part de GameServerPacketHandler.
public static class PrivateStorePacketHandler
{
    public static void OnManageListBuy(byte[] data, EventProcessor eventProcessor)
    {
        PrivateStoreManageListBuyPacket packet = new PrivateStoreManageListBuyPacket(data);
        eventProcessor.QueueEvent(() =>
        {
            PrivateStoreWindow.Instance?.Open(PrivateStoreMode.Buy, packet.Adena, false, packet.Inventory, packet.Store);
        });
    }

    public static void OnListBuy(byte[] data, EventProcessor eventProcessor)
    {
        PrivateStoreListBuyPacket packet = new PrivateStoreListBuyPacket(data);
        eventProcessor.QueueEvent(() =>
        {
            PrivateStoreBuyWindow.Instance?.Open(PrivateStoreMode.Buy, packet.StoreObjectId, false, packet.Adena, packet.Products);
        });
    }
}
