public class RequestChangePartyLeaderPacket : ClientPacket
{
    public RequestChangePartyLeaderPacket(string targetName) : base((byte)GameClientPacketType.DoubleOPCode)
    {
        WriteB((byte)GameClientPacketDoubleType.RequestChangePartyLeader);
        WriteB(0);
        WriteS(targetName);
        BuildPacket();
    }
}
