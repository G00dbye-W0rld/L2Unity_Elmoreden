// Commandes utilisateur du client d'origine (/loc, /time...) : le serveur les
// reconnait par un numero. 0 = /loc, qui renvoie la position en message
// systeme.
public class UserCommandPacket : ClientPacket
{
    public const int Loc = 0;
    public const int ClanWarList = 88;
    public const int AttackerList = 89;
    public const int UnderAttackList = 90;
    public const int ClanPenalty = 100;

    public UserCommandPacket(int commandId) : base((byte)GameClientPacketType.UserCommand)
    {
        WriteI(commandId);
        BuildPacket();
    }
}
