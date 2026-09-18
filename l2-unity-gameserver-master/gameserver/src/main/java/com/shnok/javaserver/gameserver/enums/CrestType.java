package com.shnok.javaserver.gameserver.enums;

/**
 * Les tailles sont des MAXIMA, pas des tailles exactes : le serveur ne
 * regarde jamais le contenu d'un blason, il le stocke et le rend tel quel.
 * Les 256 / 2176 / 192 octets d'origine correspondaient a des DDS DXT1 de
 * taille figee ; ces marges laissent passer du PNG, plus souple et plus fin.
 */
public enum CrestType
{
	PLEDGE("Crest_", 8192),
	PLEDGE_LARGE("LargeCrest_", 65536),
	ALLY("AllyCrest_", 8192);
	
	private final String _prefix;
	private final int _maxSize;
	
	private CrestType(String prefix, int maxSize)
	{
		_prefix = prefix;
		_maxSize = maxSize;
	}
	
	public final String getPrefix()
	{
		return _prefix;
	}
	
	public final int getSize()
	{
		return _maxSize;
	}
}