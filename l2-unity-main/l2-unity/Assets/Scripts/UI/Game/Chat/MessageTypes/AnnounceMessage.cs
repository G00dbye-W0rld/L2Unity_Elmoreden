public class AnnounceMesasge : ChatMessage
{
    public AnnounceMesasge(string user, string message) : base(user, message, L2MessageType.ANNOUNCEMENT)
    {
    }

    public override string ToString()
    {
        return "<color=#80FFFF>" + _user + ": " + _message + "</color>";
    }
}