// Une ligne de la liste des membres du clan.
public class ClanMemberInfo
{
    public string Name;
    public int Level;
    public int ClassId;
    public int Sex;
    public int Race;
    public int ObjectId;
    public int PledgeType;
    public bool HasSponsor;

    public bool IsOnline { get { return ObjectId != 0; } }
}
