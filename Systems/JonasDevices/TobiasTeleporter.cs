using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent;

[ProtoContract]
public class PlayerLocationData
{
    [ProtoMember(1)]
    public Vec3d Position;

    [ProtoMember(2)]
    public double TotalDaysSinceLastTeleport;
}

[ProtoContract]
public class TobiasTeleporterData
{
    [ProtoMember(1)]
    public Vec3d TobiasTeleporterLocation { get; set; }

    [ProtoMember(2)]
    public Dictionary<string, PlayerLocationData> PlayerLocations = new Dictionary<string, PlayerLocationData>();
}

[ProtoContract]
public class TobiasLastUsage
{
    [ProtoMember(1)]
    public double LastUsage { get; set; }
}

public class TobiasTeleporter : ModSystem
{
    ICoreServerAPI sapi;
    ICoreClientAPI capi;
    public TobiasTeleporterData TeleporterData = new TobiasTeleporterData();

    private bool needsSaving;
    private IClientNetworkChannel clientChannel;
    private IServerNetworkChannel serverChannel;
    public double OwnLastUsage;

    public int TpCooldownInMonths { get; set; } = 2;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        clientChannel = api.Network.RegisterChannel("tobiasteleporter");
        clientChannel.RegisterMessageType(typeof(TobiasLastUsage));
        clientChannel.SetMessageHandler<TobiasLastUsage>(OnLastUsage);
    }

    private void OnLastUsage(TobiasLastUsage lastUsage)
    {
        OwnLastUsage = lastUsage.LastUsage;
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        sapi.Event.SaveGameLoaded += Event_SaveGameLoaded;
        sapi.Event.GameWorldSave += Event_GameWorldSave;
        sapi.Event.PlayerJoin += OnPlayerJoin;

        serverChannel = api.Network.RegisterChannel("tobiasteleporter");
        serverChannel.RegisterMessageType(typeof(TobiasLastUsage));

        var parsers = sapi.ChatCommands.Parsers;
        sapi.ChatCommands.GetOrCreate("dev")
            .BeginSubCommand("tobias-teleporter")
            .WithAlias("tobt")
            .WithDescription("Set tobias teleporter at the specified location. Only one per world can exist.")
            .RequiresPlayer()
            .WithArgs(parsers.WorldPosition("position"))
            .HandleWith(OnSetTp)
            .EndSubCommand();
    }

    private void OnPlayerJoin(IServerPlayer byplayer)
    {
        SendLastUsageToPlayer(byplayer);
    }

    private void SendLastUsageToPlayer(IServerPlayer byplayer)
    {
        if (TeleporterData.PlayerLocations.TryGetValue(byplayer.PlayerUID, out var playerLocation))
        {
            var message = new TobiasLastUsage() { LastUsage = playerLocation.TotalDaysSinceLastTeleport };
            serverChannel.SendPacket(message, byplayer);
        }
    }

    private TextCommandResult OnSetTp(TextCommandCallingArgs args)
    {
        var posVec = (args[0] as Vec3d);
        var pos = posVec.AsBlockPos;

        var block = sapi.World.BlockAccessor.GetBlock(pos);
        var bett = block.GetBlockEntity<BlockEntityTobiasTeleporter>(pos);
        if (bett == null)
        {
            return TextCommandResult.Success("Target block not a Tobias Translocator");
        }

        bett.IsAtTobiasCave = true;
        bett.OwnerPlayerUid = null;
        var side = block.Variant["side"];
        var tpPos = posVec + BlockTobiasTeleporter.GetTeleportOffset(side);
        TeleporterData.TobiasTeleporterLocation = tpPos;
        needsSaving = true;

        return TextCommandResult.Success($"Tobias teleporter set to Tobias Cave");
    }

    private void Event_GameWorldSave()
    {
        if (needsSaving)
        {
            needsSaving = false;
            sapi.WorldManager.SaveGame.StoreData("tobiasTeleporterData", TeleporterData);
        }
    }

    private void Event_SaveGameLoaded()
    {
        TeleporterData = sapi.WorldManager.SaveGame.GetData("tobiasTeleporterData", new TobiasTeleporterData());
    }

    public void UpdatePlayerLastTeleport(Entity entity)
    {
        if (entity is EntityPlayer player && TeleporterData.PlayerLocations.TryGetValue(player.PlayerUID, out var location))
        {
            location.TotalDaysSinceLastTeleport = sapi.World.Calendar.TotalDays;
            SendLastUsageToPlayer(player.Player as IServerPlayer);
        }
    }

    public bool IsAllowedToTeleport(string playerUid, out Vec3d location)
    {
        if (TeleporterData.PlayerLocations.TryGetValue(playerUid, out var data))
        {
            var sixMonths = sapi.World.Calendar.DaysPerMonth * TpCooldownInMonths;
            if (data.TotalDaysSinceLastTeleport + sixMonths < sapi.World.Calendar.TotalDays)
            {
                location = data.Position;
                return true;
            }
        }

        location = null;
        return false;
    }

    public bool TryGetPlayerLocation(string playerUid, out Vec3d location)
    {
        if (TeleporterData.PlayerLocations.TryGetValue(playerUid, out var data))
        {
            location = data.Position;
            return true;
        }

        location = null;
        return false;
    }

    public void AddPlayerLocation(string playerUid, BlockPos position)
    {
        var block = sapi.World.BlockAccessor.GetBlock(position);
        var side = block.Variant["side"];
        var tpPos = position.ToVec3d() + BlockTobiasTeleporter.GetTeleportOffset(side);
        TeleporterData.PlayerLocations[playerUid] = new PlayerLocationData()
        {
            Position = tpPos,
            TotalDaysSinceLastTeleport = sapi.World.Calendar.TotalDays - sapi.World.Calendar.DaysPerMonth * TpCooldownInMonths
        };
        var player = sapi.World.PlayerByUid(playerUid) as IServerPlayer;
        SendLastUsageToPlayer(player);
        needsSaving = true;
    }

    public void RemovePlayerTeleporter(string ownerPlayerUid)
    {
        TeleporterData.PlayerLocations.Remove(ownerPlayerUid);
        var player = sapi.World.PlayerByUid(ownerPlayerUid);
        var message = new TobiasLastUsage() { LastUsage = 0 };
        serverChannel.SendPacket(message, player as IServerPlayer);
        needsSaving = true;
    }

    public double GetNextUsage()
    {
        var sixMonths = capi.World.Calendar.DaysPerMonth * TpCooldownInMonths;
        return Math.Max(0, sixMonths + OwnLastUsage - capi.World.Calendar.TotalDays);
    }
}
