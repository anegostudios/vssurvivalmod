using System;
using System.Collections.Generic;
using System.Linq;
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
}

[ProtoContract]
public class PlayerLastUsageData
{
    [ProtoMember(1)]
    public double LastTeleportTotalDays;
}

[ProtoContract]
public class TobiasTeleporterData
{
    [ProtoMember(1)]
    public Vec3d TobiasTeleporterLocation { get; set; }

    [ProtoMember(2)]
    public Dictionary<string, PlayerLocationData> PlayerLocations = new Dictionary<string, PlayerLocationData>();

    [ProtoMember(3)]
    public Dictionary<string, PlayerLastUsageData> PlayerLastUsages = new Dictionary<string, PlayerLastUsageData>();
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
        if (TeleporterData.PlayerLastUsages.TryGetValue(byplayer.PlayerUID, out var playerLastUsage))
        {
            var message = new TobiasLastUsage() { LastUsage = playerLastUsage.LastTeleportTotalDays };
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
        SetTeleporterLocation(tpPos);

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

    //TODO? could be moved to Api and renamed into something like GetControllingPlayer()
    public static EntityPlayer GetPlayerForTeleportingEntity(Entity teleportingEntity)
    {
        // safeguard against teleporting multiple players on a mount, as that could be exploited to bypass TobiasTeleporter's current limitations
        if (teleportingEntity.GetInterface<IMountable>()?.Seats.Count(s => s.Passenger is EntityPlayer) > 1)
        {
            return null;
        }

        // if the entity is a player, return it directly
        if (teleportingEntity is EntityPlayer player)
        {
            return player;
        }

        // if the entity is a mount, try returning the player controlling the mount
        if (teleportingEntity.GetInterface<IMountable>()?.Controller is EntityPlayer controllerPlayer)
        {
            return controllerPlayer;
        }

        return null;
    }

    public void UpdatePlayerLastTeleport(Entity entity)
    {
        EntityPlayer player = GetPlayerForTeleportingEntity(entity);
        if (player == null) return;

        if (TeleporterData.PlayerLastUsages.TryGetValue(player.PlayerUID, out var playerLastUsage))
        {
            playerLastUsage.LastTeleportTotalDays = sapi.World.Calendar.TotalDays;
        }
        else
        {
            TeleporterData.PlayerLastUsages[player.PlayerUID] = new PlayerLastUsageData()
            {
                LastTeleportTotalDays = sapi.World.Calendar.TotalDays
            };
        }
        SendLastUsageToPlayer(player.Player as IServerPlayer);
        needsSaving = true;
    }

    public bool IsAllowedToTeleport(string playerUid, out Vec3d location)
    {
        if (TeleporterData.PlayerLocations.TryGetValue(playerUid, out var playerLocation))
        {
            var tpCooldownInDays = sapi.World.Calendar.DaysPerMonth * TpCooldownInMonths;
            if (!TeleporterData.PlayerLastUsages.TryGetValue(playerUid, out var playerLastUsage) || playerLastUsage.LastTeleportTotalDays + tpCooldownInDays < sapi.World.Calendar.TotalDays)
            {
                location = playerLocation.Position;
                return true;
            }
        }

        location = null;
        return false;
    }

    public void SetTeleporterLocation(Vec3d pos)
    {
        if (TeleporterData.TobiasTeleporterLocation != pos)
        {
            TeleporterData.TobiasTeleporterLocation = pos;
            needsSaving = true;
        }
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
        };
        needsSaving = true;
    }

    public void RemovePlayerTeleporter(string ownerPlayerUid)
    {
        TeleporterData.PlayerLocations.Remove(ownerPlayerUid);
        needsSaving = true;
    }

    public double GetNextUsage()
    {
        var tpCooldownInDays = capi.World.Calendar.DaysPerMonth * TpCooldownInMonths;
        return Math.Max(0, tpCooldownInDays + OwnLastUsage - capi.World.Calendar.TotalDays);
    }
}
