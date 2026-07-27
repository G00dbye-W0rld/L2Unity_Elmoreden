public class AllianceMessage : ChatMessage
{
    public AllianceMessage(string user, string message) : base(user, message, L2MessageType.ALLIANCE)
    {
    }

    public override string ToString()
    {
        return "<color=#77FF99>" + _user + ": " + _message + "</color>";
    }
}