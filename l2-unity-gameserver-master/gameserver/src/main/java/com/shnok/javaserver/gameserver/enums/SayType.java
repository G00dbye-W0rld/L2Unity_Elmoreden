package com.shnok.javaserver.gameserver.enums;

public enum SayType
{
	ROLE_PLAY, // Canal blanc, dedie au Role Play (l'ancien "All")
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
	// Deux canaux morts de l'epoque (chat d'amis, passerelle MSN) : la place
	// est gardee car sa POSITION est le numero qui circule sur le reseau.
	UNUSED_L2FRIEND,
	UNUSED_MSNCHAT,
	PARTYMATCH_ROOM,
	PARTYROOM_COMMANDER, // (Yellow)
	PARTYROOM_ALL, // (Red)
	HERO_VOICE,
	CRITICAL_ANNOUNCE,
	// Hors Role Play, portee locale : le client l'envoie quand le message est
	// ecrit entre parentheses. Ajoute EN FIN : la position est le numero
	// echange avec le client.
	HRP;
	
	public static final SayType[] VALUES = values();
}