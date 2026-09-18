// Paquets de l'entrepot, gardes a part de GameServerPacketHandler.
public static class WarehousePacketHandler
{
    public static void OnList(byte[] data, bool deposit, EventProcessor eventProcessor)
    {
        WarehouseListPacket packet = new WarehouseListPacket(data);
        eventProcessor.QueueEvent(() =>
        {
            WarehouseWindow.Instance?.Open(packet.Type, deposit, packet.Adena, packet.Products);
        });
    }

    public static void OnPackageTargets(byte[] data, EventProcessor eventProcessor)
    {
        PackageToListPacket packet = new PackageToListPacket(data);
        eventProcessor.QueueEvent(() =>
        {
            WarehouseWindow.Instance?.PrepareFreight(packet.Characters);
        });
    }

    public static void OnPackageSendable(byte[] data, EventProcessor eventProcessor)
    {
        PackageSendableListPacket packet = new PackageSendableListPacket(data);
        eventProcessor.QueueEvent(() =>
        {
            WarehouseWindow.Instance?.OpenFreightDeposit(packet.TargetId, packet.Adena, packet.Products);
        });
    }
}
