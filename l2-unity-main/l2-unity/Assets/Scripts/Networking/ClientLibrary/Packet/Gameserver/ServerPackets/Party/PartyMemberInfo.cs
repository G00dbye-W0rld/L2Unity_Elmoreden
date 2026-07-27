// Etat d'un membre de groupe tel que transmis par PartySmallWindowAll/PartySmallWindowAdd.
// Simple porteur de donnees, la logique de stockage/etat vit en Couche B (pas encore faite).
public class PartyMemberInfo
{
    public int ObjectId;
    public string Name;
    public int Cp;
    public int MaxCp;
    public int Hp;
    public int MaxHp;
    public int Mp;
    public int MaxMp;
    public int Level;
    public int ClassId;
    public int Race;
}
