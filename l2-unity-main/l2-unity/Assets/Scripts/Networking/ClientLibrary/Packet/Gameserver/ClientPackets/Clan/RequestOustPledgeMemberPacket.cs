// Exclure un membre du clan, par son nom.
public class RequestOustPledgeMemberPacket : ClientPacket
{
    public RequestOustPledgeMemberPacket(string name) : base((byte)GameClientPacketType.RequestOustPledgeMember)
    {
        WriteS(name);
        BuildPacket();
    }
}
