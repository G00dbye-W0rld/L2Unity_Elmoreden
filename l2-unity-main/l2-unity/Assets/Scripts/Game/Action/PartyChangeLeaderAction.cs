// Transfere le lead du groupe a la cible actuelle. Le serveur verifie deja
// que l'expediteur est bien le leader (RequestChangePartyLeader.java) - pas
// besoin de le revalider cote client, mais on filtre quand meme ici pour ne
// pas envoyer un paquet inutile a un non-leader.
public class PartyChangeLeaderAction : L2Action
{
    public PartyChangeLeaderAction() : base() { }

    public override void UseAction()
    {
        if (!PartyManager.Instance.IsLeader) return;
        if (!TargetManager.Instance.HasTarget()) return;

        Entity target = TargetManager.Instance.Target;
        if (target == PlayerEntity.Instance) return;
        if (!PartyManager.Instance.IsMember(target)) return;

        GameClient.Instance.ClientPacketHandler.SendRequestChangePartyLeader(target.Identity.Name);
    }
}
