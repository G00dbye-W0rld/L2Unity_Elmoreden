using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerActions : MonoBehaviour
{
    private Dictionary<ActionType, L2Action> _actions;
    private static PlayerActions _instance;
    public static PlayerActions Instance
    {
        get { return _instance; }
    }

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

        _actions = new Dictionary<ActionType, L2Action>();
        _actions.Add(ActionType.Attack, new AttackAction());
        _actions.Add(ActionType.Sit, new SitStandAction());
        _actions.Add(ActionType.WalkRun, new WalkRunAction());
        _actions.Add(ActionType.NextTarget, new NextTargetAction());
        _actions.Add(ActionType.Pickup, new PickupAction());
        _actions.Add(ActionType.PartyInvite, new PartyInviteAction());
        _actions.Add(ActionType.PartyLeave, new PartyLeaveAction());
        _actions.Add(ActionType.PartyKick, new PartyKickAction());
        _actions.Add(ActionType.PartyChangeLeader, new PartyChangeLeaderAction());
        _actions.Add(ActionType.PrivateStoreSell, new ServerAction(ActionType.PrivateStoreSell));
        _actions.Add(ActionType.PackageSale, new ServerAction(ActionType.PackageSale));
        _actions.Add(ActionType.PrivateStoreBuy, new ServerAction(ActionType.PrivateStoreBuy));
        _actions.Add(ActionType.Trade, new TradeAction());
    }

    private void OnDestroy()
    {
        _instance = null;
    }

    private void Update()
    {
        ListenToKeybindedActions();
    }

    private void ListenToKeybindedActions()
    {
        if (InputManager.Instance.Attack)
        {
            UseAction(ActionType.Attack);
        }

        if (InputManager.Instance.NextTarget)
        {
            UseAction(ActionType.NextTarget);
        }

        if (InputManager.Instance.TargetSelf)
        {
            ObjectData data = new ObjectData(PlayerEntity.Instance.transform.gameObject);
            TargetManager.Instance.SetTarget(data);
        }
    }

    public void UseAction(ActionType actionType)
    {
        if (_actions.TryGetValue(actionType, out L2Action action))
        {
            action.UseAction();
            return;
        }

        // Les emotes ne sont pas enregistrees une par une : la table du client dit
        // lesquelles en sont, et sous quel identifiant social les envoyer.
        ActionData data = ActionNameTable.Instance.GetAction(actionType);
        if (data != null && data.IsSocial)
        {
            L2Action social = new SocialAction(data.Type, data.IsCouple);
            _actions.Add(actionType, social);
            social.UseAction();
            return;
        }

        Debug.LogWarning($"Action not found: {(int)actionType}.");
    }

    // Commande de chat, "/socialbow" et compagnie : la table porte le nom sans la barre.
    public bool UseCommand(string command)
    {
        if (string.IsNullOrEmpty(command))
        {
            return false;
        }

        foreach (ActionData data in ActionNameTable.Instance.Actions.Values)
        {
            if (data.IsSocial && string.Equals(data.Command, command, System.StringComparison.OrdinalIgnoreCase))
            {
                UseAction((ActionType)data.Id);
                return true;
            }
        }

        return false;
    }
}