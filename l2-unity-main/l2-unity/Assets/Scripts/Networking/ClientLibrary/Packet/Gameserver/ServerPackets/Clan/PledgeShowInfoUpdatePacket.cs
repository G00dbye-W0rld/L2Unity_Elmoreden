// Mise a jour des informations du clan, sans la liste des membres.
public class PledgeShowInfoUpdatePacket : ServerPacket
{
    public int ClanId { get; private set; }
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

    public PledgeShowInfoUpdatePacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ClanId = ReadI();
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
    }
}
