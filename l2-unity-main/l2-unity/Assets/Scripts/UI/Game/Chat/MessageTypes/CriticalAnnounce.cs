public class CriticalAnnounceMesasge : ChatMessage
{
    public CriticalAnnounceMesasge(string user, string message) : base(user, message, L2MessageType.CRITICAL_ANNOUNCE)
    {
    }

    public override string ToString()
    {
        return "<color=#00FFFF>" + _user + ": " + _message + "</color>";
    }
}