// Lire (action 1) ou ecrire (action 2) les privileges d'un rang.
public class RequestPledgePowerPacket : ClientPacket
{
    public RequestPledgePowerPacket(int rank, int action, int privileges) : base((byte)GameClientPacketType.RequestPledgePower)
    {
        WriteI(rank);
        WriteI(action);
        if (action == 2)
        {
            WriteI(privileges);
        }

        BuildPacket();
    }
}
