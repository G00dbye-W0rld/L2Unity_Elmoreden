package com.shnok.javaserver.dto.external.serverpackets;

import com.shnok.javaserver.dto.SendablePacket;
import com.shnok.javaserver.enums.AccountKickedReason;
import com.shnok.javaserver.enums.packettypes.external.ServerPacketType;

public class AccountKickedPacket extends SendablePacket {
    public AccountKickedPacket(AccountKickedReason kickedReason) {
        this(kickedReason, "", 0);
    }

    // reason/expireDate (raison du ban + date de fin, 0 = permanent) : uniquement
    // pertinents pour REASON_PERMANENTLY_BANNED, mais toujours ecrits pour garder
    // un format de paquet uniforme cote client quel que soit le motif du kick.
    public AccountKickedPacket(AccountKickedReason kickedReason, String reason, long expireDate) {
        super(ServerPacketType.AccountKicked.getValue());
        writeB(kickedReason.getCode());
        writeS(reason == null ? "" : reason);
        writeL(expireDate);

        buildPacket();
    }
}
