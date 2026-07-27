public class RequestOustPartyMemberPacket : ClientPacket
{
    public RequestOustPartyMemberPacket(string targetName) : base((byte)GameClientPacketType.RequestOustPartyMember)
    {
        WriteS(targetName);
        BuildPacket();
    }
}
