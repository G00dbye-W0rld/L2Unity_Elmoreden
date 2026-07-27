// Demande d'enchantement de l'objet objectId, avec le parchemin deja arme
// serveur-side (ChooseInventoryItemPacket recu au prealable). Le serveur
// connait deja le parchemin (Player.activeEnchantItem) donc ce paquet ne
// porte que la cible.
public class RequestEnchantItemPacket : ClientPacket
{
    public RequestEnchantItemPacket(int objectId) : base((byte)GameClientPacketType.RequestEnchantItem)
    {
        WriteI(objectId);
        BuildPacket();
    }
}
