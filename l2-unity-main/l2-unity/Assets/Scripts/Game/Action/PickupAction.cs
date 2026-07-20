using UnityEngine;

// Action de la barre de raccourcis (ActionType.Pickup) : ramasse l'objet au
// sol le plus proche dans un rayon court, sans avoir a cliquer dessus.
public class PickupAction : L2Action
{
    // Rayon de recherche assez court pour ne ramasser que ce qui est
    // vraiment a proximite immediate du joueur (pas tout ce qui est visible).
    private const float SearchRadius = 3f;

    public PickupAction() : base() { }

    public override void UseAction()
    {
        if (PlayerStateMachine.Instance.State == PlayerState.DEAD)
        {
            return;
        }

        WorldItem nearest = WorldSpawner.Instance.GetNearestItem(PlayerEntity.Instance.transform.position, SearchRadius);
        if (nearest == null)
        {
            // Message local (jamais envoye par le serveur, donc pas lie a un
            // id de SystemMsg_Classic-eu.txt - aucun message dedie n'y existe
            // pour "rien a ramasser ici"). Meme couleur/son que "Invalid
            // target." (id 109) pour rester coherent avec le reste de l'UI.
            SystemMessageDat messageData = new SystemMessageDat
            {
                Message = "Rien à ramasser autour de vous.",
                Color = "FF2222B0",
                Sound = "sys_impossible"
            };
            ChatWindow.Instance.ReceiveSystemMessage(new SystemMessage(null, messageData));
            return;
        }

        PickupIntention.Target = nearest;
        PlayerStateMachine.Instance.ChangeIntention(Intention.INTENTION_PICKUP);
    }
}
