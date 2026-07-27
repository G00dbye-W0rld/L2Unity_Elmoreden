public class ShoutMessage : ChatMessage
{
    public ShoutMessage(string user, string message) : base(user, message, L2MessageType.SHOUT)
    {
    }

    public override string ToString()
    {
        return "<color=#FF7200>" + _user + ": " + _message + "</color>";
    }
}