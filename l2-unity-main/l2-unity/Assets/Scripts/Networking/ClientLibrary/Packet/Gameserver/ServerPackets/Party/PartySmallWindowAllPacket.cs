using System.Collections.Generic;

// Etat complet du groupe, envoye a un membre qui vient de le rejoindre (tous
// les AUTRES membres, pas soi-meme - cf. Party.java cote serveur).
public class PartySmallWindowAllPacket : ServerPacket
{
    public int LeaderObjectId { get; private set; }
    public int LootRuleId { get; private set; }
    public List<PartyMemberInfo> Members { get; private set; } = new List<PartyMemberInfo>();

    public PartySmallWindowAllPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        LeaderObjectId = ReadI();
        LootRuleId = ReadI();
        int memberCount = ReadI();

        for (int i = 0; i < memberCount; i++)
        {
            PartyMemberInfo member = new PartyMemberInfo
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
            ReadI(); // reserve (toujours 0 cote serveur)
            member.Race = ReadI();

            Members.Add(member);
        }
    }
}
