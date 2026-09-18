// Reponse a une fenetre de confirmation (ConfirmDlg) : 1 = oui, 0 = non.
public class DlgAnswerPacket : ClientPacket
{
    public DlgAnswerPacket(int messageId, int answer, int requesterId) : base((byte)GameClientPacketType.DlgAnswer)
    {
        WriteI(messageId);
        WriteI(answer);
        WriteI(requesterId);
        BuildPacket();
    }
}
