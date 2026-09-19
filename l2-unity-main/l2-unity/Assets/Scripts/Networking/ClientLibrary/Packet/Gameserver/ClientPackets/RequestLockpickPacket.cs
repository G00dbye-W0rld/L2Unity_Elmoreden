// Crochetage d'une porte de salle de clan (ajout du projet, sous-code 0x41) :
// 1 = tenter une goupille, 0 = abandonner.
public class RequestLockpickPacket : ClientPacket
{
    public RequestLockpickPacket(int doorObjectId, int action) : base((byte)GameClientPacketType.DoubleOPCode)
    {
        WriteB((byte)GameClientPacketDoubleType.RequestLockpick);
        WriteB(0);
        WriteI(doorObjectId);
        WriteI(action);
        BuildPacket();
    }
}
