// Dissolution du groupe (aucune donnee dans le paquet, juste le signal).
public class PartySmallWindowDeleteAllPacket : ServerPacket
{
    public PartySmallWindowDeleteAllPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
    }
}
