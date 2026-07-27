// Resultat d'une tentative d'enchantement (RequestEnchantItem.java, cote
// serveur). Ne porte ni ObjectId ni niveau resultant : le nouvel etat de
// l'objet (niveau d'enchant, destruction) arrive via le rafraichissement
// d'inventaire habituel (InventoryUpdate) et un SystemMessage explicatif,
// deja geres par les systemes existants - ce paquet ne sert qu'a savoir
// quand sortir du mode selection (cf. EnchantManager) et, si besoin plus
// tard, a distinguer le cas particulier ou l'objet a ete detruit.
public enum EnchantResultCode
{
    Success = 0,
    FailedItemDestroyedNoCrystal = 1,
    Cancelled = 2,
    FailedBlessedReset = 3,
    FailedItemDestroyed = 4,
}

public class EnchantResultPacket : ServerPacket
{
    public EnchantResultCode Result { get; private set; }

    public EnchantResultPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        Result = (EnchantResultCode)ReadI();
    }
}
