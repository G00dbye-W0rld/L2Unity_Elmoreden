public class RequestJoinPartyPacket : ClientPacket
{
    public RequestJoinPartyPacket(string targetName, int lootRuleId) : base((byte)GameClientPacketType.RequestJoinParty)
    {
        WriteS(targetName);
        WriteI(lootRuleId);
        BuildPacket();
    }
}
