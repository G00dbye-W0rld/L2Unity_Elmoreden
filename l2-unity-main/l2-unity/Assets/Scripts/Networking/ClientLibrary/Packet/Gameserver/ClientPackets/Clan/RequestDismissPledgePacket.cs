// Dissoudre son clan. Le serveur ne le detruit qu'au terme du delai de
// dissolution, sept jours par defaut.
public class RequestDismissPledgePacket : ClientPacket
{
    public RequestDismissPledgePacket() : base((byte)GameClientPacketType.RequestDismissPledge)
    {
        BuildPacket();
    }
}
