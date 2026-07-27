public class NormalMessage : ChatMessage
{
    public NormalMessage(string user, string message) : base(user, message, L2MessageType.ALL)
    {
    }

    public override string ToString()
    {
        return "<color=#DDDDDD>" + _user + ": " + _message + "</color>";
    }
}