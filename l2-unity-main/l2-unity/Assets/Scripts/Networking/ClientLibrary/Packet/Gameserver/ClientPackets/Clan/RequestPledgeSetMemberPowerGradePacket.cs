// Change le rang d'un membre.
public class RequestPledgeSetMemberPowerGradePacket : ClientPacket
{
    public RequestPledgeSetMemberPowerGradePacket(string name, int powerGrade) : base((byte)GameClientPacketType.DoubleOPCode)
    {
        WriteB((byte)GameClientPacketDoubleType.RequestPledgeSetMemberPowerGrade);
        WriteB(0);
        WriteS(name);
        WriteI(powerGrade);
        BuildPacket();
    }
}
