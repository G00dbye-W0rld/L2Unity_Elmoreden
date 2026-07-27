public class AskJoinPartyPacket : ServerPacket
{
    public string RequestorName { get; private set; }
    public int LootRuleId { get; private set; }

    public AskJoinPartyPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        RequestorName = ReadS();
        LootRuleId = ReadI();
    }
}
