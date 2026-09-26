// Emote. L'identifiant envoye n'est pas celui de l'action mais le "type" de ActionName,
// qui vaut 2 a 13 : c'est ce que RequestSocialAction.java attend.
using UnityEngine;

public class SocialAction : L2Action
{
    private readonly int _socialId;
    private readonly bool _couple;

    public SocialAction(int socialId, bool couple) : base()
    {
        _socialId = socialId;
        _couple = couple;
    }

    public override void UseAction()
    {
        if (PlayerStateMachine.Instance.State == PlayerState.DEAD)
        {
            return;
        }

        // Le serveur refuse une emote si le personnage n'est pas au repos, autant
        // ne pas l'envoyer : assis, en magasin ou en mouvement.
        if (PlayerStateMachine.IsPlayerInStoreMode()
            || PlayerStateMachine.Instance.State == PlayerState.SITTING
            || PlayerStateMachine.Instance.State == PlayerState.SIT_WAIT)
        {
            return;
        }

        if (!_couple)
        {
            GameClient.Instance.ClientPacketHandler.SendRequestSocialAction(_socialId);
            return;
        }

        // Une emote a deux vise quelqu'un : sans cible, le client d'origine ne fait rien.
        Entity target = TargetManager.Instance != null ? TargetManager.Instance.Target : null;
        if (target == null || target.Identity == null)
        {
            return;
        }

        GameClient.Instance.ClientPacketHandler.SendRequestCoupleAction(target.Identity.Id, _socialId);
    }
}
