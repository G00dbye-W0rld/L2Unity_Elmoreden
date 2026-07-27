public class HeroMessage : ChatMessage
{
    public HeroMessage(string user, string message) : base(user, message, L2MessageType.HERO_VOICE)
    {
    }

    public override string ToString()
    {
        return "<color=#408CFF>" + _user + ": " + _message + "</color>";
    }
}