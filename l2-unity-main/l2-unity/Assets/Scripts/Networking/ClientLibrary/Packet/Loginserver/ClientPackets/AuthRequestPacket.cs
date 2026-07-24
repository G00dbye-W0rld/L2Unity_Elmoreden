public class AuthRequestPacket : LoginClientPacket
{
    public AuthRequestPacket(byte[] rsaBlock) : base((byte)LoginClientPacketType.AuthRequest)
    {
        WriteB(rsaBlock);
        // Identifiant machine (limite "un client lance par machine" cote loginserver).
        WriteS(UnityEngine.SystemInfo.deviceUniqueIdentifier);
        BuildPacket();
    }
}