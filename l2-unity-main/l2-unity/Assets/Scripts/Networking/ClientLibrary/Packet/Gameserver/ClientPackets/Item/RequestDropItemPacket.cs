using UnityEngine;

public class RequestDropItemPacket : ClientPacket
{
    // Le serveur (RequestDropItem.java) lit objectId, count, x, y, z (5 ints)
    // et valide la distance via isIn3DRadius(x,y,z,...) - le packet ne
    // contenait avant que objectId+count (2 ints), un mismatch qui aurait
    // fait mal parser les 3 ints suivants du flux reseau.
    public RequestDropItemPacket(int objectId, int count, Vector3 position) : base((byte)GameClientPacketType.RequestDropItem)
    {
        WriteI(objectId);
        WriteI(count);
        WriteI((int)(position.z * 52.5f));
        WriteI((int)(position.x * 52.5f));
        WriteI((int)(position.y * 52.5f));
        BuildPacket();
    }
}