// Lie (set = 1) ou delie (set = 0) un parrain et son apprenti de l'academie.
public class RequestPledgeSetAcademyMasterPacket : ClientPacket
{
    public RequestPledgeSetAcademyMasterPacket(bool set, string member, string other) : base((byte)GameClientPacketType.DoubleOPCode)
    {
        WriteB((byte)GameClientPacketDoubleType.RequestPledgeSetAcademyMaster);
        WriteB(0);
        WriteI(set ? 1 : 0);
        WriteS(member);
        WriteS(other);
        BuildPacket();
    }
}
