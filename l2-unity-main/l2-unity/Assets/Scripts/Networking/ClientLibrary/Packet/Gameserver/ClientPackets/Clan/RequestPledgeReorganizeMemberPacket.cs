// Change un membre d'unite. Sans membre choisi, simple deplacement si l'unite
// a de la place ; sinon echange avec le membre nomme.
public class RequestPledgeReorganizeMemberPacket : ClientPacket
{
    public RequestPledgeReorganizeMemberPacket(string member, int newPledgeType, string swapWith) : base((byte)GameClientPacketType.DoubleOPCode)
    {
        WriteB((byte)GameClientPacketDoubleType.RequestPledgeReorganizeMember);
        WriteB(0);
        WriteI(string.IsNullOrEmpty(swapWith) ? 0 : 1);
        WriteS(member);
        WriteI(newPledgeType);
        WriteS(swapWith ?? string.Empty);
        BuildPacket();
    }
}
