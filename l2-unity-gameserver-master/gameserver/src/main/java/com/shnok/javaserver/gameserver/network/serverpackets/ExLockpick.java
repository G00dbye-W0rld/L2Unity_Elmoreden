package com.shnok.javaserver.gameserver.network.serverpackets;

/**
 * Paquet du projet : etat d'un crochetage de porte de salle de clan (fenetre du jet de de).
 */
public class ExLockpick extends L2GameServerPacket
{
	private final int _doorObjectId;
	private final String _hallName;
	private final int _state;
	private final int _pins;
	private final int _pinsTotal;
	private final int _difficulty;
	private final int _bonus;
	private final int _die;
	private final int _result;
	private final int _lockpicks;

	public ExLockpick(int doorObjectId, String hallName, int state, int pins, int pinsTotal, int difficulty, int bonus, int die, int result, int lockpicks)
	{
		_doorObjectId = doorObjectId;
		_hallName = hallName;
		_state = state;
		_pins = pins;
		_pinsTotal = pinsTotal;
		_difficulty = difficulty;
		_bonus = bonus;
		_die = die;
		_result = result;
		_lockpicks = lockpicks;
	}

	@Override
	protected void writeImpl()
	{
		writeC(0xfe);
		writeH(0x5e);
		writeD(_doorObjectId);
		writeS(_hallName);
		writeD(_state);
		writeD(_pins);
		writeD(_pinsTotal);
		writeD(_difficulty);
		writeD(_bonus);
		writeD(_die);
		writeD(_result);
		writeD(_lockpicks);
	}
}
