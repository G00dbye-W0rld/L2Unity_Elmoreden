// Fiche publique d'un clan (ajout du projet, sous-code 0x5c). WarState vu
// depuis notre clan : 1 nous l'avons declaree, 2 il nous l'a declaree, 3 mutuelle.
public class ExClanCardPacket : ServerPacket
{
    public ClanCard Card { get; private set; }

    public ExClanCardPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ReadH();

        ClanCard card = new ClanCard();
        card.ClanId = ReadI();
        card.Name = ReadS();
        card.Level = ReadI();
        card.LeaderName = ReadS();
        card.Members = ReadI();
        card.OnlineMembers = ReadI();
        card.AllyId = ReadI();
        card.AllyName = ReadS();
        card.CrestId = ReadI();
        card.AllyCrestId = ReadI();
        card.LargeCrestId = ReadI();
        card.CastleId = ReadI();
        card.ClanHallId = ReadI();
        card.Reputation = ReadI();
        card.WarState = ReadI();
        Card = card;
    }
}

public class ClanCard
{
    public int ClanId;
    public string Name;
    public int Level;
    public string LeaderName;
    public int Members;
    public int OnlineMembers;
    public int AllyId;
    public string AllyName;
    public int CrestId;
    public int AllyCrestId;
    public int LargeCrestId;
    public int CastleId;
    public int ClanHallId;
    public int Reputation;
    public int WarState;
}
