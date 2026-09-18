// Paquets du clan ajoutes apres coup, gardes a part de GameServerPacketHandler.
public static class ClanPacketHandler
{
    // Le nameplate relit le titre de l'identite a chaque image.
    public static void OnTitleUpdate(byte[] data)
    {
        TitleUpdatePacket packet = new TitleUpdatePacket(data);
        _ = WorldSpawner.Instance.ExecuteWithEntityAsync(packet.ObjectId, e => e.Identity.Title = packet.Title);
    }

    public static void OnSubPledgeCreated(byte[] data, EventProcessor eventProcessor)
    {
        PledgeReceiveSubPledgeCreatedPacket packet = new PledgeReceiveSubPledgeCreatedPacket(data);
        eventProcessor.QueueEvent(() => ClanData.SetUnit(packet.PledgeType, packet.Name, packet.LeaderName));
    }

    public static void OnSkillList(byte[] data, EventProcessor eventProcessor)
    {
        PledgeSkillListPacket packet = new PledgeSkillListPacket(data);
        eventProcessor.QueueEvent(() => ClanData.SetSkills(packet.Skills, packet.IsAddition));
    }

    public static void OnAllyCrest(byte[] data, EventProcessor eventProcessor)
    {
        // Meme format que le blason de clan : identifiant, longueur, octets.
        PledgeCrestPacket packet = new PledgeCrestPacket(data);
        if (packet.Data != null)
        {
            eventProcessor.QueueEvent(() => ClanCrests.SetAlly(packet.CrestId, packet.Data));
        }
    }

    public static void OnCrestLarge(byte[] data, EventProcessor eventProcessor)
    {
        ExPledgeCrestLargePacket packet = new ExPledgeCrestLargePacket(data);
        if (packet.Data != null)
        {
            eventProcessor.QueueEvent(() => ClanCrests.SetLarge(packet.CrestId, packet.Data));
        }
    }

    public static void OnClanCard(byte[] data, EventProcessor eventProcessor)
    {
        ExClanCardPacket packet = new ExClanCardPacket(data);
        eventProcessor.QueueEvent(() => ClanCards.Set(packet.Card));
    }
}
