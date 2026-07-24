package com.shnok.javaserver.dto.internal.gameserverpackets;

import com.shnok.javaserver.dto.ReceivablePacket;
import lombok.Getter;

@Getter
public class ChangeAccessLevelPacket extends ReceivablePacket {
    private final int accessLevel;
    private final String account;

    public ChangeAccessLevelPacket(byte[] data) {
        super(data);

        accessLevel = readI();
        account = readS();
    }
}
