using System;

public abstract class LoginServerPacket : ServerPacket
{
    public LoginServerPacket(byte[] d) : base(d)
    {
    }

    // ReadS() is intentionally not overridden here anymore - the base
    // ServerPacket.ReadS() (UTF-16LE, char-by-char until null terminator,
    // matching the Java side's writeS()) is correct and already proven
    // working for the gameserver protocol. This class's own override had a
    // dual bug (copied from the post-advance iterator instead of the
    // string's actual start, and decoded as UTF-8 instead of UTF-16) that
    // went unnoticed because no loginserver packet ever called ReadS()
    // until ServerListPacket's new per-server name field.

    protected override int ReadI()
    {
        byte[] data = new byte[4];
        Array.Copy(_packetData, _iterator, data, 0, 4);
        // Array.Reverse(data);
        int value = BitConverter.ToInt32(data, 0);
        _iterator += 4;
        return value;
    }

    protected override long ReadL()
    {
        byte[] data = new byte[8];
        Array.Copy(_packetData, _iterator, data, 0, 8);
        // Array.Reverse(data);
        long value = BitConverter.ToInt64(data, 0);
        _iterator += 8;
        return value;
    }

    protected override float ReadF()
    {
        byte[] data = new byte[4];
        Array.Copy(_packetData, _iterator, data, 0, 4);
        // Array.Reverse(data);
        float value = BitConverter.ToSingle(data, 0);
        _iterator += 4;
        return value;
    }
}
