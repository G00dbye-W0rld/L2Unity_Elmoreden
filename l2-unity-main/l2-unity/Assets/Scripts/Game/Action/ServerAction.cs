// Action entierement traitee par le serveur : on lui transmet son identifiant.
public class ServerAction : L2Action
{
    private readonly ActionType _type;

    public ServerAction(ActionType type) : base()
    {
        _type = type;
    }

    public override void UseAction()
    {
        if (PlayerStateMachine.Instance.State == PlayerState.DEAD)
        {
            return;
        }

        GameClient.Instance.ClientPacketHandler.RequestActionUse((int)_type);
    }
}
