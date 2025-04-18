using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{


    // Aaaaah protobuf sux. v1.9.6 format:
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BlockReinforcementOld
    {
        public int Strength;
        public string PlayerUID;
        public string LastPlayername;

        public BlockReinforcement Update()
        {
            return new BlockReinforcement()
            {
                Strength = Strength,
                PlayerUID = PlayerUID,
                LastPlayername = LastPlayername
            };
        }
    }

    // Tyron Apr13-2023: ImplicitFields can be removed by adding [ProtoMember(x)] in alphabetical order. If we want to add new fields here, we can do it this way without breaking backwards compatibility again
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BlockReinforcement {
        public int Strength;
        public string PlayerUID;
        public string LastPlayername;
        public bool Locked;
        public string LockedByItemCode;

        public int GroupUid;
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

            api.Permissions.RegisterPrivilege("denybreakreinforced", "Deny the ability to break reinforced blocks", false);
        }

        private TextCommandResult OnCmdGroupRevokeGroup(TextCommandCallingArgs args)
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

            ReinforcedPrivilegeGrantsGroup groupGrants;
            if (!privGrantsByOwningGroupUid.TryGetValue(groupId, out groupGrants))
            {
                privGrantsByOwningGroupUid[groupId] = groupGrants = new ReinforcedPrivilegeGrantsGroup();
            }
            return GrantRevokeGroupOwned2Group(player, groupId, args.Command.Name, groupName ,"none", EnumBlockAccessFlags.None, groupGrants);
        }

        private TextCommandResult OnCmdGroupGrantGroup(TextCommandCallingArgs args)
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

            ReinforcedPrivilegeGrantsGroup groupGrants;
            if (!privGrantsByOwningGroupUid.TryGetValue(groupId, out groupGrants))
            {
                privGrantsByOwningGroupUid[groupId] = groupGrants = new ReinforcedPrivilegeGrantsGroup();
            }
            return GrantRevokeGroupOwned2Group(player, groupId, args.Command.Name, groupName ,flagString, flags, groupGrants);
        }

        private static EnumBlockAccessFlags GetFlags(string flagString)
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

        private TextCommandResult OnCmdGroupRevoke(TextCommandCallingArgs args)
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
            
            
            ReinforcedPrivilegeGrantsGroup groupGrants;
            if (!privGrantsByOwningGroupUid.TryGetValue(groupId, out groupGrants))
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

        private TextCommandResult OnCmdGroupGrant(TextCommandCallingArgs args)
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
            
            ReinforcedPrivilegeGrantsGroup groupGrants;
            if (!privGrantsByOwningGroupUid.TryGetValue(groupId, out groupGrants))
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

        private TextCommandResult OnCmdGrantGroup(TextCommandCallingArgs args)
        {
            var groupName = args.Parsers[0].GetValue() as string;
            var flagString = args.Parsers[1].GetValue() as string;
            var flags = GetFlags(flagString);
            return SetGroupPrivilege(args.Caller.Player as IServerPlayer, groupName, flags);
        }

        private TextCommandResult OnCmdRevokeGroup(TextCommandCallingArgs args)
        {
            var groupName = args.Parsers[0].GetValue() as string;
            return SetGroupPrivilege(args.Caller.Player as IServerPlayer, groupName, EnumBlockAccessFlags.None);
        }

        private TextCommandResult OnCmdGrant(TextCommandCallingArgs args)
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

        private TextCommandResult OnCmdRevoke(TextCommandCallingArgs args)
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

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            serverChannel?.SendPacket(new PrivGrantsData() { privGrantsByOwningPlayerUid = privGrantsByOwningPlayerUid, privGrantsByOwningGroupUid = privGrantsByOwningGroupUid }, byPlayer);
        }
        
        private void onChunkData(ChunkReinforcementData msg)
        {
            IWorldChunk chunk = api.World.BlockAccessor.GetChunk(msg.chunkX, msg.chunkY, msg.chunkZ);
            if (chunk != null)
            {
                chunk.SetModdata("reinforcements", msg.Data);
            }
        }

        private void onPrivData(PrivGrantsData networkMessage)
        {
            this.privGrantsByOwningPlayerUid = networkMessage.privGrantsByOwningPlayerUid;
            this.privGrantsByOwningGroupUid = networkMessage.privGrantsByOwningGroupUid;
        }

        void SyncPrivData()
        {
            serverChannel?.BroadcastPacket(new PrivGrantsData() { privGrantsByOwningPlayerUid = privGrantsByOwningPlayerUid, privGrantsByOwningGroupUid = privGrantsByOwningGroupUid });
        }



        private void Event_GameWorldSave()
        {
            (api as ICoreServerAPI).WorldManager.SaveGame.StoreData("blockreinforcementprivileges", SerializerUtil.Serialize(privGrantsByOwningPlayerUid));
            (api as ICoreServerAPI).WorldManager.SaveGame.StoreData("blockreinforcementprivilegesgroup", SerializerUtil.Serialize(privGrantsByOwningGroupUid));
        }

        private void Event_SaveGameLoaded()
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
        private void addReinforcementBehavior()
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
                    block.BlockMaterial == EnumBlockMaterial.Liquid ||
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
            Dictionary<int, BlockReinforcement> reinforcmentsOfChunk = getOrCreateReinforcmentsAt(pos);
            if (reinforcmentsOfChunk == null) return false;

            int index3d = toLocalIndex(pos);
            if (!reinforcmentsOfChunk.ContainsKey(index3d))
            {
                errorCode = "notreinforced";
                return false;
            }

            var bre = reinforcmentsOfChunk[index3d];
            var group = forPlayer.GetGroup(bre.GroupUid);
            if (bre.PlayerUID != forPlayer.PlayerUID && group == null && (GetAccessFlags(bre.PlayerUID, bre.GroupUid, forPlayer) & EnumBlockAccessFlags.BuildOrBreak) == 0)
            {
                errorCode = "notownblock";
                return false;
            }

            reinforcmentsOfChunk.Remove(index3d);

            SaveReinforcments(reinforcmentsOfChunk, pos);
            return true;
        }

        public bool IsReinforced(BlockPos pos)
        {
            Dictionary<int, BlockReinforcement> reinforcmentsOfChunk = getOrCreateReinforcmentsAt(pos);
            if (reinforcmentsOfChunk == null) return false;
            int index3d = toLocalIndex(pos);

            return reinforcmentsOfChunk.ContainsKey(index3d);
        }

        public bool IsLockedForInteract(BlockPos pos, IPlayer forPlayer)
        {
            Dictionary<int, BlockReinforcement> reinforcmentsOfChunk = getOrCreateReinforcmentsAt(pos);
            if (reinforcmentsOfChunk == null) return false;

            int index3d = toLocalIndex(pos);
            BlockReinforcement bre;
            if (reinforcmentsOfChunk.TryGetValue(index3d, out bre))
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

            ReinforcedPrivilegeGrants grants;
            EnumBlockAccessFlags flags = EnumBlockAccessFlags.None;

            if (owningPlayerUid != null && privGrantsByOwningPlayerUid.TryGetValue(owningPlayerUid, out grants))
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

            ReinforcedPrivilegeGrantsGroup grantsgr;
            if (owningGroupId != 0 && privGrantsByOwningGroupUid.TryGetValue(owningGroupId, out grantsgr))
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
            Dictionary<int, BlockReinforcement> reinforcmentsOfChunk = getOrCreateReinforcmentsAt(pos);
            if (reinforcmentsOfChunk == null) return false;

            int index3d = toLocalIndex(pos);

            BlockReinforcement bre;
            if (reinforcmentsOfChunk.TryGetValue(index3d, out bre))
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
                SaveReinforcments(reinforcmentsOfChunk, pos);
                return true;
            }

            reinforcmentsOfChunk[index3d] = new BlockReinforcement() { PlayerUID = byPlayer.PlayerUID, LastPlayername = byPlayer.PlayerName, Strength = 0, Locked = true, LockedByItemCode = itemCode };

            SaveReinforcments(reinforcmentsOfChunk, pos);

            return true;
        }



        public BlockReinforcement GetReinforcment(BlockPos pos)
        {
            Dictionary<int, BlockReinforcement> reinforcmentsOfChunk = getOrCreateReinforcmentsAt(pos);
            if (reinforcmentsOfChunk == null) return null;

            int index3d = toLocalIndex(pos);
            if (!reinforcmentsOfChunk.ContainsKey(index3d)) return null;

            return reinforcmentsOfChunk[index3d];
        }


        public void ConsumeStrength(BlockPos pos, int byAmount)
        {
            Dictionary<int, BlockReinforcement> reinforcmentsOfChunk = getOrCreateReinforcmentsAt(pos);
            if (reinforcmentsOfChunk == null) return;

            int index3d = toLocalIndex(pos);
            if (!reinforcmentsOfChunk.ContainsKey(index3d)) return;

            reinforcmentsOfChunk[index3d].Strength -= byAmount;

            if (reinforcmentsOfChunk[index3d].Strength <= 0)
            {
                reinforcmentsOfChunk.Remove(index3d);
            }
            SaveReinforcments(reinforcmentsOfChunk, pos);
        }

        public void ClearReinforcement(BlockPos pos)
        {
            Dictionary<int, BlockReinforcement> reinforcmentsOfChunk = getOrCreateReinforcmentsAt(pos);
            if (reinforcmentsOfChunk == null) return;

            int index3d = toLocalIndex(pos);
            if (!reinforcmentsOfChunk.ContainsKey(index3d)) return;

            if (reinforcmentsOfChunk.Remove(index3d))
            {
                SaveReinforcments(reinforcmentsOfChunk, pos);
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

            Dictionary<int, BlockReinforcement> reinforcmentsOfChunk = getOrCreateReinforcmentsAt(pos);

            int index3d = toLocalIndex(pos);

            if (reinforcmentsOfChunk.ContainsKey(index3d))
            {
                BlockReinforcement bre = reinforcmentsOfChunk[index3d];
                if (bre.Strength > 0) return false;
                bre.Strength = strength;
            } else
            {
                string grpname = null;
                if ((api as ICoreServerAPI).Groups.PlayerGroupsById.TryGetValue(forGroupUid, out var grp)) grpname = grp.Name;

                reinforcmentsOfChunk[index3d] = new BlockReinforcement() { 
                    PlayerUID = forGroupUid == 0 ? byPlayer.PlayerUID : null,  
                    GroupUid = forGroupUid,
                    LastPlayername = byPlayer.PlayerName, 
                    LastGroupname = grpname,
                    Strength = strength 
                };
            }
            
            SaveReinforcments(reinforcmentsOfChunk, pos);
            
            return true;
        }




        Dictionary<int, BlockReinforcement> getOrCreateReinforcmentsAt(BlockPos pos)
        {
            byte[] data;

            IWorldChunk chunk = api.World.BlockAccessor.GetChunkAtBlockPos(pos);
            if (chunk == null) return null;

            data = chunk.GetModdata("reinforcements");
            
            Dictionary<int, BlockReinforcement> reinforcmentsOfChunk;

            if (data != null)
            {
                try
                {
                    reinforcmentsOfChunk = SerializerUtil.Deserialize<Dictionary<int, BlockReinforcement>>(data);
                } catch (Exception)
                {
                    try
                    {
                        api.World.Logger.Warning("Failed reading block reinforcments at block position, maybe old format. Will attempt to convert.");

                        Dictionary<int, BlockReinforcementOld> old = SerializerUtil.Deserialize<Dictionary<int, BlockReinforcementOld>>(data);
                        reinforcmentsOfChunk = new Dictionary<int, BlockReinforcement>();
                        foreach (var val in old)
                        {
                            reinforcmentsOfChunk[val.Key] = val.Value.Update();
                        }
                        SaveReinforcments(reinforcmentsOfChunk, pos);

                        api.World.Logger.Warning("Ok, converted");

                    } catch (Exception e2)
                    {
                        api.World.Logger.VerboseDebug("Failed reading block reinforcments at block position {0}, will discard, sorry.", pos);
                        api.World.Logger.VerboseDebug(LoggerBase.CleanStackTrace(e2.ToString()));
                    }

                    reinforcmentsOfChunk = new Dictionary<int, BlockReinforcement>();
                }
            }
            else
            {
                reinforcmentsOfChunk = new Dictionary<int, BlockReinforcement>();
            }

            return reinforcmentsOfChunk;
        }

        void SaveReinforcments(Dictionary<int, BlockReinforcement> reif, BlockPos pos)
        {
            const int chunksize = GlobalConstants.ChunkSize;
            int chunkX = pos.X / chunksize;
            int chunkY = pos.Y / chunksize;
            int chunkZ = pos.Z / chunksize;

            byte[] data = SerializerUtil.Serialize(reif);

            IWorldChunk chunk = api.World.BlockAccessor.GetChunk(chunkX, chunkY, chunkZ);
            chunk.SetModdata("reinforcements", data);

            // Todo: Send only to players that have this chunk in their loaded range
            serverChannel?.BroadcastPacket(new ChunkReinforcementData() { chunkX = chunkX, chunkY = chunkY, chunkZ = chunkZ, Data = data });
        }

        #endregion

        #region Privilege Stuff

        public TextCommandResult SetPlayerPrivilege(IServerPlayer owningPlayer, string forPlayerUid, EnumBlockAccessFlags access)
        {
            ReinforcedPrivilegeGrants grants;
            if (!privGrantsByOwningPlayerUid.TryGetValue(owningPlayer.PlayerUID, out grants))
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
            ReinforcedPrivilegeGrants grants;
            if (!privGrantsByOwningPlayerUid.TryGetValue(owningPlayer.PlayerUID, out grants))
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

        int toLocalIndex(BlockPos pos)
        {
            return toLocalIndex(pos.X % GlobalConstants.ChunkSize, pos.Y % GlobalConstants.ChunkSize, pos.Z % GlobalConstants.ChunkSize);
        }

        int toLocalIndex(int x, int y, int z)
        {
            return (y << 16) | (z << 8) | (x);
        }

        Vec3i fromLocalIndex(int index)
        {
            return new Vec3i(index & 0xff, (index >> 16) & 0xff, (index >> 8) & 0xff);
        }

    }
}
