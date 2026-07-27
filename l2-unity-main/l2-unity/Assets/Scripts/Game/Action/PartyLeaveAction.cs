public class PartyLeaveAction : L2Action
{
    public PartyLeaveAction() : base() { }

    public override void UseAction()
    {
        if (!PartyManager.Instance.IsInParty) return;

        GameClient.Instance.ClientPacketHandler.SendRequestWithdrawParty();
    }
}
