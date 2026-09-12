public enum L2MessageType : int
{
    ROLE_PLAY, // Canal blanc, dedie au Role Play
    SHOUT, // !
    TELL, // "
    PARTY, // #
    CLAN, // @
    GM,
    PETITION_PLAYER,
    PETITION_GM,
    TRADE, // +
    ALLIANCE, // $
    ANNOUNCEMENT,
    BOAT,
    // Canaux morts (chat d'amis, passerelle MSN) : la place est gardee car sa
    // POSITION est le numero echange avec le serveur.
    UNUSED_L2FRIEND,
    UNUSED_MSNCHAT,
    PARTYMATCH_ROOM,
    PARTYROOM_COMMANDER, // (Yellow)
    PARTYROOM_ALL, // (Red)
    HERO_VOICE,
    CRITICAL_ANNOUNCE,
    HRP, // hors Role Play, local, ecrit entre parentheses
    SYSTEM_MESSAGE
}
