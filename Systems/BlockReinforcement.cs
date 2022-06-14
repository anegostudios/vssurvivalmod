using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
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

            if (api is ICoreServerAPI sapi) sapi.Event.AssetsFinalizers += addReinforcementBehavior;  // Needs to be done before assets are ready because it rewrites Behavior and CollectibleBehavior
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

            api.RegisterCommand("bre", "Player owned Block reinforcement privilege management", "[grant|revoke|grantgroup|revokegroup] [playername/groupname] [use or all]", onCmd, Privilege.chat);
            api.RegisterCommand("gbre", "Group owned Block reinforcement privilege management", "[grant|revoke|grantgroup|revokegroup] members or [playername/groupname] [use or all]", onCmdGroup, Privilege.chat);

            api.Permissions.RegisterPrivilege("denybreakreinforced", "Deny the ability to break reinforced blocks", false);
        }

        private void onCmdGroup(IServerPlayer player, int groupId, CmdArgs args)
        {
            string privtype = args.PopWord();
            string firstarg = args.PopWord();
            string flagString = args.PopWord();

            if (privtype == null)
            {
                player.SendMessage(groupId, "Syntax: /gbre [grant|revoke|grantgroup|revokegroup] default or [playername/groupname] [use or all]", EnumChatType.CommandError);
                return;
            }

            EnumBlockAccessFlags flags;
            if (flagString?.ToLowerInvariant() == "use") flags = EnumBlockAccessFlags.Use;
            else if (flagString?.ToLowerInvariant() == "all") flags = EnumBlockAccessFlags.BuildOrBreak | EnumBlockAccessFlags.Use;
            else
            {
                if (privtype == "grant")
                {
                    player.SendMessage(groupId, "Missing argument or argument is not 'use' or 'all'", EnumChatType.CommandError);
                }
                return;
            }

            var group = player.GetGroup(groupId);
            if (group == null)
            {
                player.SendMessage(groupId, "Type this command inside the group chat tab that you are a owner of", EnumChatType.CommandError);
                return;
            }
            if (group.Level != EnumPlayerGroupMemberShip.Owner)
            {
                player.SendMessage(groupId, "Must be owner of the group to change access flags", EnumChatType.CommandError);
                return;
            }

            ReinforcedPrivilegeGrantsGroup groupGrants;
            if (!privGrantsByOwningGroupUid.TryGetValue(groupId, out groupGrants))
            {
                privGrantsByOwningGroupUid[groupId] = groupGrants = new ReinforcedPrivilegeGrantsGroup();
            }

            if (firstarg == "default")
            {
                if (privtype == "grant")
                {
                    groupGrants.DefaultGrants = flags;
                    player.SendMessage(groupId, "Default access for group members set to " + flagString, EnumChatType.CommandError);
                } else
                {
                    groupGrants.DefaultGrants = 0;
                    player.SendMessage(groupId, "All access revoked for group members", EnumChatType.CommandError);
                }
                

                SyncPrivData();
                return;
            }

            if (privtype == "grant" || privtype == "revoke")
            {
                grantRevokeGroupOwned2Player(player, groupId, privtype, firstarg, flagString, flags, groupGrants);

                return;
            }

            if (privtype == "grantgroup" || privtype == "revokegroup")
            {
                grantRevokeGroupOwned2Group(player, groupId, privtype, firstarg, flags);
                return;
            }


            player.SendMessage(groupId, "Syntax: /gbre [grant|revoke|grantgroup|revokegroup] members or [playername/groupname] [use or all]", EnumChatType.CommandError);
            return;
        }

        protected void grantRevokeGroupOwned2Player(IServerPlayer player, int groupId, string privtype, string firstarg, string flagString, EnumBlockAccessFlags flags, ReinforcedPrivilegeGrantsGroup groupGrants)
        {
            (api as ICoreServerAPI).PlayerData.ResolvePlayerName(firstarg, (result, playeruid) =>
            {
                if (result == EnumServerResponse.Good)
                {
                    if (privtype == "grant")
                    {
                        groupGrants.PlayerGrants[playeruid] = flags;
                        player.SendMessage(groupId, flagString + " access set for player " + firstarg, EnumChatType.CommandError);
                        SyncPrivData();
                    }
                    else
                    {
                        if (groupGrants.PlayerGrants.Remove(playeruid))
                        {
                            player.SendMessage(groupId, "All access revoked for player " + firstarg, EnumChatType.CommandError);
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
                    player.SendMessage(groupId, Lang.Get("Player with name '{0}' is not online and auth server is offline. Cannot check if this player exists. Try again later.", firstarg), EnumChatType.CommandError);
                    return;
                }

                player.SendMessage(groupId, Lang.Get("No player with name '{0}' exists", firstarg), EnumChatType.CommandError);
            });
        }

        protected void grantRevokeGroupOwned2Group(IServerPlayer player, int sourceGroupId, string privtype, string targetGroupName, EnumBlockAccessFlags flags)
        {
            // [insert code here \o/]
        }

        private void Event_PlayerJoin(IServerPlayer byPlayer)
        {
            serverChannel?.SendPacket(new PrivGrantsData() { privGrantsByOwningPlayerUid = privGrantsByOwningPlayerUid, privGrantsByOwningGroupUid = privGrantsByOwningGroupUid }, byPlayer);
        }

        private void onCmd(IServerPlayer player, int groupId, CmdArgs args)
        {
            string subcmd = args.PopWord();
            string plrgrpname = args.PopWord();
            string flagString = args.PopWord();

            EnumBlockAccessFlags flags = EnumBlockAccessFlags.None;
            if (flagString != null) {
                if (flagString.ToLowerInvariant() == "use") flags = EnumBlockAccessFlags.Use;
                if (flagString.ToLowerInvariant() == "all") flags = EnumBlockAccessFlags.BuildOrBreak | EnumBlockAccessFlags.Use;
            }

            if (subcmd == null || plrgrpname == null)
            {
                player.SendMessage(groupId, "Syntax: /bre [grant|revoke|grantgroup|revokegroup] [playername/groupname] [use or all]", EnumChatType.CommandError);
                return;
            }

            ICoreServerAPI sapi = api as ICoreServerAPI;
            IServerPlayerData plrData = null;

            if (subcmd == "grant" || subcmd == "revoke")
            {
                plrData = sapi.PlayerData.GetPlayerDataByLastKnownName(plrgrpname);
                if (plrData == null)
                {
                    player.SendMessage(groupId, "No player with such name found or never connected to this server", EnumChatType.CommandError);
                    return;
                }
            }

 
            switch (subcmd)
            {
                case "grant":
                    if (flags == EnumBlockAccessFlags.None)
                    {
                        player.SendMessage(groupId, "Invalid or missing access flag. Declare 'use' or 'all'", EnumChatType.CommandError);
                        return;
                    }
                    SetPlayerPrivilege(player, groupId, plrData.PlayerUID, flags);
                    break;

                case "revoke":
                    SetPlayerPrivilege(player, groupId, plrData.PlayerUID, EnumBlockAccessFlags.None);
                    break;

                case "grantgroup":
                    if (flags == EnumBlockAccessFlags.None)
                    {
                        player.SendMessage(groupId, "Invalid or missing access flag. Declare 'use' or 'all'", EnumChatType.CommandError);
                        return;
                    }
                    SetGroupPrivilege(player, groupId, plrgrpname, flags);
                    break;

                case "revokegroup":
                    SetGroupPrivilege(player, groupId, plrgrpname, EnumBlockAccessFlags.None); ;
                    break;
            }
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
            if (!reasonableReinforcements && (block.BlockMaterial == EnumBlockMaterial.Plant ||
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

            saveReinforcments(reinforcmentsOfChunk, pos);
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
                if (bre.PlayerUID != byPlayer.PlayerUID || bre.Locked) return false;
                bre.Locked = true;
                bre.LockedByItemCode = itemCode;
                saveReinforcments(reinforcmentsOfChunk, pos);
                return true;
            }

            reinforcmentsOfChunk[index3d] = new BlockReinforcement() { PlayerUID = byPlayer.PlayerUID, LastPlayername = byPlayer.PlayerName, Strength = 0, Locked = true, LockedByItemCode = itemCode };

            saveReinforcments(reinforcmentsOfChunk, pos);

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
            saveReinforcments(reinforcmentsOfChunk, pos);
        }

        public void ClearReinforcement(BlockPos pos)
        {
            Dictionary<int, BlockReinforcement> reinforcmentsOfChunk = getOrCreateReinforcmentsAt(pos);
            if (reinforcmentsOfChunk == null) return;

            int index3d = toLocalIndex(pos);
            if (!reinforcmentsOfChunk.ContainsKey(index3d)) return;

            if (reinforcmentsOfChunk.Remove(index3d))
            {
                saveReinforcments(reinforcmentsOfChunk, pos);
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

            if (!api.World.BlockAccessor.GetBlock(pos).HasBehavior<BlockBehaviorReinforcable>()) return false;

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
            
            saveReinforcments(reinforcmentsOfChunk, pos);
            
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
                        saveReinforcments(reinforcmentsOfChunk, pos);

                        api.World.Logger.Warning("Ok, converted");

                    } catch (Exception e2)
                    {
                        api.World.Logger.VerboseDebug("Failed reading block reinforcments at block position {0}, will discard, sorry. Exception: {1}", pos, e2);
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

        void saveReinforcments(Dictionary<int, BlockReinforcement> reif, BlockPos pos)
        {
            int chunksize = api.World.BlockAccessor.ChunkSize;
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

        public void SetPlayerPrivilege(IServerPlayer owningPlayer, int chatGroupId, string forPlayerUid, EnumBlockAccessFlags access)
        {
            ReinforcedPrivilegeGrants grants;
            if (!privGrantsByOwningPlayerUid.TryGetValue(owningPlayer.PlayerUID, out grants))
            {
                grants = new ReinforcedPrivilegeGrants();
                privGrantsByOwningPlayerUid[owningPlayer.PlayerUID] = grants;
            }

            if (access == EnumBlockAccessFlags.None)
            {
                if (grants.PlayerGrants.Remove(forPlayerUid))
                {
                    owningPlayer.SendMessage(chatGroupId, Lang.Get("Ok, privilege revoked from player."), EnumChatType.CommandSuccess);
                } else
                {
                    owningPlayer.SendMessage(chatGroupId, Lang.Get("No action taken. Player does not have any privilege to your reinforced blocks."), EnumChatType.CommandSuccess);
                }
            } else
            {
                grants.PlayerGrants[forPlayerUid] = access;
                owningPlayer.SendMessage(chatGroupId, Lang.Get("Ok, Privilege for player set."), EnumChatType.CommandSuccess);
            }

            SyncPrivData();
        }

        public void SetGroupPrivilege(IServerPlayer owningPlayer, int chatGroupId, string forGroupName, EnumBlockAccessFlags access)
        {
            ReinforcedPrivilegeGrants grants;
            if (!privGrantsByOwningPlayerUid.TryGetValue(owningPlayer.PlayerUID, out grants))
            {
                grants = new ReinforcedPrivilegeGrants();
                privGrantsByOwningPlayerUid[owningPlayer.PlayerUID] = grants;
            }

            PlayerGroup group = (api as ICoreServerAPI).Groups.GetPlayerGroupByName(forGroupName);
            if (group == null)
            {
                owningPlayer.SendMessage(chatGroupId, Lang.Get("No such group found"), EnumChatType.CommandError);
                return;
            }

            if (access == EnumBlockAccessFlags.None)
            {
                if (grants.GroupGrants.Remove(group.Uid))
                {
                    owningPlayer.SendMessage(chatGroupId, Lang.Get("Ok, privilege revoked from group."), EnumChatType.CommandSuccess);
                }
                else
                {
                    owningPlayer.SendMessage(chatGroupId, Lang.Get("No action taken. Group does not have any privilege to your reinforced blocks."), EnumChatType.CommandSuccess);
                }
            }
            else
            {
                grants.GroupGrants[group.Uid] = access;
                owningPlayer.SendMessage(chatGroupId, Lang.Get("Ok, Privilege for group set."), EnumChatType.CommandSuccess);
            }

            SyncPrivData();
        }


        #endregion

        int toLocalIndex(BlockPos pos)
        {
            return toLocalIndex(pos.X % api.World.BlockAccessor.ChunkSize, pos.Y % api.World.BlockAccessor.ChunkSize, pos.Z % api.World.BlockAccessor.ChunkSize);
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
