// Didascalie d'emote : pas de "nom :", le geste se raconte a la troisieme personne.
// Meme blanc que la parole Role Play pour rester au premier plan, l'italique et les
// asterisques suffisent a la distinguer d'une replique.
public class SocialActionMessage : ChatMessage
{
    public SocialActionMessage(string user, string action) : base(user, action, L2MessageType.ROLE_PLAY)
    {
    }

    public override string ToString()
    {
        return "<color=#DDDDDD><i>*" + _user + " " + _message + "*</i></color>";
    }
}
