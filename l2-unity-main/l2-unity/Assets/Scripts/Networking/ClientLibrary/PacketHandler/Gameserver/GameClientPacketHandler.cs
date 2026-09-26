using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class GameClientPacketHandler : ClientPacketHandler
{
    protected override void EncryptPacket(ClientPacket packet)
    {
        byte[] data = packet.GetData();

        GameClient.Instance.GameCrypt.Encrypt(data);

        if (GameClient.Instance.LogCryptography)
        {
            Debug.Log("----> [GAME] ENCRYPTED: " + StringUtils.ByteArrayToString(data));
        }

        packet.SetData(data);
    }

    public void SendPing()
    {
        PingPacket packet = new PingPacket();
        SendPacket(packet);
    }

    public void SendProtocolVersion()
    {
        ProtocolVersionPacket packet = new ProtocolVersionPacket(GameManager.Instance.ProtocolVersion);
        SendPacket(packet);
    }

    public void SendAuth()
    {
        GameAuthRequestPacket authPacket =
            new GameAuthRequestPacket(LoginClient.Instance.Account, GameClient.Instance.PlayKey1,
                GameClient.Instance.PlayKey2,
                GameClient.Instance.SessionKey1, GameClient.Instance.SessionKey2);


        SendPacket(authPacket);
    }

    public void SendMessage(string message, L2MessageType messageType, string pmTarget)
    {
        SendMessagePacket packet = new SendMessagePacket(message, messageType, pmTarget);
        SendPacket(packet);
    }

    public void ValidatePosition(Vector3 position, int heading)
    {
        ValidatePositionPacket packet = new ValidatePositionPacket(position, heading);
        SendPacket(packet);
    }

    public void SendLoadWorld()
    {
        EnterWorldPacket packet = new EnterWorldPacket();
        SendPacket(packet);
    }

    public void UpdateRotation(float angle)
    {
        RequestRotatePacket packet = new RequestRotatePacket(angle);
        SendPacket(packet);
    }

    public void UpdateAnimation(byte anim, float value)
    {
        RequestAnimPacket packet = new RequestAnimPacket(anim, value);
        SendPacket(packet);
    }

    public void RequestAttackForce(int targetId)
    {
        RequestAttackPacket packet = new RequestAttackPacket(targetId);
        SendPacket(packet);
    }

    public void UpdateMoveDirection(Vector3 direction, int heading, float verticalVelocity, Vector3 position, bool requireReply)
    {
        // Debug.LogWarning("Sharing move direction: " + direction);
        RequestMoveDirectionPacket packet = new RequestMoveDirectionPacket(direction, heading, verticalVelocity, position, requireReply);
        SendPacket(packet);
    }

    public void SendRequestSetTarget(int targetId)
    {
        RequestSetTargetPacket packet = new RequestSetTargetPacket(targetId, false);
        SendPacket(packet);
    }

    public void SendRequestCancel(bool cancelCast)
    {
        RequestCancelPacket packet = new RequestCancelPacket(cancelCast);
        SendPacket(packet);
    }

    public void SendRequestAction(int objectId)
    {
        RequestActionPacket packet = new RequestActionPacket(objectId);
        SendPacket(packet);
    }

    public void SendRequestLockpick(int doorObjectId, int action)
    {
        SendPacket(new RequestLockpickPacket(doorObjectId, action));
    }

    public void SendDlgAnswer(int messageId, int answer, int requesterId)
    {
        SendPacket(new DlgAnswerPacket(messageId, answer, requesterId));
    }

    public void SendRequestJoinParty(string targetName, int lootRuleId)
    {
        RequestJoinPartyPacket packet = new RequestJoinPartyPacket(targetName, lootRuleId);
        SendPacket(packet);
    }

    public void SendRequestAnswerJoinParty(bool accept)
    {
        RequestAnswerJoinPartyPacket packet = new RequestAnswerJoinPartyPacket(accept);
        SendPacket(packet);
    }

    public void SendRequestWithdrawParty()
    {
        RequestWithdrawPartyPacket packet = new RequestWithdrawPartyPacket();
        SendPacket(packet);
    }

    public void SendRequestOustPartyMember(string targetName)
    {
        RequestOustPartyMemberPacket packet = new RequestOustPartyMemberPacket(targetName);
        SendPacket(packet);
    }

    public void SendRequestEnchantItem(int objectId)
    {
        RequestEnchantItemPacket packet = new RequestEnchantItemPacket(objectId);
        SendPacket(packet);
    }

    public void SendRequestJoinPledge(int targetId, int pledgeType)
    {
        SendPacket(new RequestJoinPledgePacket(targetId, pledgeType));
    }

    public void SendAnswerJoinPledge(bool accept)
    {
        SendPacket(new RequestAnswerJoinPledgePacket(accept));
    }

    public void SendWithdrawPledge()
    {
        SendPacket(new RequestWithdrawPledgePacket());
    }

    public void SendDismissPledge()
    {
        SendPacket(new RequestDismissPledgePacket());
    }

    public void SendOustPledgeMember(string name)
    {
        SendPacket(new RequestOustPledgeMemberPacket(name));
    }

    public void SendGiveNickName(string name, string title)
    {
        SendPacket(new RequestGiveNickNamePacket(name, title));
    }

    public void SendJoinAlly(int targetId)
    {
        SendPacket(new AllyTargetPacket(GameClientPacketType.RequestJoinAlly, targetId));
    }

    public void SendAnswerJoinAlly(bool accept)
    {
        SendPacket(new AllyTargetPacket(GameClientPacketType.RequestAnswerJoinAlly, accept ? 1 : 0));
    }

    public void SendAllyLeave()
    {
        SendPacket(new AllyRequestPacket(GameClientPacketType.AllyLeave));
    }

    public void SendAllyDismiss(string clanName)
    {
        SendPacket(new AllyDismissPacket(clanName));
    }

    public void SendDismissAlly()
    {
        SendPacket(new AllyRequestPacket(GameClientPacketType.RequestDismissAlly));
    }

    public void SendRequestAllyInfo()
    {
        SendPacket(new AllyRequestPacket(GameClientPacketType.RequestAllyInfo));
    }

    public void SendStartPledgeWar(string clanName)
    {
        SendPacket(new RequestPledgeWarPacket(GameClientPacketType.RequestStartPledgeWar, clanName));
    }

    public void SendStopPledgeWar(string clanName)
    {
        SendPacket(new RequestPledgeWarPacket(GameClientPacketType.RequestStopPledgeWar, clanName));
    }

    public void SendSurrenderPledgeWar(string clanName)
    {
        SendPacket(new RequestPledgeWarPacket(GameClientPacketType.RequestSurrenderPledgeWar, clanName));
    }

    public void SendRequestPledgeWarList(int page, int tab)
    {
        SendPacket(new RequestPledgeWarListPacket(page, tab));
    }

    public void SendRequestPledgePower(int rank, int action, int privileges)
    {
        SendPacket(new RequestPledgePowerPacket(rank, action, privileges));
    }

    public void SendRequestPledgeMemberInfo(int pledgeType, string name)
    {
        SendPacket(new RequestPledgeMemberInfoPacket(pledgeType, name));
    }

    public void SendPledgeReorganizeMember(string member, int newPledgeType, string swapWith)
    {
        SendPacket(new RequestPledgeReorganizeMemberPacket(member, newPledgeType, swapWith));
    }

    public void SendPledgeSetAcademyMaster(bool set, string member, string other)
    {
        SendPacket(new RequestPledgeSetAcademyMasterPacket(set, member, other));
    }

    public void SendSetMemberPowerGrade(string name, int powerGrade)
    {
        SendPacket(new RequestPledgeSetMemberPowerGradePacket(name, powerGrade));
    }

    public void SendRequestPledgePowerGradeList()
    {
        SendPacket(new RequestPledgePowerGradeListPacket());
    }

    public void SendSetPledgeCrest(byte[] image)
    {
        SendPacket(new RequestSetPledgeCrestPacket(image));
    }

    public void SendRequestPledgeCrest(int crestId)
    {
        SendPacket(new RequestPledgeCrestPacket(crestId));
    }

    public void SendRequestAllyCrest(int crestId)
    {
        SendPacket(new RequestAllyCrestPacket(crestId));
    }

    public void SendSetAllyCrest(byte[] image)
    {
        SendPacket(new RequestSetAllyCrestPacket(image));
    }

    public void SendRequestPledgeCrestLarge(int crestId)
    {
        SendPacket(new RequestExPledgeCrestLargePacket(crestId));
    }

    public void SendSetPledgeCrestLarge(byte[] image)
    {
        SendPacket(new RequestExSetPledgeCrestLargePacket(image));
    }

    public void SendRequestClanCard(int clanId)
    {
        SendPacket(new RequestClanCardPacket(clanId));
    }

    public void SendRequestPledgeInfo(int clanId)
    {
        SendPacket(new RequestPledgeInfoPacket(clanId));
    }

    public void SendRequestPledgeMemberList()
    {
        SendPacket(new RequestPledgeMemberListPacket());
    }

    public void SendPartyMarker(int x, int y, int z)
    {
        RequestPartyMarkerPacket packet = new RequestPartyMarkerPacket(x, y, z);
        SendPacket(packet);
    }

    public void SendRequestChangePartyLeader(string targetName)
    {
        RequestChangePartyLeaderPacket packet = new RequestChangePartyLeaderPacket(targetName);
        SendPacket(packet);
    }

    public void SendRequestSelectCharacter(int slot)
    {
        RequestCharSelectPacket packet = new RequestCharSelectPacket(slot);
        SendPacket(packet);
    }

    public void SendRequestOpenInventory()
    {
        RequestInventoryOpenPacket packet = new RequestInventoryOpenPacket();
        SendPacket(packet);
    }

    public override void SendPacket(ClientPacket packet)
    {
        if (GameClient.Instance.LogSentPackets)
        {
            GameClientPacketType packetType = (GameClientPacketType)packet.GetPacketType();
            Debug.Log("[" + Thread.CurrentThread.ManagedThreadId + "] [GameServer] Sending packet:" + packetType);
        }

        if (GameClient.Instance.LogCryptography)
        {
            Debug.Log("----> [GAME] CLEAR: " + StringUtils.ByteArrayToString(packet.GetData()));
        }

        if (_client.CryptEnabled)
        {
            EncryptPacket(packet);
        }

        _client.SendPacket(packet);
    }

    public void UseItem(int objectId)
    {
        UseItemPacket packet = new UseItemPacket(objectId);
        SendPacket(packet);
    }

    public void UpdateInventoryOrder(List<InventoryOrder> orders)
    {
        RequestInventoryUpdateOrderPacket packet = new RequestInventoryUpdateOrderPacket(orders);
        SendPacket(packet);
    }

    public void DestroyItem(int objectId, int quantity)
    {
        RequestDestroyItemPacket packet = new RequestDestroyItemPacket(objectId, quantity);
        SendPacket(packet);
    }

    public void DropItem(int objectId, int quantity, Vector3 position)
    {
        RequestDropItemPacket packet = new RequestDropItemPacket(objectId, quantity, position);
        SendPacket(packet);
    }

    public void RequestDisconnect()
    {
        DisconnectPacket packet = new DisconnectPacket();
        SendPacket(packet);
    }

    public void RequestRestart()
    {
        RequestRestartPacket packet = new RequestRestartPacket();
        SendPacket(packet);
    }

    public void RequestAddShortcut(int type, int id, int slot)
    {
        RequestShortcutRegPacket packet = new RequestShortcutRegPacket(type, id, slot);
        SendPacket(packet);
    }

    public void RequestRemoveShortcut(int oldSlot)
    {
        RequestShortcutDelPacket packet = new RequestShortcutDelPacket(oldSlot);
        SendPacket(packet);
    }

    public void RequestActionUse(int actionId)
    {
        bool isControlPressed = false;
        bool isShiftPressed = false;
        RequestActionUsePacket packet = new RequestActionUsePacket(actionId, isControlPressed, isShiftPressed);
        SendPacket(packet);
    }

    public void SendRequestCreateCharacter(string name, CharacterRace race, CharacterSex sex, CharacterClass clazz,
        int hairstyle, int haircolor, int face)
    {
        RequestCharCreatePacket packet =
            new RequestCharCreatePacket(name, race, sex, clazz, hairstyle, haircolor, face);
        SendPacket(packet);
    }

    public void SendRequestRestartPoint(int restartPoint)
    {
        RequestRestartPointPacket packet = new RequestRestartPointPacket(restartPoint);
        SendPacket(packet);
    }

    public void NotifyAppearing()
    {
        AppearingPacket packet = new AppearingPacket();
        SendPacket(packet);
    }

    public void RequestBypassToServer(string htmlCommand)
    {
        RequestBypassToServerPacket packet = new RequestBypassToServerPacket(htmlCommand);
        SendPacket(packet);
    }

    public void RequestAutoSoulshot(int id, bool toggled)
    {
        RequestAutoSoulshotPacket packet = new RequestAutoSoulshotPacket(id, !toggled);
        SendPacket(packet);
    }

    public void SendGMCommand(string command)
    {
        GMCommandPacket packet = new GMCommandPacket(command);
        SendPacket(packet);
    }

    public void SendRequestDeleteCharacter(int slot)
    {
        RequestCharDeletePacket packet = new RequestCharDeletePacket(slot);
        SendPacket(packet);
    }

    public void SendRequestRestoreCharacter(int slot)
    {
        RequestCharRestorePacket packet = new RequestCharRestorePacket(slot);
        SendPacket(packet);
    }

    public void SendRequestSkillList()
    {
        RequestSkillListPacket packet = new RequestSkillListPacket();
        SendPacket(packet);
    }

    public void SendRequestAcquireSkill(int skillId, int skillLvl, PacketSkillType skillType)
    {
        RequestAcquireSkillPacket packet = new RequestAcquireSkillPacket(skillId, skillLvl, skillType);
        SendPacket(packet);
    }

    public void SendRequestAcquireSkillInfo(int skillId, int skillLvl, PacketSkillType skillType)
    {
        RequestAcquireSkillInfoPacket packet = new RequestAcquireSkillInfoPacket(skillId, skillLvl, skillType);
        SendPacket(packet);
    }

    public void SendRequestSocialAction(int actionId)
    {
        SendPacket(new RequestSocialActionPacket(actionId));
    }

    public void SendRequestCoupleAction(int targetId, int actionId)
    {
        SendPacket(new RequestCoupleActionPacket(targetId, actionId));
    }

    public void SendRequestSellItem(int listId, List<Product> products)
    {
        RequestSellItemPacket packet = new RequestSellItemPacket(listId, products);
        SendPacket(packet);
    }

    public void SendRequestBuyItem(int listId, List<Product> products)
    {
        RequestBuyItemPacket packet = new RequestBuyItemPacket(listId, products);
        SendPacket(packet);
    }

    public void SendSetPrivateStoreMsgSell(string title)
    {
        SendPacket(new SetPrivateStoreMsgSellPacket(title));
    }

    public void SendSetPrivateStoreListSell(bool packageSale, List<Product> products)
    {
        SendPacket(new SetPrivateStoreListSellPacket(packageSale, products));
    }

    public void SendRequestPrivateStoreQuitSell()
    {
        SendPacket(new RequestPrivateStoreQuitSellPacket());
    }

    public void SendRequestPrivateStoreBuy(int storeObjectId, List<Product> products)
    {
        SendPacket(new RequestPrivateStoreBuyPacket(storeObjectId, products));
    }

    public void SendWarehouseList(bool deposit, List<Product> products)
    {
        SendPacket(new SendWarehouseListPacket(deposit, products));
    }

    public void SendRequestPackageSendableItemList(int targetId)
    {
        SendPacket(new RequestPackageSendableItemListPacket(targetId));
    }

    public void SendRequestPackageSend(int targetId, List<Product> products)
    {
        SendPacket(new RequestPackageSendPacket(targetId, products));
    }

    public void SendSetPrivateStoreMsgBuy(string title)
    {
        SendPacket(new SetPrivateStoreMsgBuyPacket(title));
    }

    public void SendSetPrivateStoreListBuy(List<Product> products)
    {
        SendPacket(new SetPrivateStoreListBuyPacket(products));
    }

    public void SendRequestPrivateStoreQuitBuy()
    {
        SendPacket(new RequestPrivateStoreQuitBuyPacket());
    }

    public void SendRequestPrivateStoreSell(int storeObjectId, List<Product> products)
    {
        SendPacket(new RequestPrivateStoreSellPacket(storeObjectId, products));
    }

    public void SendUserCommand(int commandId)
    {
        SendPacket(new UserCommandPacket(commandId));
    }

    public void SendTradeRequest(int targetId)
    {
        SendPacket(new TradeRequestPacket(targetId));
    }

    public void SendAnswerTradeRequest(bool accept)
    {
        SendPacket(new AnswerTradeRequestPacket(accept));
    }

    public void SendAddTradeItem(int objectId, int count)
    {
        SendPacket(new AddTradeItemPacket(objectId, count));
    }

    public void SendTradeDone(bool confirm)
    {
        SendPacket(new TradeDonePacket(confirm));
    }

    public void RequestMagicSkillUse(int skillId, bool ctrlPressed, bool shiftPressed)
    {
        RequestMagicSkillUsePacket packet = new RequestMagicSkillUsePacket(skillId, ctrlPressed, shiftPressed);
        SendPacket(packet);
    }
}