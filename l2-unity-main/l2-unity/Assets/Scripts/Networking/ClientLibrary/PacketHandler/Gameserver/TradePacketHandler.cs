// Paquets de l'echange direct, gardes a part de GameServerPacketHandler.
public static class TradePacketHandler
{
    private const float RequestTimeoutSeconds = 13f;

    public static void Handle(GameServerPacketType type, byte[] data, EventProcessor eventProcessor)
    {
        switch (type)
        {
            case GameServerPacketType.SendTradeRequest:
            {
                SendTradeRequestPacket packet = new SendTradeRequestPacket(data);
                eventProcessor.QueueEvent(() => AskTrade(packet.SenderId));
                break;
            }
            case GameServerPacketType.TradeStart:
            {
                TradeStartPacket packet = new TradeStartPacket(data);
                eventProcessor.QueueEvent(() => TradeWindow.Instance?.Open(packet.PartnerId, packet.Items));
                break;
            }
            case GameServerPacketType.TradeOwnAdd:
            {
                TradeAddPacket packet = new TradeAddPacket(data);
                eventProcessor.QueueEvent(() => TradeWindow.Instance?.AddOwn(packet.Item));
                break;
            }
            case GameServerPacketType.TradeOtherAdd:
            {
                TradeAddPacket packet = new TradeAddPacket(data);
                eventProcessor.QueueEvent(() => TradeWindow.Instance?.AddOther(packet.Item));
                break;
            }
            case GameServerPacketType.TradeUpdate:
            {
                TradeUpdatePacket packet = new TradeUpdatePacket(data);
                eventProcessor.QueueEvent(() => TradeWindow.Instance?.UpdateInventory(packet.Items));
                break;
            }
            case GameServerPacketType.TradePressOwnOk:
                eventProcessor.QueueEvent(() => TradeWindow.Instance?.OnOwnConfirmed());
                break;
            case GameServerPacketType.TradePressOtherOk:
                eventProcessor.QueueEvent(() => TradeWindow.Instance?.OnOtherConfirmed());
                break;
            case GameServerPacketType.SendTradeDone:
                eventProcessor.QueueEvent(() => TradeWindow.Instance?.Close());
                break;
        }
    }

    // Jauge a 13 s, deliberement sous les 15 s du serveur (Player.REQUEST_TIMEOUT) :
    // passe ce delai il oublie la demande et repondrait "ce joueur n'est pas
    // connecte" au lieu de prevenir le demandeur du refus.
    // Sans reponse avant la fin de la jauge, la demande est refusee.
    private static void AskTrade(int senderId)
    {
        string name = WorldSpawner.Instance.TryGetEntity(senderId, out Entity sender) ? sender.Identity.Name : "?";

        L2ConfirmWindow.Instance.ShowWindow(
            $"{name} vous propose un \u00e9change. Acceptez-vous ?",
            () => GameClient.Instance.ClientPacketHandler.SendAnswerTradeRequest(true),
            () => GameClient.Instance.ClientPacketHandler.SendAnswerTradeRequest(false),
            RequestTimeoutSeconds);
    }
}
