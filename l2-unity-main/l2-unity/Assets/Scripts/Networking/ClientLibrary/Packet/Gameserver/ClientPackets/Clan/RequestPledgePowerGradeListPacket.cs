// Demande le nombre de membres par rang.
public class RequestPledgePowerGradeListPacket : ClientPacket
{
    public RequestPledgePowerGradeListPacket() : base((byte)GameClientPacketType.DoubleOPCode)
    {
        WriteB((byte)GameClientPacketDoubleType.RequestPledgePowerGradeList);
        WriteB(0);
        BuildPacket();
    }
}
