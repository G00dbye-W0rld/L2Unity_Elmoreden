public class PartyMessage : ChatMessage
{
    public PartyMessage(string user, string message) : base(user, message, L2MessageType.PARTY)
    {
    }

    public override string ToString()
    {
        return "<color=#00FF00>" + _user + ": " + _message + "</color>";
    }
}