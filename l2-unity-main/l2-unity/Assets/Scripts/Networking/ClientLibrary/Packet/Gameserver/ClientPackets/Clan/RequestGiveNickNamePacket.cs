// Donner un titre a un membre du clan (ou a soi-meme).
public class RequestGiveNickNamePacket : ClientPacket
{
    public RequestGiveNickNamePacket(string name, string title) : base((byte)GameClientPacketType.RequestGiveNickName)
    {
        WriteS(name);
        WriteS(title);
        BuildPacket();
    }
}
