// Hors Role Play, local : bleu-gris sourd, pour se distinguer d'un coup d'oeil
// du blanc du Role Play sans attirer l'oeil comme les canaux publics.
public class HrpMessage : ChatMessage
{
    public HrpMessage(string user, string message) : base(user, message, L2MessageType.HRP)
    {
    }

    public override string ToString()
    {
        return "<color=#8FA9C0>" + _user + ": " + _message + "</color>";
    }
}
