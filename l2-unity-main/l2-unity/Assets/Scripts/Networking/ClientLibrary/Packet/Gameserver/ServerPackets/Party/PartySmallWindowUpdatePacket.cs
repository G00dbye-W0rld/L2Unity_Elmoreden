// CP/HP/MP/niveau d'un membre du groupe ont change (envoye a chaque membre
// du groupe des qu'un des trois change chez l'un d'eux, cf.
// PlayerStatus.java#broadcastStatusUpdate cote serveur). C'est le SEUL moyen
// de connaitre en temps reel les stats d'un membre hors de portee de rendu
// (son Entity locale n'existe pas forcement) - sans ce paquet, PartyWindow
// ne pouvait afficher qu'un instantane fige pris a l'ajout au groupe.
public class PartySmallWindowUpdatePacket : ServerPacket
{
    public int ObjectId { get; private set; }
    public string Name { get; private set; }
    public int Cp { get; private set; }
    public int MaxCp { get; private set; }
    public int Hp { get; private set; }
    public int MaxHp { get; private set; }
    public int Mp { get; private set; }
    public int MaxMp { get; private set; }
    public int Level { get; private set; }
    public int ClassId { get; private set; }

    public PartySmallWindowUpdatePacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        ObjectId = ReadI();
        Name = ReadS();
        Cp = ReadI();
        MaxCp = ReadI();
        Hp = ReadI();
        MaxHp = ReadI();
        Mp = ReadI();
        MaxMp = ReadI();
        Level = ReadI();
        ClassId = ReadI();
    }
}
