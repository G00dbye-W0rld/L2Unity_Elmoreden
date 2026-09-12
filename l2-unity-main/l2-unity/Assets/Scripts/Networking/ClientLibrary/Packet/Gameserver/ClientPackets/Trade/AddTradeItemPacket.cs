public class AddTradeItemPacket : ClientPacket
{
    public AddTradeItemPacket(int objectId, int count) : base((byte)GameClientPacketType.AddTradeItem)
    {
        WriteI(0); // identifiant d'echange, ignore par le serveur
        WriteI(objectId);
        WriteI(count);
        BuildPacket();
    }
}
