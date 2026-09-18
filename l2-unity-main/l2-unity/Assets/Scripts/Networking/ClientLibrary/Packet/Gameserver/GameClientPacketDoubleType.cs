public enum GameClientPacketDoubleType : byte
{
    RequestChangePartyLeader = 0x04,
    RequestAutoSoulshot = 0x05,
    RequestExPledgeCrestLarge = 0x10,
    RequestExSetPledgeCrestLarge = 0x11,
    RequestPledgeSetAcademyMaster = 0x19,
    RequestPledgePowerGradeList = 0x1a,
    RequestPledgeSetMemberPowerGrade = 0x1c,
    RequestPledgeMemberInfo = 0x1d,
    RequestPledgeWarList = 0x1e,
    RequestPledgeReorganizeMember = 0x24,
    RequestClanCard = 0x3a,
    RequestPartyMarker = 0x40,
}
