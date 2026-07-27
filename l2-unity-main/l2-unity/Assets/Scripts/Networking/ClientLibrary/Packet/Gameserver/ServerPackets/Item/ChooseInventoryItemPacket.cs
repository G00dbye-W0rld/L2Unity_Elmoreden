// Un parchemin d'enchantement vient d'etre utilise (EnchantScrolls.java,
// cote serveur) : le joueur doit maintenant cliquer un objet de son
// inventaire a enchanter. ItemId est l'id TEMPLATE du parchemin arme (pas un
// ObjectId), utilise cote serveur pour valider la cible choisie - le client
// n'a pas besoin de dupliquer cette validation, juste d'entrer en mode
// selection (cf. EnchantManager).
public class ChooseInventoryItemPacket : ServerPacket
{
    public int ItemId { get; private set; }

    public ChooseInventoryItemPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ItemId = ReadI();
    }
}
