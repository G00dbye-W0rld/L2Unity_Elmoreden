// Demande la fiche d'un membre. Le serveur ignore l'unite envoyee.
public class RequestPledgeMemberInfoPacket : ClientPacket
{
    public RequestPledgeMemberInfoPacket(int pledgeType, string name) : base((byte)GameClientPacketType.DoubleOPCode)
    {
        WriteB((byte)GameClientPacketDoubleType.RequestPledgeMemberInfo);
        WriteB(0);
        WriteI(pledgeType);
        WriteS(name);
        BuildPacket();
    }
}
