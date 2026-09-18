// Portes du monde : etat initial puis ouverture / fermeture.
public static class DoorPacketHandler
{
    public static void OnDoorInfo(byte[] data, EventProcessor eventProcessor)
    {
        DoorInfoPacket packet = new DoorInfoPacket(data);
        eventProcessor.QueueEvent(() =>
            DoorManager.Instance.Register(packet.ObjectId, packet.DoorId, packet.Position, packet.Opened));
    }

    public static void OnDoorStatusUpdate(byte[] data, EventProcessor eventProcessor)
    {
        DoorStatusUpdatePacket packet = new DoorStatusUpdatePacket(data);
        eventProcessor.QueueEvent(() =>
            DoorManager.Instance.SetState(packet.DoorId, packet.Opened));
    }
}
