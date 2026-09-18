using System.Collections.Generic;

// Etat complet d'une unite du clan : le clan principal quand PledgeType vaut
// 0, sinon une sous-unite (academie, garde royale, chevalerie).
public class PledgeShowMemberListAllPacket : ServerPacket
{
    public bool IsSubPledge { get; private set; }
    public int ClanId { get; private set; }
    public int PledgeType { get; private set; }
    public string PledgeName { get; private set; }
    public string LeaderName { get; private set; }
    public int CrestId { get; private set; }
    public int Level { get; private set; }
    public int CastleId { get; private set; }
    public int ClanHallId { get; private set; }
    public int Rank { get; private set; }
    public int Reputation { get; private set; }
    public int Dissolution { get; private set; }
    public int AllyId { get; private set; }
    public string AllyName { get; private set; }
    public int AllyCrestId { get; private set; }
    public bool AtWar { get; private set; }
    public List<ClanMemberInfo> Members { get; private set; } = new List<ClanMemberInfo>();

    public PledgeShowMemberListAllPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        IsSubPledge = ReadI() != 0;
        ClanId = ReadI();
        PledgeType = ReadI();
        PledgeName = ReadS();
        LeaderName = ReadS();

        CrestId = ReadI();
        Level = ReadI();
        CastleId = ReadI();
        ClanHallId = ReadI();
        Rank = ReadI();
        Reputation = ReadI();
        Dissolution = ReadI();
        ReadI(); // reserve (toujours 0 cote serveur)
        AllyId = ReadI();
        AllyName = ReadS();
        AllyCrestId = ReadI();
        AtWar = ReadI() != 0;

        int count = ReadI();
        for (int i = 0; i < count; i++)
        {
            ClanMemberInfo member = new ClanMemberInfo
            {
                Name = ReadS(),
                Level = ReadI(),
                ClassId = ReadI(),
                Sex = ReadI(),
                Race = ReadI(),
                ObjectId = ReadI(),
                PledgeType = PledgeType
            };
            member.HasSponsor = ReadI() != 0;

            Members.Add(member);
        }
    }
}
