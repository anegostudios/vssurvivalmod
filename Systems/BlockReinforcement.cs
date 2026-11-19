using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    // Tyron Apr13-2023: ImplicitFields can be removed by adding [ProtoMember(x)] in alphabetical order. If we want to add new fields here, we can do it this way without breaking backwards compatibility again
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BlockReinforcement {
        // [ProtoMember(7)]
        public int Strength;
        // [ProtoMember(6)]
        public string PlayerUID;
        // [ProtoMember(3)]
        public string LastPlayername;
        // [ProtoMember(4)]
        public bool Locked;
        // [ProtoMember(5)]
        public string LockedByItemCode;
        // [ProtoMember(1)]
        public int GroupUid;
        // [ProtoMember(2)]
        public string LastGroupname;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ChunkReinforcementData
    {
        public byte[] Data;
        public int chunkX, chunkY, chunkZ;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ReinforcedPrivilegeGrants
    {
        public string OwnedByPlayerUid;
        public int OwnedByGroupId;
        public Dictionary<string, EnumBlockAccessFlags> PlayerGrants = new Dictionary<string, EnumBlockAccessFlags>();
        public Dictionary<int, EnumBlockAccessFlags> GroupGrants = new Dictionary<int, EnumBlockAccessFlags>();
    }

    public class ReinforcedPrivilegeGrantsGroup
    {
        public string OwnedByPlayerUid;
        public int OwnedByGroupId;
        public EnumBlockAccessFlags DefaultGrants = EnumBlockAccessFlags.Use | EnumBlockAccessFlags.BuildOrBreak;
        public Dictionary<string, EnumBlockAccessFlags> PlayerGrants = new Dictionary<string, EnumBlockAccessFlags>();
        public Dictionary<int, EnumBlockAccessFlags> GroupGrants = new Dictionary<int, EnumBlockAccessFlags>();
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class PrivGrantsData
    {
        public Dictionary<string, ReinforcedPrivilegeGrants> privGrantsByOwningPlayerUid = new Dictionary<string, ReinforcedPrivilegeGrants>();
        public Dictionary<int, ReinforcedPrivilegeGrantsGroup> privGrantsByOwningGroupUid = new Dictionary<int, ReinforcedPrivilegeGrantsGroup>();
    }


    public class ModSystemBlockReinforcement : ModSystem
    {
        private const int ChunkDataCacheSize = 10;

        ICoreAPI api;
        IServerNetworkChannel serverChannel;

        // Both sided data
        Dictionary<string, ReinforcedPrivilegeGrants> privGrantsByOwningPlayerUid = new Dictionary<string, ReinforcedPrivilegeGrants>();
        Dictionary<int, ReinforcedPrivilegeGrantsGroup> privGrantsByOwningGroupUid = new Dictionary<int, ReinforcedPrivilegeGrantsGroup>();

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            this.api = api;

            api.RegisterItemClass("ItemPlumbAndSquare", typeof(ItemPlumbAndSquare));
            api.RegisterBlockBehaviorClass("Reinforcable", typeof(BlockBehaviorReinforcable));
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            // Needs to be done before assets are ready because it rewrites Behavior and CollectibleBehavior
            if (api.Side == EnumAppSide.Server) // No need to add it twice on the client
            {
                addReinforcementBehavior();
            }
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            api.Network
                .RegisterChannel("blockreinforcement")
                .RegisterMessageType(typeof(ChunkReinforcementData))
                .RegisterMessageType(typeof(PrivGrantsData))
                .SetMessageHandler<ChunkReinforcementData>(onChunkData)
                .SetMessageHandler<PrivGrantsData>(onPrivData)
            ;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;
            api.Event.PlayerJoin += Event_PlayerJoin;

            serverChannel = api.Network
                .RegisterChannel("blockreinforcement")
                .RegisterMessageType(typeof(ChunkReinforcementData))
                .RegisterMessageType(typeof(PrivGrantsData))
            ;

            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands.Create("bre")
                .WithDescription("Player owned Block reinforcement privilege management")
                .RequiresPrivilege(Privilege.chat)
                .BeginSubCommand("grant")
                    .WithDescription("Grant a player access to your block reinforcements")
                    .WithArgs(parsers.Word("playername"), parsers.WordRange("flag", "all", "use"))
                    .HandleWith(OnCmdGrant)
                .EndSubCommand()

                .BeginSubCommand("revoke")
                    .WithDescription("Revoke access for a player to your block reinforcements")
                    .WithArgs(parsers.Word("playername"))
                    .HandleWith(OnCmdRevoke)
                .EndSubCommand()

                .BeginSubCommand("grantgroup")
                    .WithDescription("Grant a group access to your block reinforcements")
                    .WithArgs(parsers.Word("groupname"), parsers.WordRange("flag", "all", "use"))
                    .HandleWith(OnCmdGrantGroup)
                .EndSubCommand()

                .BeginSubCommand("revokegroup")
                    .WithDescription("Revoke access for a group to your block reinforcements")
                .WithArgs(parsers.Word("groupname"))
                    .HandleWith(OnCmdRevokeGroup)
                .EndSubCommand()

                .BeginSubCommand("reinforce")
                    .WithAlias("rf")
                    .WithDescription("Reinforce an area. Select the area using worldedit. When leaving block_code empty the active hand slot is used. If 'all' is used as block_code, all blocks in the selection will be reinforced regardless of the active hand slot. If a block in the selection is a chisel block the first material will only be checked for target blocks. This command does ignore any block with render mode meta if strength is larger then 0.")
                    .WithArgs(parsers.Int("strength"), parsers.OptionalWord("block_code"), parsers.OptionalAll("playername"))
                    .RequiresPrivilege("worldedit")
                    .RequiresPlayer()
                    .HandleWith(OnCmdReinforce)
                .EndSubCommand()
            ;

            api.ChatCommands.Create("gbre")
                .WithDescription("Group owned Block reinforcement privilege management")
                .RequiresPrivilege(Privilege.chat)
                .BeginSubCommand("grant")
                    .WithDescription("Grant a player access to your groups block reinforcements. Use default as group name to change the access type for members")
                    .WithArgs(parsers.Word("playername"), parsers.WordRange("flag", "all", "use"))
                    .HandleWith(OnCmdGroupGrant)
                .EndSubCommand()

                .BeginSubCommand("revoke")
                    .WithDescription("Revoke a player access to your groups block reinforcements. Use default as group name to revoke the access type for goup members")
                    .WithArgs(parsers.Word("playername"))
                    .HandleWith(OnCmdGroupRevoke)
                .EndSubCommand()

                .BeginSubCommand("grantgroup")
                    .WithDescription("Grant an other group access to your groups block reinforcements")
                    .WithArgs(parsers.Word("groupname"), parsers.WordRange("flag", "all", "use"))
                    .HandleWith(OnCmdGroupGrantGroup)
                .EndSubCommand()

                .BeginSubCommand("revokegroup")
                    .WithDescription("Revoke an others groups access to your groups block reinforcements")
                    .WithArgs(parsers.Word("groupname"))
                    .HandleWith(OnCmdGroupRevokeGroup)
                .EndSubCommand()
            ;
            api.Event.OnSchematicExport += OnSchematicExport;
            api.Event.OnSchematicImport += OnSchematicImport;

            api.Permissions.RegisterPrivilege("denybreakreinforced", "Deny the ability to break reinforced blocks", false);
        }

        private void OnSchematicImport(IBlockAccessor blockAccessor, IWorldAccessor world, BlockPos startpos, Dictionary<uint, Dictionary<string, byte[]>> modData, Rectanglei? rec = null)
        {
            var ms = new MemoryStream();
            var chunkRefCache = new Dictionary<FastVec3i, Dictionary<int, BlockReinforcement>>();
            foreach (var (schematicLocalIndex, data) in modData)
            {
                int dx = (int)(schematicLocalIndex & BlockSchematic.PosBitMask);
                int dy = (int)((schematicLocalIndex >> 20) & BlockSchematic.PosBitMask);
                int dz = (int)((schematicLocalIndex >> 10) & BlockSchematic.PosBitMask);
                var pos = new BlockPos(dx, dy, dz).Add(startpos);

                // for partial placement check if we are inside the current chunk rectangle
                if(rec.HasValue && !rec.Value.Contains(pos.X, pos.Z)) continue;

                var chunkIndex = toChunkLocalIndex(pos);
                if (data.TryGetValue("reinforcement", out var reinforcements))
                {
                    var fastVec3I = toChunkPos(pos);
                    if (!chunkRefCache.TryGetValue(fastVec3I, out var chunkReinforcements))
                    {
                        chunkReinforcements = getOrCreateReinforcementsAt(pos, blockAccessor);
                        if (chunkReinforcements == null) continue;
                        chunkRefCache[fastVec3I] = chunkReinforcements;
                    }

                    ms.SetLength(0);
                    ms.Write(reinforcements,0,reinforcements.Length);
                    ms.Position = 0;
                    var res = Serializer.Deserialize<BlockReinforcement>(ms);
                    chunkReinforcements[chunkIndex] = res;
                }
            }

            foreach (var (pos, chunkReinforcements) in chunkRefCache)
            {
                SaveReinforcements(chunkReinforcements, pos, blockAccessor);
            }
        }

        private void OnSchematicExport(IBlockAccessor blockAccessor, IWorldAccessor world, BlockPos startPos, BlockPos endPos, Dictionary<BlockPos, Dictionary<string, byte[]>> modData)
        {
            int startX = Math.Min(startPos.X, endPos.X);
            int startY = Math.Min(startPos.InternalY, endPos.InternalY);
            int startZ = Math.Min(startPos.Z, endPos.Z);

            int endX = Math.Max(startPos.X, endPos.X);
            int endY = Math.Max(startPos.InternalY, endPos.InternalY);
            int endZ = Math.Max(startPos.Z, endPos.Z);

            var chunksize = GlobalConstants.ChunkSize;
            int mincx = startX / chunksize;
            int maxcx = endX / chunksize;
            int mincy = startY / chunksize;
            int maxcy = endY / chunksize;
            int mincz = startZ / chunksize;
            int maxcz = endZ / chunksize;

            Dictionary<BlockPos, BlockReinforcement> reinforcements = new ();
            var chunkRect = new Cuboidi(startPos, endPos);

            for (int cx = mincx; cx <= maxcx; cx++)
            {
                for (int cy = mincy; cy <= maxcy; cy++)
                {
                    for (int cz = mincz; cz <= maxcz; cz++)
                    {
                        if (GetChunkReinforcements(blockAccessor, new FastVec3i(cx, cy, cz), out var reinforcementsOfChunk) && reinforcementsOfChunk != null)
                        {
                            foreach (var (indexPos, reinforcement) in reinforcementsOfChunk)
                            {
                                var chunkLocalPos = toLocalPos(indexPos);
                                chunkLocalPos.Add(cx * chunksize, cy * chunksize, cz * chunksize);
                                if (chunkRect.Contains(chunkLocalPos))
                                {
                                    reinforcements.Add(chunkLocalPos, reinforcement);
                                }
                            }
                        }
                    }
                }
            }

            using var ms = new MemoryStream();
            foreach (var (pos, reinforcement) in reinforcements)
            {
                if (!modData.TryGetValue(pos, out var dict))
                {
                    dict = new Dictionary<string, byte[]>();
                    modData.Add(pos, dict);
                }
                ms.SetLength(0);
                Serializer.Serialize(ms, reinforcement);
                dict["reinforcement"] = ms.ToArray();
            }
        }

        private TextCommandResult OnCmdReinforce(TextCommandCallingArgs args)
        {
            if (!GetMarkedArea(args.Caller, out var startPos, out var endPos))
            {
                return TextCommandResult.Success("Mark an area with with WorldEdit first.");
            }
            var strength = (int)args[0];

            var playerPlayerUid = args.Caller.Player.PlayerUID;

            Block block = args.Caller.Player.InventoryManager.ActiveHotbarSlot.Itemstack?.Block;
            if(!args.Parsers[1].IsMissing)
            {
                var assetLocation = args[1].ToString();
                if (assetLocation == "all")
                {
                    block = null;
                }
                else
                {
                    block = api.World.GetBlock(assetLocation);
                    if (block == null)
                    {
                        return TextCommandResult.Success($"Block {assetLocation} not found");
                    }
                }
            }

            var playerPlayerName = args.Caller.Player.PlayerName;
            if(!args.Parsers[2].IsMissing)
            {
                playerPlayerName = args[2].ToString();
            }

            var minX = Math.Min(startPos.X, endPos.X);
            var minY = Math.Min(startPos.Y, endPos.Y);
            var minZ = Math.Min(startPos.Z, endPos.Z);

            var maxX = Math.Max(startPos.X, endPos.X);
            var maxY = Math.Max(startPos.Y, endPos.Y);
            var maxZ = Math.Max(startPos.Z, endPos.Z);
            var reinforcements = 0;
            var chunkRefCache = new Dictionary<FastVec3i, Dictionary<int, BlockReinforcement>>();
            var tmpPos = new BlockPos();
            for (int x = minX; x < maxX; x++)
            {
                for (int z = minZ; z < maxZ; z++)
                {
                    for (int y = minY; y < maxY; y++)
                    {
                        tmpPos.Set(x, y, z);
                        var currBlock = api.World.BlockAccessor.GetBlock(tmpPos);
                        if (currBlock?.Id == 0 || (strength > 0 && currBlock?.RenderPass == EnumChunkRenderPass.Meta)) continue;

                        if (block != null && currBlock != null)
                        {
                            var mbl = currBlock as BlockMicroBlock;
                            if (currBlock.Code != block.Code && mbl == null)
                            {
                                continue;
                            }

                            if (mbl != null && mbl.GetBlockEntity<BlockEntityMicroBlock>(tmpPos).BlockIds.FirstOrDefault() != block.Id)
                            {
                                continue;
                            }
                        }

                        var chunkPos = toChunkPos(tmpPos);
                        Dictionary<int, BlockReinforcement> reinforcementsOfChunk;
                        if (!chunkRefCache.TryGetValue(chunkPos, out reinforcementsOfChunk))
                        {
                            reinforcementsOfChunk = getOrCreateReinforcementsAt(tmpPos, api.World.BlockAccessor);
                        }
                        int index3d = toChunkLocalIndex(tmpPos);

                        if (strength <= 0)
                        {
                            if (reinforcementsOfChunk.Remove(index3d))
                            {
                                reinforcements++;
                                chunkRefCache[chunkPos] = reinforcementsOfChunk;
                            }

                            continue;
                        }

                        reinforcements++;
                        if (reinforcementsOfChunk.TryGetValue(index3d, out var bre))
                        {
                            bre.Strength = strength;
                            bre.LastPlayername = playerPlayerName;
                            bre.PlayerUID = playerPlayerUid;
                        }
                        else
                        {
                            reinforcementsOfChunk[index3d] = new BlockReinforcement()
                            {
                                PlayerUID = playerPlayerUid,
                                LastPlayername = playerPlayerName,
                                Strength = strength
                            };
                        }

                        chunkRefCache[chunkPos] = reinforcementsOfChunk;
                    }
                }
            }

            foreach (var (pos, chunkReinforcements) in chunkRefCache)
            {
                SaveReinforcements(chunkReinforcements, pos, api.World.BlockAccessor);
            }
            return TextCommandResult.Success($"Reinforced {reinforcements} Blocks");
        }

        private bool GetMarkedArea(Caller caller, out BlockPos startPos, out BlockPos endPos)
        {
            var uid = caller.Player.PlayerUID;
            startPos = null;
            endPos = null;

            if (api.ObjectCache.TryGetValue("weStartMarker-" + uid, out var start))
            {
                startPos = start as BlockPos;
            }

            if (api.ObjectCache.TryGetValue("weEndMarker-" + uid, out var end))
            {
                endPos = end as BlockPos;
            }
            return startPos != null && endPos != null;
        }

        protected TextCommandResult OnCmdGroupRevokeGroup(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            var groupId = args.Caller.FromChatGroupId;
            var groupName = args.Parsers[0].GetValue() as string;

            var group = player.GetGroup(groupId);
            if (group == null)
            {
                return TextCommandResult.Success("Type this command inside the group chat tab that you are a owner of");
            }
            if (group.Level != EnumPlayerGroupMemberShip.Owner)
            {
                return TextCommandResult.Success("Must be owner of the group to change access flags");
            }

            if (!privGrantsByOwningGroupUid.TryGetValue(groupId, out ReinforcedPrivilegeGrantsGroup groupGrants))
            {
                privGrantsByOwningGroupUid[groupId] = groupGrants = new ReinforcedPrivilegeGrantsGroup();
            }
            return GrantRevokeGroupOwned2Group(player, groupId, args.Command.Name, groupName ,"none", EnumBlockAccessFlags.None, groupGrants);
        }

        protected TextCommandResult OnCmdGroupGrantGroup(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            var groupId = args.Caller.FromChatGroupId;
            var groupName = args.Parsers[0].GetValue() as string;
            var flagString = args.Parsers[1].GetValue() as string;

            var group = player.GetGroup(groupId);
            if (group == null)
            {
                return TextCommandResult.Success("Type this command inside the group chat tab that you are a owner of");
            }
            if (group.Level != EnumPlayerGroupMemberShip.Owner)
            {
                return TextCommandResult.Success("Must be owner of the group to change access flags");
            }

            var flags = GetFlags(flagString);

            if (!privGrantsByOwningGroupUid.TryGetValue(groupId, out ReinforcedPrivilegeGrantsGroup groupGrants))
            {
                privGrantsByOwningGroupUid[groupId] = groupGrants = new ReinforcedPrivilegeGrantsGroup();
            }
            return GrantRevokeGroupOwned2Group(player, groupId, args.Command.Name, groupName ,flagString, flags, groupGrants);
        }

        protected static EnumBlockAccessFlags GetFlags(string flagString)
        {
            var flags = EnumBlockAccessFlags.None;
            if (flagString != null)
            {
                if (flagString.ToLowerInvariant() == "use") flags = EnumBlockAccessFlags.Use;
                if (flagString.ToLowerInvariant() == "all")
                    flags = EnumBlockAccessFlags.BuildOrBreak | EnumBlockAccessFlags.Use;
            }

            return flags;
        }

        protected TextCommandResult OnCmdGroupRevoke(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            var groupId = args.Caller.FromChatGroupId;
            var playerName = args.Parsers[0].GetValue() as string;

            var group = player.GetGroup(groupId);
            if (group == null)
            {
                return TextCommandResult.Success("Type this command inside the group chat tab that you are a owner of");
            }
            if (group.Level != EnumPlayerGroupMemberShip.Owner)
            {
                return TextCommandResult.Success("Must be owner of the group to change access flags");
            }


            if (!privGrantsByOwningGroupUid.TryGetValue(groupId, out ReinforcedPrivilegeGrantsGroup groupGrants))
            {
                privGrantsByOwningGroupUid[groupId] = groupGrants = new ReinforcedPrivilegeGrantsGroup();
            }

            if (playerName == "default")
            {
                groupGrants.DefaultGrants = 0;
                SyncPrivData();
                return TextCommandResult.Success("All access revoked for group members");
            }
            GrantRevokeGroupOwned2Player(player, groupId, args.Command.Name, playerName, "none", EnumBlockAccessFlags.None, groupGrants);
            return TextCommandResult.Success();
        }

        protected TextCommandResult OnCmdGroupGrant(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            var groupId = args.Caller.FromChatGroupId;
            var playerName = args.Parsers[0].GetValue() as string;
            var flagString = args.Parsers[1].GetValue() as string;

            var group = player.GetGroup(groupId);
            if (group == null)
            {
                return TextCommandResult.Success("Type this command inside the group chat tab that you are a owner of");
            }
            if (group.Level != EnumPlayerGroupMemberShip.Owner)
            {
                return TextCommandResult.Success("Must be owner of the group to change access flags");
            }

            var flags = GetFlags(flagString);

            if (!privGrantsByOwningGroupUid.TryGetValue(groupId, out ReinforcedPrivilegeGrantsGroup groupGrants))
            {
                privGrantsByOwningGroupUid[groupId] = groupGrants = new ReinforcedPrivilegeGrantsGroup();
            }
            if (playerName == "default")
            {
                groupGrants.DefaultGrants = flags;
                SyncPrivData();
                return TextCommandResult.Success("Default access for group members set to " + flagString);
            }
            GrantRevokeGroupOwned2Player(player, groupId, args.Command.Name, playerName, flagString, flags, groupGrants);
            return TextCommandResult.Success();
        }

        protected TextCommandResult OnCmdGrantGroup(TextCommandCallingArgs args)
        {
            var groupName = args.Parsers[0].GetValue() as string;
            var flagString = args.Parsers[1].GetValue() as string;
            var flags = GetFlags(flagString);
            return SetGroupPrivilege(args.Caller.Player as IServerPlayer, groupName, flags);
        }

        protected TextCommandResult OnCmdRevokeGroup(TextCommandCallingArgs args)
        {
            var groupName = args.Parsers[0].GetValue() as string;
            return SetGroupPrivilege(args.Caller.Player as IServerPlayer, groupName, EnumBlockAccessFlags.None);
        }

        protected TextCommandResult OnCmdGrant(TextCommandCallingArgs args)
        {
            var playerName = args.Parsers[0].GetValue() as string;
            var flagString = args.Parsers[1].GetValue() as string;
            var sapi = api as ICoreServerAPI;

            var flags = GetFlags(flagString);
            IServerPlayerData plrData = null;
            plrData = sapi.PlayerData.GetPlayerDataByLastKnownName(playerName);
            if (plrData == null)
            {
                return TextCommandResult.Success("No player with such name found or never connected to this server");
            }
            return SetPlayerPrivilege(args.Caller.Player as IServerPlayer, plrData.PlayerUID, flags);
        }

        protected TextCommandResult OnCmdRevoke(TextCommandCallingArgs args)
        {
            var playerName = args.Parsers[0].GetValue() as string;
            var sapi = api as ICoreServerAPI;
            IServerPlayerData plrData;
            plrData = sapi.PlayerData.GetPlayerDataByLastKnownName(playerName);
            if (plrData == null)
            {
                return TextCommandResult.Success( "No player with such name found or never connected to this server");
            }

            return SetPlayerPrivilege(args.Caller.Player as IServerPlayer, plrData.PlayerUID, EnumBlockAccessFlags.None);
        }

        protected void GrantRevokeGroupOwned2Player(IServerPlayer player, int groupId, string command, string playername, string flagString, EnumBlockAccessFlags flags, ReinforcedPrivilegeGrantsGroup groupGrants)
        {
            (api as ICoreServerAPI).PlayerData.ResolvePlayerName(playername, (result, playeruid) =>
            {
                if (result == EnumServerResponse.Good)
                {
                    if (command == "grant")
                    {
                        groupGrants.PlayerGrants[playeruid] = flags;
                        player.SendMessage(groupId, flagString + " access set for player " + playername, EnumChatType.CommandError);
                        SyncPrivData();
                    }
                    else
                    {
                        if (groupGrants.PlayerGrants.Remove(playeruid))
                        {
                            player.SendMessage(groupId, "All access revoked for player " + playername, EnumChatType.CommandError);
                            SyncPrivData();
                        }
                        else
                        {
                            player.SendMessage(groupId, "This player has no access. No action taken.", EnumChatType.CommandError);
                        }
                    }
                    return;
                }

                if (result == EnumServerResponse.Offline)
                {
                    player.SendMessage(groupId, Lang.Get("Player with name '{0}' is not online and auth server is offline. Cannot check if this player exists. Try again later.", playername), EnumChatType.CommandError);
                    return;
                }

                player.SendMessage(groupId, Lang.Get("No player with name '{0}' exists", playername), EnumChatType.CommandError);
            });
        }

        protected TextCommandResult GrantRevokeGroupOwned2Group(IServerPlayer player, int groupId, string command, string groupname, string flagString, EnumBlockAccessFlags flags, ReinforcedPrivilegeGrantsGroup groupGrants)
        {
            var group = (api as ICoreServerAPI).Groups.GetPlayerGroupByName(groupname);

            if(group == null)
                return TextCommandResult.Success(Lang.Get("No group with name '{0}' exists", groupname));

            string msg;
            if (command == "grant")
            {
                groupGrants.GroupGrants[group.Uid] = flags;
                msg = flagString + " access set for group " + groupname;
                SyncPrivData();
            }
            else
            {
                if (groupGrants.GroupGrants.Remove(group.Uid))
                {
                    msg = "All access revoked for group " + groupname;
                }
                else
                {
                    msg = "This group has no access. No action taken.";
                    SyncPrivData();
                }
            }
            return TextCommandResult.Success(msg);
        }

        protected void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            serverChannel?.SendPacket(new PrivGrantsData() { privGrantsByOwningPlayerUid = privGrantsByOwningPlayerUid, privGrantsByOwningGroupUid = privGrantsByOwningGroupUid }, byPlayer);
        }

        protected void onChunkData(ChunkReinforcementData msg)
        {
            IWorldChunk chunk = api.World.BlockAccessor.GetChunk(msg.chunkX, msg.chunkY, msg.chunkZ);
            if (chunk != null)
            {
                chunk.SetModdata("reinforcements", msg.Data);
            }

            reinfCache.Remove(new FastVec3i(msg.chunkX, msg.chunkY, msg.chunkZ));
        }

        protected void onPrivData(PrivGrantsData networkMessage)
        {
            this.privGrantsByOwningPlayerUid = networkMessage.privGrantsByOwningPlayerUid;
            this.privGrantsByOwningGroupUid = networkMessage.privGrantsByOwningGroupUid;
        }

        protected void SyncPrivData()
        {
            serverChannel?.BroadcastPacket(new PrivGrantsData() { privGrantsByOwningPlayerUid = privGrantsByOwningPlayerUid, privGrantsByOwningGroupUid = privGrantsByOwningGroupUid });
        }

        protected void Event_GameWorldSave()
        {
            (api as ICoreServerAPI).WorldManager.SaveGame.StoreData("blockreinforcementprivileges", SerializerUtil.Serialize(privGrantsByOwningPlayerUid));
            (api as ICoreServerAPI).WorldManager.SaveGame.StoreData("blockreinforcementprivilegesgroup", SerializerUtil.Serialize(privGrantsByOwningGroupUid));
        }

        protected void Event_SaveGameLoaded()
        {
            byte[] data = (api as ICoreServerAPI).WorldManager.SaveGame.GetData("blockreinforcementprivileges");
            if (data != null)
            {
                try
                {
                    privGrantsByOwningPlayerUid = SerializerUtil.Deserialize<Dictionary<string, ReinforcedPrivilegeGrants>>(data);
                }
                catch
                {
                    api.World.Logger.Notification("Unable to load player->group privileges for the block reinforcement system. Exception thrown when trying to deserialize it. Will be discarded.");
                }
            }
            data = (api as ICoreServerAPI).WorldManager.SaveGame.GetData("blockreinforcementprivilegesgroup");
            if (data != null)
            {
                try
                {
                    privGrantsByOwningGroupUid = SerializerUtil.Deserialize<Dictionary<int, ReinforcedPrivilegeGrantsGroup>>(data);
                }
                catch
                {
                    api.World.Logger.Notification("Unable to load group->player privileges for the block reinforcement system. Exception thrown when trying to deserialize it. Will be discarded.");
                }
            }

        }

        /// <summary>
        /// If true, limits reinforcing of blocks to reasonable block materials (e.g. not plants, not snow, not leaves, not sand, etc.)
        /// </summary>
        public bool reasonableReinforcements = true;


        #region Reinforcing and Locking
        protected void addReinforcementBehavior()
        {
            foreach (Block block in api.World.Blocks)
            {
                if (block.Code == null || block.Id == 0) continue;

                if (IsReinforcable(block))
                {
                    block.BlockBehaviors = block.BlockBehaviors.Append(new BlockBehaviorReinforcable(block));
                    block.CollectibleBehaviors = block.CollectibleBehaviors.Append(new BlockBehaviorReinforcable(block));
                }
            }
        }

        protected bool IsReinforcable(Block block)
        {
            if (reasonableReinforcements && (block.BlockMaterial == EnumBlockMaterial.Plant ||
                    block.BlockMaterial == EnumBlockMaterial.Water ||
                    block.BlockMaterial == EnumBlockMaterial.Snow ||
                    block.BlockMaterial == EnumBlockMaterial.Leaves ||
                    block.BlockMaterial == EnumBlockMaterial.Lava ||
                    block.BlockMaterial == EnumBlockMaterial.Sand ||
                    block.BlockMaterial == EnumBlockMaterial.Gravel))
            {
                // Do not allow reinforcement of soft blocks, unless positively given the attribute reinforcable == true
                if (block.Attributes == null || block.Attributes["reinforcable"].AsBool(false) != true) return false;
            }

            // For other blocks, allow reinforcement unless positively given the attribute reinforcable == false
            if (block.Attributes == null || block.Attributes["reinforcable"].AsBool(true) != false)
            {
                return true;
            }

            return false;
        }


        public ItemSlot FindResourceForReinforcing(IPlayer byPlayer)
        {
            ItemSlot foundSlot = null;

            byPlayer.Entity.WalkInventory((onSlot) =>
            {
                if (onSlot.Itemstack == null || onSlot.Itemstack.ItemAttributes == null) return true;
                if (onSlot is ItemSlotCreative) return true;
                if (!(onSlot.Inventory is InventoryBasePlayer)) return true;

                int? strength = onSlot.Itemstack.ItemAttributes["reinforcementStrength"].AsInt(0);
                if (strength > 0)
                {
                    foundSlot = onSlot;
                    return false;
                }

                return true;
            });

            return foundSlot;
        }


        public bool TryRemoveReinforcement(BlockPos pos, IPlayer forPlayer, ref string errorCode)
        {
            Dictionary<int, BlockReinforcement> reinforcementsOfChunk = getOrCreateReinforcementsAt(pos);
            if (reinforcementsOfChunk == null) return false;

            int index3d = toChunkLocalIndex(pos);
            if (!reinforcementsOfChunk.ContainsKey(index3d))
            {
                errorCode = "notreinforced";
                return false;
            }

            var bre = reinforcementsOfChunk[index3d];
            var group = forPlayer.GetGroup(bre.GroupUid);
            if (bre.PlayerUID != forPlayer.PlayerUID && group == null && (GetAccessFlags(bre.PlayerUID, bre.GroupUid, forPlayer) & EnumBlockAccessFlags.BuildOrBreak) == 0)
            {
                errorCode = "notownblock";
                return false;
            }

            reinforcementsOfChunk.Remove(index3d);

            SaveReinforcements(reinforcementsOfChunk, toChunkPos(pos));
            return true;
        }

        protected FastVec3i toChunkPos(BlockPos pos)
        {
            var chunkSize = GlobalConstants.ChunkSize;
            return new FastVec3i(pos.X / chunkSize, pos.Y / chunkSize, pos.Z / chunkSize);
        }

        public bool IsReinforced(BlockPos pos)
        {
            Dictionary<int, BlockReinforcement> reinforcementsOfChunk = getOrCreateReinforcementsAt(pos);
            if (reinforcementsOfChunk == null) return false;
            int index3d = toChunkLocalIndex(pos);

            return reinforcementsOfChunk.ContainsKey(index3d);
        }

        public bool IsLockedForInteract(BlockPos pos, IPlayer forPlayer)
        {
            Dictionary<int, BlockReinforcement> reinforcementsOfChunk = getOrCreateReinforcementsAt(pos);
            if (reinforcementsOfChunk == null) return false;

            int index3d = toChunkLocalIndex(pos);
            if (reinforcementsOfChunk.TryGetValue(index3d, out BlockReinforcement bre))
            {
                if (bre.Locked && bre.PlayerUID != forPlayer.PlayerUID && forPlayer.GetGroup(bre.GroupUid) == null)
                {
                    EnumBlockAccessFlags flags = GetAccessFlags(bre.PlayerUID, bre.GroupUid, forPlayer);

                    if ((flags & EnumBlockAccessFlags.Use) > 0) return false;

                    if (forPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative && forPlayer.HasPrivilege(Privilege.commandplayer))
                    {
                        return false;
                    }

                    return true;
                }
            }

            return false;
        }


        public EnumBlockAccessFlags GetAccessFlags(string owningPlayerUid, int owningGroupId, IPlayer forPlayer)
        {
            if (owningPlayerUid == forPlayer.PlayerUID) return EnumBlockAccessFlags.Use | EnumBlockAccessFlags.BuildOrBreak;
            var group = forPlayer.GetGroup(owningGroupId);
            if (group != null) return EnumBlockAccessFlags.Use | EnumBlockAccessFlags.BuildOrBreak;

            EnumBlockAccessFlags flags = EnumBlockAccessFlags.None;

            if (owningPlayerUid != null && privGrantsByOwningPlayerUid.TryGetValue(owningPlayerUid, out ReinforcedPrivilegeGrants grants))
            {
                // Maybe player privilege?
                grants.PlayerGrants.TryGetValue(forPlayer.PlayerUID, out flags);

                // Maybe group privilege?
                foreach (var val in grants.GroupGrants)
                {
                    if (forPlayer.GetGroup(val.Key) != null)
                    {
                        flags |= val.Value;
                    }
                }
            }

            if (owningGroupId != 0 && privGrantsByOwningGroupUid.TryGetValue(owningGroupId, out ReinforcedPrivilegeGrantsGroup grantsgr))
            {
                // Is a member of the owning group
                if (group != null)
                {
                    grantsgr.PlayerGrants.TryGetValue(forPlayer.PlayerUID, out flags);
                    flags |= grantsgr.DefaultGrants;
                }

                // Is a member of a group who has access to the reinforcement
                foreach (var val in grantsgr.GroupGrants)
                {
                    if (forPlayer.GetGroup(val.Key) != null)
                    {
                        flags |= val.Value;
                    }
                }
            }

            return flags;
        }



        public bool TryLock(BlockPos pos, IPlayer byPlayer, string itemCode)
        {
            Dictionary<int, BlockReinforcement> reinforcementsOfChunk = getOrCreateReinforcementsAt(pos);
            if (reinforcementsOfChunk == null) return false;

            int index3d = toChunkLocalIndex(pos);

            if (reinforcementsOfChunk.TryGetValue(index3d, out BlockReinforcement bre))
            {
                var membership = byPlayer.GetGroup(bre.GroupUid);
                bool isAllowed = bre.PlayerUID == byPlayer.PlayerUID;
                if (membership != null)
                {
                    isAllowed |= membership.Level == EnumPlayerGroupMemberShip.Owner;
                    isAllowed |= membership.Level == EnumPlayerGroupMemberShip.Op;
                }

                if (!isAllowed || bre.Locked) return false;
                bre.Locked = true;
                bre.LockedByItemCode = itemCode;
                SaveReinforcements(reinforcementsOfChunk, toChunkPos(pos));
                return true;
            }

            reinforcementsOfChunk[index3d] = new BlockReinforcement() { PlayerUID = byPlayer.PlayerUID, LastPlayername = byPlayer.PlayerName, Strength = 0, Locked = true, LockedByItemCode = itemCode };

            SaveReinforcements(reinforcementsOfChunk, toChunkPos(pos));

            return true;
        }



        public BlockReinforcement GetReinforcment(BlockPos pos)
        {
            Dictionary<int, BlockReinforcement> reinforcementsOfChunk = getOrCreateReinforcementsAt(pos);
            if (reinforcementsOfChunk == null) return null;

            int index3d = toChunkLocalIndex(pos);
            if (!reinforcementsOfChunk.ContainsKey(index3d)) return null;

            return reinforcementsOfChunk[index3d];
        }


        public void ConsumeStrength(BlockPos pos, int byAmount)
        {
            Dictionary<int, BlockReinforcement> reinforcementsOfChunk = getOrCreateReinforcementsAt(pos);
            if (reinforcementsOfChunk == null) return;

            int index3d = toChunkLocalIndex(pos);
            if (!reinforcementsOfChunk.ContainsKey(index3d)) return;

            reinforcementsOfChunk[index3d].Strength -= byAmount;

            if (reinforcementsOfChunk[index3d].Strength <= 0)
            {
                reinforcementsOfChunk.Remove(index3d);
            }

            SaveReinforcements(reinforcementsOfChunk, toChunkPos(pos));
        }

        public void ClearReinforcement(BlockPos pos)
        {
            Dictionary<int, BlockReinforcement> reinforcementsOfChunk = getOrCreateReinforcementsAt(pos);
            if (reinforcementsOfChunk == null) return;

            int index3d = toChunkLocalIndex(pos);
            if (!reinforcementsOfChunk.ContainsKey(index3d)) return;

            if (reinforcementsOfChunk.Remove(index3d))
            {
                SaveReinforcements(reinforcementsOfChunk, toChunkPos(pos));
            }
        }


        /// <summary>
        /// Reinforces a block. If forGroupId is not set, the reinforcment is owned by the player, otherwise the reinforcment will be owned by the group
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="byPlayer"></param>
        /// <param name="strength"></param>
        /// <param name="forGroupUid"></param>
        /// <returns></returns>
        public bool StrengthenBlock(BlockPos pos, IPlayer byPlayer, int strength, int forGroupUid = 0)
        {
            if (api.Side == EnumAppSide.Client) return false;

            if (!api.World.BlockAccessor.GetBlock(pos, BlockLayersAccess.Solid).HasBehavior<BlockBehaviorReinforcable>()) return false;

            Dictionary<int, BlockReinforcement> reinforcementsOfChunk = getOrCreateReinforcementsAt(pos);
            if(reinforcementsOfChunk == null) return false;

            int index3d = toChunkLocalIndex(pos);

            if (reinforcementsOfChunk.ContainsKey(index3d))
            {
                BlockReinforcement bre = reinforcementsOfChunk[index3d];
                if (bre.Strength > 0) return false;
                bre.Strength = strength;
            } else
            {
                string grpname = null;
                if ((api as ICoreServerAPI).Groups.PlayerGroupsById.TryGetValue(forGroupUid, out var grp)) grpname = grp.Name;

                reinforcementsOfChunk[index3d] = new BlockReinforcement() {
                    PlayerUID = forGroupUid == 0 ? byPlayer.PlayerUID : null,
                    GroupUid = forGroupUid,
                    LastPlayername = byPlayer.PlayerName,
                    LastGroupname = grpname,
                    Strength = strength
                };
            }

            SaveReinforcements(reinforcementsOfChunk, toChunkPos(pos));

            return true;
        }

        public void SetReinforcement(BlockReinforcement reinforcement, BlockPos pos)
        {
            Dictionary<int, BlockReinforcement> reinforcementsOfChunk = getOrCreateReinforcementsAt(pos);
            int index3d = toChunkLocalIndex(pos);
            reinforcementsOfChunk[index3d] = reinforcement;
            SaveReinforcements(reinforcementsOfChunk, toChunkPos(pos));
        }

        class BlockReinforcementChunk
        {
            public Dictionary<int, BlockReinforcement> Reinforcements;
        }

        API.Datastructures.OrderedDictionary<FastVec3i, BlockReinforcementChunk> reinfCache = new API.Datastructures.OrderedDictionary<FastVec3i, BlockReinforcementChunk>();

        protected Dictionary<int, BlockReinforcement> getOrCreateReinforcementsAt(BlockPos pos, IBlockAccessor blockAccessor = null)
        {
            blockAccessor ??= api.World.BlockAccessor;
            var chunkPos = new FastVec3i(pos.X / GlobalConstants.ChunkSize, pos.Y / GlobalConstants.ChunkSize, pos.Z / GlobalConstants.ChunkSize);
            if (!GetChunkReinforcements(blockAccessor, chunkPos, out var reinforcementsOfChunk))
            {
                return null;
            }

            reinforcementsOfChunk ??= new Dictionary<int, BlockReinforcement>();

            reinfCache[chunkPos] = new BlockReinforcementChunk() { Reinforcements = reinforcementsOfChunk };
            if (reinfCache.Count > ChunkDataCacheSize) reinfCache.RemoveAt(0);

            return reinforcementsOfChunk;
        }

        private bool GetChunkReinforcements(IBlockAccessor blockAccessor, FastVec3i chunkPos, out Dictionary<int, BlockReinforcement> reinforcementsOfChunk)
        {
            reinforcementsOfChunk = null;
            if (reinfCache.TryGetValue(chunkPos, out BlockReinforcementChunk cachedChunk))
            {
                reinforcementsOfChunk = cachedChunk.Reinforcements;
                return true;
            }

            IWorldChunk chunk = blockAccessor.GetChunk(chunkPos.X, chunkPos.Y, chunkPos.Z);
            if (chunk == null)
            {
                return false;
            }
            byte[] data = chunk.GetModdata("reinforcements");

            if (data != null)
            {
                try
                {
                    reinforcementsOfChunk = SerializerUtil.Deserialize<Dictionary<int, BlockReinforcement>>(data);
                }
                catch (Exception _) { }
            }

            return true;
        }

        protected void SaveReinforcements(Dictionary<int, BlockReinforcement> reif, FastVec3i chunkPos, IBlockAccessor blockAccessor = null)
        {
            blockAccessor ??= api.World.BlockAccessor;
            byte[] data = SerializerUtil.Serialize(reif);

            IWorldChunk chunk = blockAccessor.GetChunk(chunkPos.X, chunkPos.Y, chunkPos.Z);

            chunk.SetModdata("reinforcements", data);

            var chunkSize = GlobalConstants.ChunkSize;
            if(!(blockAccessor is IWorldGenBlockAccessor))
            {
                serverChannel?.BroadcastPacketInRange(new FastBlockPos(chunkPos.X * chunkSize, chunkPos.Y * chunkSize, chunkPos.Z * chunkSize),
                    new ChunkReinforcementData() {
                    chunkX = chunkPos.X, chunkY = chunkPos.Y, chunkZ = chunkPos.Z,
                    Data = data,
                });
            }
        }

        #endregion

        #region Privilege Stuff

        public TextCommandResult SetPlayerPrivilege(IServerPlayer owningPlayer, string forPlayerUid, EnumBlockAccessFlags access)
        {
            if (!privGrantsByOwningPlayerUid.TryGetValue(owningPlayer.PlayerUID, out ReinforcedPrivilegeGrants grants))
            {
                grants = new ReinforcedPrivilegeGrants();
                privGrantsByOwningPlayerUid[owningPlayer.PlayerUID] = grants;
            }

            string msg;
            if (access == EnumBlockAccessFlags.None)
            {
                if (grants.PlayerGrants.Remove(forPlayerUid))
                {
                    msg = Lang.Get("Ok, privilege revoked from player.");
                } else
                {
                    msg = Lang.Get("No action taken. Player does not have any privilege to your reinforced blocks.");
                }
            } else
            {
                grants.PlayerGrants[forPlayerUid] = access;
                msg = Lang.Get("Ok, Privilege for player set.");
            }

            SyncPrivData();
            return TextCommandResult.Success(msg);
        }

        public TextCommandResult SetGroupPrivilege(IServerPlayer owningPlayer,string forGroupName, EnumBlockAccessFlags access)
        {
            if (!privGrantsByOwningPlayerUid.TryGetValue(owningPlayer.PlayerUID, out ReinforcedPrivilegeGrants grants))
            {
                grants = new ReinforcedPrivilegeGrants();
                privGrantsByOwningPlayerUid[owningPlayer.PlayerUID] = grants;
            }

            string msg;
            PlayerGroup group = (api as ICoreServerAPI).Groups.GetPlayerGroupByName(forGroupName);
            if (group == null)
            {
                return TextCommandResult.Success(Lang.Get("No such group found"));
            }

            if (access == EnumBlockAccessFlags.None)
            {
                if (grants.GroupGrants.Remove(group.Uid))
                {
                    msg = Lang.Get("Ok, privilege revoked from group.");
                }
                else
                {
                    msg = Lang.Get("No action taken. Group does not have any privilege to your reinforced blocks.");
                }
            }
            else
            {
                grants.GroupGrants[group.Uid] = access;
                msg = Lang.Get("Ok, Privilege for group set.");
            }

            SyncPrivData();
            return TextCommandResult.Success(msg);
        }


        #endregion

        protected int toChunkLocalIndex(BlockPos pos)
        {
            return toChunkLocalIndex(pos.X % GlobalConstants.ChunkSize, pos.Y % GlobalConstants.ChunkSize, pos.Z % GlobalConstants.ChunkSize);
        }

        protected int toChunkLocalIndex(int x, int y, int z)
        {
            return (y << 16) | (z << 8) | (x);
        }

        protected BlockPos toLocalPos(int index)
        {
            return new BlockPos(index & 0xFF, index >> 16, (index >> 8) & 0xFF);
        }
    }
}
