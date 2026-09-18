// Paquets d'alliance sans contenu : quitter (0x84), dissoudre (0x86),
// demander les informations (0x8e).
public class AllyRequestPacket : ClientPacket
{
    public AllyRequestPacket(GameClientPacketType type) : base((byte)type)
    {
        BuildPacket();
    }
}
