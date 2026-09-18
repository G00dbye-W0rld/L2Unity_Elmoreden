using System.Collections.Generic;

// Alliance du joueur : son nom, son chef, et chaque clan membre.
public class AllianceInfoPacket : ServerPacket
{
    public struct Member
    {
        public string ClanName;
        public int Level;
        public string LeaderName;
        public int Total;
        public int Online;
    }

    public string Name { get; private set; }
    public int Total { get; private set; }
    public int Online { get; private set; }
    public string LeaderClan { get; private set; }
    public string LeaderName { get; private set; }
    public List<Member> Clans { get; private set; } = new List<Member>();

    public AllianceInfoPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        Name = ReadS();
        Total = ReadI();
        Online = ReadI();
        LeaderClan = ReadS();
        LeaderName = ReadS();

        int count = ReadI();
        for (int i = 0; i < count && _iterator < _packetData.Length; i++)
        {
            Member member = new Member();
            member.ClanName = ReadS();
            ReadI();
            member.Level = ReadI();
            member.LeaderName = ReadS();
            member.Total = ReadI();
            member.Online = ReadI();
            Clans.Add(member);
        }
    }
}
