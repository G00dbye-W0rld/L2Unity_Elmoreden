// Exclut la cible actuelle du groupe. Le serveur verifie deja que
// l'expediteur est bien le leader (RequestOustPartyMember.java) - pas besoin
// de le revalider cote client.
public class PartyKickAction : L2Action
{
    public PartyKickAction() : base() { }

    public override void UseAction()
    {
        if (!PartyManager.Instance.IsLeader) return;
        if (!TargetManager.Instance.HasTarget()) return;

        GameClient.Instance.ClientPacketHandler.SendRequestOustPartyMember(TargetManager.Instance.Target.Identity.Name);
    }
}
