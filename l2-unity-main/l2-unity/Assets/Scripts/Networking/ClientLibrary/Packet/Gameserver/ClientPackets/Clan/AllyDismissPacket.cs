// Exclure un clan de l'alliance, par son nom.
public class AllyDismissPacket : ClientPacket
{
    public AllyDismissPacket(string clanName) : base((byte)GameClientPacketType.AllyDismiss)
    {
        WriteS(clanName);
        BuildPacket();
    }
}
