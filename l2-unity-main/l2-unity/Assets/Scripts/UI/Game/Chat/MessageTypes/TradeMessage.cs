public class TradeMessage : ChatMessage
{
    public TradeMessage(string user, string message) : base(user, message, L2MessageType.TRADE)
    {
    }

    public override string ToString()
    {
        return "<color=#f5a5ea>" + _user + ": " + _message + "</color>";
    }
}