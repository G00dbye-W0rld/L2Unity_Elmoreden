public class AccountKickedPacket : LoginServerPacket
{
    public enum AccountKickedReason : byte
    {
        REASON_DATA_STEALER = 0x01,
        REASON_GENERIC_VIOLATION = 0x08,
        REASON_7_DAYS_SUSPENDED = 0x10,
        REASON_PERMANENTLY_BANNED = 0x20
    }

    private AccountKickedReason _kickedReason;
    public AccountKickedReason KickedReason { get { return _kickedReason; } }

    private string _reason;
    public string Reason { get { return _reason; } }

    // Millisecondes epoch, 0 = permanent.
    private long _expireDate;
    public long ExpireDate { get { return _expireDate; } }

    public AccountKickedPacket(byte[] d) : base(d)
    {
        Parse();
    }

    public override void Parse()
    {
        _kickedReason = (AccountKickedReason)ReadB();
        _reason = ReadS();
        _expireDate = ReadL();
    }
}