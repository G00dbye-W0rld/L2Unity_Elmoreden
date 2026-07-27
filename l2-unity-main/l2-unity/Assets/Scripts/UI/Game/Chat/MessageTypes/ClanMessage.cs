public class ClanMessage : ChatMessage
{
    public ClanMessage(string user, string message) : base(user, message, L2MessageType.CLAN)
    {
    }

    public override string ToString()
    {
        return "<color=#7D77FF>" + _user + ": " + _message + "</color>";
    }
}