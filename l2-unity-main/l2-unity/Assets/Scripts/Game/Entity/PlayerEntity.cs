using System;
using UnityEngine;

// Used by LOCAL PLAYER
public class PlayerEntity : Entity
{
    private static PlayerEntity _instance;
    public static PlayerEntity Instance { get => _instance; }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        EntityLoaded = true;
    }

    void OnDestroy()
    {
        _instance = null;
    }

    protected override void LookAtTarget() { }

    public override float UpdateRunSpeed(int speed)
    {
        float converted = base.UpdateRunSpeed(speed);
        PlayerController.Instance.DefaultRunSpeed = converted;

        return converted;
    }

    public override float UpdateWalkSpeed(int speed)
    {
        float converted = base.UpdateWalkSpeed(speed);
        PlayerController.Instance.DefaultWalkSpeed = converted;

        return converted;
    }

    public override float UpdateSwimSpeed(int speed)
    {
        float converted = base.UpdateSwimSpeed(speed);
        PlayerController.Instance.DefaultSwimSpeed = converted;

        return converted;
    }

    public void OnActionFailed()
    {
        PlayerStateMachine.Instance.OnActionDenied();
    }

    public void OnActionAllowed()
    {
        PlayerStateMachine.Instance.OnActionAllowed();
    }

    // public  

    public override void UpdateWaitType(ChangeWaitTypePacket.WaitType moveType)
    {
        base.UpdateWaitType(moveType);

        PlayerStateMachine.Instance.OnActionAllowed();
    }

    public override void UpdateMoveType(bool running)
    {

        base.UpdateMoveType(running);

        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.Running = running;
        }

        if (PlayerStateMachine.Instance != null)
        {
            PlayerStateMachine.Instance.NotifyEvent(Event.MOVE_TYPE_UPDATED);
        }

        if (CharacterInfoWindow.Instance != null)
        {
            CharacterInfoWindow.Instance.UpdateValues();
        }
    }

    public override void UpdateSwimming(bool swimming)
    {
        base.UpdateSwimming(swimming);

        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.Swimming = swimming;
        }

        AudioManager.Instance?.Play3DSoundByReferenceName(swimming ? "ChrSound/water_in" : "ChrSound/water_out", transform.position);

        if (!swimming)
        {
            // Force une nouvelle recherche des volumes d'eau a la prochaine
            // session de nage, au cas ou on aurait change de carte entre
            // temps (sans effet si toujours la meme carte, juste un cache a
            // reconstruire une fois).
            WaterSurfaceQuery.Invalidate();
        }

        if (PlayerStateMachine.Instance != null)
        {
            PlayerStateMachine.Instance.NotifyEvent(Event.MOVE_TYPE_UPDATED);
        }
    }
}