using UnityEngine;

// Calque sur InteractIntention, mais pour un WorldItem (pas un Entity - donc
// pas de TargetManager). La cible est stockee dans un champ statique (meme
// convention que MoveToIntention._lastClickToMoveLocation) car WorldItem
// n'a nulle part ailleurs ou transiter entre ClickManager et cette intention.
public class PickupIntention : IntentionBase
{
    public static WorldItem Target { get; set; }

    // Rayon d'interaction pour le ramassage (36 unites L2 / 52.5 = conversion
    // Unity), cf. PlayerAI.thinkPickUp cote gameserver.
    public const float PickupRange = 36f / 52.5f;

    public PickupIntention(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter(object arg0)
    {
        WorldItem target = Target;
        if (target == null)
        {
            _stateMachine.ChangeIntention(Intention.INTENTION_IDLE);
            return;
        }

        Vector3 targetPos = target.transform.position;

        // Distance en 2D (XZ), comme PathFinderController.FixedUpdate() qui
        // determine l'arrivee reelle - une comparaison en 3D pouvait rester
        // legerement plus grande a cause d'un ecart residuel en Y entre le
        // joueur et l'objet, empechant le "assez proche" de se declarer meme
        // apres que PathFinderController ait juge etre arrive.
        float distance = VectorUtils.Distance2D(PlayerEntity.Instance.transform.position, targetPos);

        // Marge de securite resserree (0.7 au lieu de 0.95) : le personnage
        // doit s'arreter nettement a l'interieur du rayon exige par le
        // serveur, pas pile a sa limite, sinon un arrondi/imprecision de
        // deplacement laisse un ecart qui fait echouer la demande.
        const float safetyMargin = 0.7f;

        if (distance <= PickupRange * safetyMargin)
        {
            _stateMachine.ChangeState(PlayerState.IDLE);
            _stateMachine.NotifyEvent(Event.READY_TO_PICKUP);
        }
        else
        {
            // Se deplace vers l'objet (pathfinding, respecte les obstacles) ;
            // meme mecanisme que InteractIntention pour les NPC. Le callback
            // de MoveTo se declenche des le debut du trajet (pas a l'arrivee),
            // donc on ne fait que lancer le mouvement + l'animation ici -
            // MovingState detecte la vraie arrivee et revient sur cette
            // intention pour re-verifier la distance (meme flux que INTERACT).
            PathFinderController.Instance.MoveTo(targetPos, PickupRange * safetyMargin, () =>
            {
                _stateMachine.ChangeIntention(Intention.INTENTION_FOLLOW, MoveReason.PICKUP);
            });
        }
    }

    public override void Exit() { }
    public override void Update()
    {
        if (Target == null)
        {
            _stateMachine.ChangeIntention(Intention.INTENTION_IDLE);
        }
    }
}
