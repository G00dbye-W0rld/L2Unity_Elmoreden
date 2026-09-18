// Liste des guerres : onglet 0 pour celles qu'on a declarees, 1 pour les clans
// qui nous ont declare la guerre. Le serveur pagine l'onglet 1 par 13.
public class RequestPledgeWarListPacket : ClientPacket
{
    public RequestPledgeWarListPacket(int page, int tab) : base((byte)GameClientPacketType.DoubleOPCode)
    {
        WriteB((byte)GameClientPacketDoubleType.RequestPledgeWarList);
        WriteB(0);
        WriteI(page);
        WriteI(tab);
        BuildPacket();
    }
}
