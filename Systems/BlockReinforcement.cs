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
        public Dictionary<string, EnumBlockAccessFlags> PlayerGrants = new Dictionary<string, EnumBlockAccessFlags>();
        public Dictionary<int, EnumBlockAccessFlags> GroupGrants = new Dictionary<int, EnumBlockAccessFlags>();
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class PrivGrantsData
    {
        public Dictionary<string, ReinforcedPrivilegeGrants> privGrantsByOwningPlayerUid = new Dictionary<string, ReinforcedPrivilegeGrants>();
    }


    public class ModSystemBlockReinforcement : ModSystem
    {
        ICoreAPI api;

        IClientNetworkChannel clientChannel;
        IServerNetworkChannel serverChannel;

        // Client side data
        Dictionary<long, Dictionary<int, BlockReinforcement>> reinforcementsByChunk = new Dictionary<long, Dictionary<int, BlockReinforcement>>();

        // Both sided data
        Dictionary<string, ReinforcedPrivilegeGrants> privGrantsByOwningPlayerUid = new Dictionary<string, ReinforcedPrivilegeGrants>();

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

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            
            clientChannel = api.Network
                .RegisterChannel("blockreinforcement")
                .RegisterMessageType(typeof(ChunkReinforcementData))
                .RegisterMessageType(typeof(PrivGrantsData))
                .SetMessageHandler<ChunkReinforcementData>(onChunkData)
                .SetMessageHandler<PrivGrantsData>(onPrivData)
            ;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.ServerRunPhase(EnumServerRunPhase.LoadGamePre, addReinforcementBehavior);
            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;

            serverChannel = api.Network
                .RegisterChannel("blockreinforcement")
                .RegisterMessageType(typeof(ChunkReinforcementData))
                .RegisterMessageType(typeof(PrivGrantsData))
            ;

            api.RegisterCommand("bre", "Block reinforcement privilege management", "[grant|revoke|grantgroup|revokegroup] [playername/groupname] [use or all]", onCmd, Privilege.chat);
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
        }

        void SyncPrivData()
        {
            serverChannel?.BroadcastPacket(new PrivGrantsData() { privGrantsByOwningPlayerUid = privGrantsByOwningPlayerUid });
        }



        private void Event_GameWorldSave()
        {
            byte[] data = (api as ICoreServerAPI).WorldManager.SaveGame.GetData("blockreinforcementprivileges");
            if (data != null)
            {
                try
                {
                    privGrantsByOwningPlayerUid = SerializerUtil.Deserialize<Dictionary<string, ReinforcedPrivilegeGrants>>(data);
                } catch
                {
                    api.World.Logger.Notification("Unable to load group privileges for the block reinforcement system. Exception thrown when trying to deserialize it. Will be discarded.");
                }
            }
        }

        private void Event_SaveGameLoaded()
        {
            (api as ICoreServerAPI).WorldManager.SaveGame.StoreData("blockreinforcementprivileges", SerializerUtil.Serialize(privGrantsByOwningPlayerUid));
        }


        #region Reinforcing and Locking
        private void addReinforcementBehavior()
        {
            foreach (Block block in api.World.Blocks)
            {
                if (block.Code == null) continue;
                if (block.Attributes == null || block.Attributes["reinforcable"].AsBool(true) != false)
                {
                    block.BlockBehaviors = block.BlockBehaviors.Append(new BlockBehaviorReinforcable(block));
                }
            }
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
                    foundSlot = onSlot as ItemSlot;
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

            if (reinforcmentsOfChunk[index3d].PlayerUID != forPlayer.PlayerUID && (GetAccessFlags(reinforcmentsOfChunk[index3d].PlayerUID, forPlayer) & EnumBlockAccessFlags.BuildOrBreak) == 0)
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
                if (bre.Locked && bre.PlayerUID != forPlayer.PlayerUID)
                {
                    EnumBlockAccessFlags flags = GetAccessFlags(bre.PlayerUID, forPlayer);

                    if ((flags & EnumBlockAccessFlags.Use) > 0) return false;
                    return true;
                }
            }

            return false;
        }


        public EnumBlockAccessFlags GetAccessFlags(string owningPlayerUid, IPlayer forPlayer)
        {
            if (owningPlayerUid == forPlayer.PlayerUID) return EnumBlockAccessFlags.Use | EnumBlockAccessFlags.BuildOrBreak;

            ReinforcedPrivilegeGrants grants;
            EnumBlockAccessFlags flags = EnumBlockAccessFlags.None;

            if (privGrantsByOwningPlayerUid.TryGetValue(owningPlayerUid, out grants))
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

            reinforcmentsOfChunk[index3d].Strength--;
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


        public bool StrengthenBlock(BlockPos pos, IPlayer byPlayer, int strength)
        {
            if (api.Side == EnumAppSide.Client) return false;

            Dictionary<int, BlockReinforcement> reinforcmentsOfChunk = getOrCreateReinforcmentsAt(pos);

            int index3d = toLocalIndex(pos);

            if (reinforcmentsOfChunk.ContainsKey(index3d))
            {
                BlockReinforcement bre = reinforcmentsOfChunk[index3d];
                if (bre.Strength > 0) return false;
                bre.Strength = strength;
            } else
            {
                reinforcmentsOfChunk[index3d] = new BlockReinforcement() { PlayerUID = byPlayer.PlayerUID, LastPlayername = byPlayer.PlayerName, Strength = strength };
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
            
            Dictionary<int, BlockReinforcement> reinforcmentsOfChunk = null;

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
                        api.World.Logger.Error("Failed reading block reinforcments at block position {0}, will discard, sorry. Exception: {1}", pos, e2);
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

            long index3d = MapUtil.Index3dL(chunkX, chunkY, chunkZ, api.World.BlockAccessor.MapSizeX / chunksize, api.World.BlockAccessor.MapSizeZ / chunksize);
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
