package com.shnok.javaserver.dto.internal.loginserverpackets;

import com.shnok.javaserver.dto.SendablePacket;
import com.shnok.javaserver.enums.packettypes.internal.LoginServerPacketType;

public class AuthResponsePacket extends SendablePacket {
    public AuthResponsePacket(int id, String name) {
        super(LoginServerPacketType.AuthResponse.getValue());
        writeB((byte) id);
        writeS(name);
        buildPacket();
    }
}