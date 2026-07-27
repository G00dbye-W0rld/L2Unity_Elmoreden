// Un nouveau membre rejoint le groupe (envoye aux membres deja presents).
public class PartySmallWindowAddPacket : ServerPacket
{
    public int LeaderObjectId { get; private set; }
    public int LootRuleId { get; private set; }
    public PartyMemberInfo Member { get; private set; }

    public PartySmallWindowAddPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        LeaderObjectId = ReadI();
        LootRuleId = ReadI();

        Member = new PartyMemberInfo
        {
            ObjectId = ReadI(),
            Name = ReadS(),
            Cp = ReadI(),
            MaxCp = ReadI(),
            Hp = ReadI(),
            MaxHp = ReadI(),
            Mp = ReadI(),
            MaxMp = ReadI(),
            Level = ReadI(),
            ClassId = ReadI(),
        };
        ReadI(); // reserve
        ReadI(); // reserve (toujours 0 cote serveur, contrairement a PartySmallWindowAll qui envoie la race ici)
    }
}
