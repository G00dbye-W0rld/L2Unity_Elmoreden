// 1 = valider l'echange, 0 = l'annuler.
public class TradeDonePacket : ClientPacket
{
    public TradeDonePacket(bool confirm) : base((byte)GameClientPacketType.TradeDone)
    {
        WriteI(confirm ? 1 : 0);
        BuildPacket();
    }
}
