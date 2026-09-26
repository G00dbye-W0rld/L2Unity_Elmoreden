package com.shnok.javaserver.gameserver.network.clientpackets.movement;

import com.shnok.javaserver.gameserver.model.actor.Player;
import com.shnok.javaserver.gameserver.model.location.Location;
import com.shnok.javaserver.gameserver.network.SystemMessageId;
import com.shnok.javaserver.gameserver.network.clientpackets.L2GameClientPacket;
import com.shnok.javaserver.gameserver.network.serverpackets.combat.ActionFailed;

public class PlayerMoveDirection extends L2GameClientPacket
{
    private double _moveDirectionX;
    private double _moveDirectionY;
    private double _moveDirectionZ;
    @SuppressWarnings("unused")
    private int _heading;

    private double _verticalVelocity;

    private int _x;
    private int _y;
    private int _z;
    private long _timestamp;
    private boolean _requireResponse;

    @Override
    protected void readImpl()
    {
        _requireResponse = readC() == 1;
        _moveDirectionY = readF();
        _moveDirectionZ = 0;
        _moveDirectionX = readF();
        _heading = readD();
        _verticalVelocity = readF();
        _x = readD();
        _y = readD();
        _z = readD();
        _timestamp = readQ();
    }

    @Override
    protected void runImpl()
    {
        final Player player = getClient().getPlayer();
        if (player == null)
            return;

        // If Player can't be controlled, forget it.
        if (player.isOutOfControl() && _requireResponse)
        {
            player.sendPacket(ActionFailed.STATIC_PACKET); //validatelocation?
            return;
        }


        // If Player can't move, forget it.
        if (player.getStatus().getMoveSpeed() == 0 && _requireResponse)
        {
            player.sendPacket(ActionFailed.STATIC_PACKET); //validatelocation?
            player.sendPacket(SystemMessageId.CANT_MOVE_TOO_ENCUMBERED); //validatelocation?
            return;
        }
        player._verticalVelocity = _verticalVelocity;
        player._lastGamePosition = new Location(_x, _y ,_z);
        player._lastPacketTimestamp = _timestamp;
        // Cancel enchant over movement.
        player.cancelActiveEnchant();

        // Generate a Location based on target coords.
        final Location moveDirection = new Location((int)(_moveDirectionX * 100), (int)(_moveDirectionY * 100), (int)(_moveDirectionZ * 100));

        player.getAI().tryToMoveTo(moveDirection, null, _requireResponse);

        // Le client pilote le deplacement par direction, pas par destination : il n'y a donc
        // jamais d'arrivee, et une direction nulle est sa facon de dire "je m'arrete". Sans
        // ce retour au repos l'IA restait en MOVE_TO indefiniment, et tout ce qui exige un
        // personnage au repos (les emotes, par exemple) etait refuse en silence.
        // Le passage par tryToMoveTo est garde : c'est lui qui diffuse l'arret aux autres
        // joueurs, sans quoi leur client continue d'extrapoler le dernier deplacement.
        if (moveDirection.getX() == 0 && moveDirection.getY() == 0 && moveDirection.getZ() == 0)
            player.getAI().tryToIdle();
    }
}
