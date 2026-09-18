// Fiche publique d'un clan (ajout du projet, sous-code 0x3a).
public class RequestClanCardPacket : ClientPacket
{
    public RequestClanCardPacket(int clanId) : base((byte)GameClientPacketType.DoubleOPCode)
    {
        WriteB((byte)GameClientPacketDoubleType.RequestClanCard);
        WriteB(0);
        WriteI(clanId);
        BuildPacket();
    }
}
