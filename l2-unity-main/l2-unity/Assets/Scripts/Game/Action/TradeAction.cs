// Propose un echange au joueur cible ; le serveur verifie tout le reste
// (distance, magasin ouvert, olympiade, liste d'ignores).
public class TradeAction : L2Action
{
    public TradeAction() : base() { }

    public override void UseAction()
    {
        Entity target = TargetManager.Instance.HasTarget() ? TargetManager.Instance.Target : null;

        if (target == null || target.Identity.EntityType != EntityType.User)
        {
            return;
        }

        GameClient.Instance.ClientPacketHandler.SendTradeRequest(target.Identity.Id);
    }
}
