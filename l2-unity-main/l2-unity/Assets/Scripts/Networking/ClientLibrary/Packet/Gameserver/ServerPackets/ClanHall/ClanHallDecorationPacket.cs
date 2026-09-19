// Decor d'une salle de clan : pour chacun des 12 emplacements, le niveau de
// l'objet a afficher (0 = aucun). Envoye a l'entree dans la salle et apres
// chaque changement d'installation.
public class ClanHallDecorationPacket : ServerPacket
{
    public int HallId { get; private set; }
    public byte[] Depths { get; private set; }

    public ClanHallDecorationPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        HallId = ReadI();
        Depths = new byte[ClanHallDecor.SlotCount];
        for (int i = 0; i < Depths.Length; i++)
        {
            Depths[i] = ReadB();
        }
    }
}
