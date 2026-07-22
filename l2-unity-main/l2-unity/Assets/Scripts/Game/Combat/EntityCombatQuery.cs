// Detecte si une entite (PNJ/monstre) est en train d'attaquer/cibler le
// joueur local. Verifie DEUX signaux possibles, selon lequel est
// effectivement tenu a jour dans ce projet :
//  - Combat.TargetId : mis a jour par EntityTargetSetPacket/UnsetPacket
//    (signal serveur temps reel, cf. WorldCombat.UpdateEntityTarget/
//    UnsetEntityTarget).
//  - Combat.Target : mis a jour directement par WorldCombat.EntityAttacks
//    des qu'un coup de cette entite sur le joueur est traite (meme si
//    aucun packet de cible dedie n'a ete envoye pour cette paire).
// Utilise a la fois par le highlight (anneaux au sol, ClickManager) et par
// l'icone de nameplate (NameplatesManagerGame) pour eviter de dupliquer la
// logique et de ne verifier qu'un seul des deux signaux.
public static class EntityCombatQuery
{
    public static bool IsAttackingPlayer(Entity entity)
    {
        if (entity == null || entity.Combat == null) return false;
        if (GameClient.Instance == null) return false;

        int playerId = GameClient.Instance.CurrentPlayerId;

        if (entity.Combat.TargetId == playerId) return true;
        if (entity.Combat.Target != null && entity.Combat.Target.Identity.Id == playerId) return true;

        return false;
    }
}
