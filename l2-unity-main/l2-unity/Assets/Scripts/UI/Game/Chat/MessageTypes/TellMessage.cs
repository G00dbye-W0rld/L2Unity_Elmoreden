public class TellMessage : ChatMessage
{
    public TellMessage(string user, string message) : base(user, message, L2MessageType.TELL)
    {
    }

    public override string ToString()
    {
        // return "<color=#F428A7>" + _user + ": " + _message + "</color>";
        return "<color=#FF00FF>" + _user + ": " + _message + "</color>";
    }
}