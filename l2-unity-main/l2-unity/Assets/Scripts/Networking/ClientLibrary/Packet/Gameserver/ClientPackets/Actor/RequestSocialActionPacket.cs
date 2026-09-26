// Emote : le serveur (RequestSocialAction.java) lit un seul int, l'identifiant social,
// et n'accepte que 2 a 13. Il rediffuse ensuite un SocialAction a tout le monde.
public class RequestSocialActionPacket : ClientPacket
{
    public RequestSocialActionPacket(int actionId) : base((byte)GameClientPacketType.RequestSocialAction)
    {
        WriteI(actionId);
        BuildPacket();
    }
}
