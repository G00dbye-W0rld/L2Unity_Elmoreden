// Nom d'un clan et de son alliance, en reponse a RequestPledgeInfo. Les
// paquets d'apparition ne portent que des identifiants : c'est par ici que
// le client apprend les noms a afficher.
public class PledgeInfoPacket : ServerPacket
{
    public int ClanId { get; private set; }
    public string ClanName { get; private set; }
    public string AllyName { get; private set; }

    public PledgeInfoPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ClanId = ReadI();
        ClanName = ReadS();
        AllyName = ReadS();
    }
}
