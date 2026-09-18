// Inviter un joueur, chef de son clan, dans l'alliance (0x82), ou repondre a
// une invitation (0x83) : un entier pour contenu.
public class AllyTargetPacket : ClientPacket
{
    public AllyTargetPacket(GameClientPacketType type, int value) : base((byte)type)
    {
        WriteI(value);
        BuildPacket();
    }
}
