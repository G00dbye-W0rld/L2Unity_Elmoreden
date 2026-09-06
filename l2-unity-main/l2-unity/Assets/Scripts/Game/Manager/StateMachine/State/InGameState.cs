using UnityEngine;

public class InGameState : GameStateBase
{
    public InGameState(GameManager stateMachine) : base(stateMachine) { }

    public override void Enter(object arg0)
    {
        if (arg0 != null && (bool)arg0 == true)
        {
            GameClient.Instance.ClientPacketHandler.NotifyAppearing();
        }
        else
        {
            GameClient.Instance.ClientPacketHandler.SendLoadWorld();
        }
    }

    public override void Update()
    {

    }

    public override void HandleEvent(GameEvent evt, object arg0)
    {
        switch (evt)
        {
            case GameEvent.RESTART_ALLOWED:
                _stateMachine.ChangeState(GameState.RESTARTING);
                break;
            case GameEvent.CHAR_LOADED:
                // La camera du joueur existe seulement maintenant. C'est donc
                // le premier instant ou sa portee peut etre bornee a l'horizon
                // du brouillard : ApplyAll, declenchee bien plus tot a la
                // construction de l'interface, n'avait encore rien a regler.
                GameSettings.RefreshCameraReach();

                _stateMachine.StopLoading();
                break;
            case GameEvent.GAME_DISCONNECTED:
                _stateMachine.ChangeState(GameState.DISONNECTING);
                break;
            case GameEvent.TELEPORTING:
                _stateMachine.ChangeState(GameState.TELEPORTING);
                break;
            default:
                Debug.LogWarning($"[GameStateMachine] Unhandled event {evt} for state {_stateMachine.State}");
                break;
        }
    }
}