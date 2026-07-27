// Invite la cible actuelle a rejoindre le groupe. Si un groupe existe deja,
// le mode de butin envoye est ignore cote serveur (RequestJoinParty.java :
// reutilise celui du groupe existant) - la valeur n'a d'importance que pour
// la toute premiere invitation, qui cree le groupe avec ce mode. Dans ce cas
// on utilise le reglage "Butin de groupe par defaut" (SettingsWindow).
public class PartyInviteAction : L2Action
{
    public PartyInviteAction() : base() { }

    public override void UseAction()
    {
        if (!TargetManager.Instance.HasTarget()) return;

        Entity target = TargetManager.Instance.Target;
        if (target == PlayerEntity.Instance) return;

        int lootRuleId = PartyManager.Instance.IsInParty ? (int)PartyManager.Instance.LootRule : GameSettings.PreferredPartyLootRule;
        GameClient.Instance.ClientPacketHandler.SendRequestJoinParty(target.Identity.Name, lootRuleId);
    }
}
