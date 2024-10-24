using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using ProperVersion;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;
using static System.Reflection.Metadata.BlobBuilder;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// A small object to hold any necessary information about an individual player's current timeswitch state
    /// For now it only holds two binary states.  But in future it could have more information, like a cooldown period or the number of times it has already been activated, etc
    /// </summary>
    [ProtoContract]
    public class TimeSwitchState
    {
        /// <summary>
        /// If true, the timeswitch feature is available to this player, so the hotkey can be used
        /// </summary>
        [ProtoMember(1)]
        public bool Enabled;

        /// <summary>
        /// If true, the timeswitch is on, so the 'alt' world is shown
        /// </summary>
        [ProtoMember(2)]
        public bool Activated;

        [ProtoMember(3)]
        public string playerUID;

        [ProtoMember(4)]
        public int baseChunkX;
        [ProtoMember(5)]
        public int baseChunkZ;
        [ProtoMember(6)]
        public int size = 3;

        public TimeSwitchState()   // Parameter-less constructor used by NetworkChannel
        { }

        public TimeSwitchState(string uid)
        {
            playerUID = uid;
        }
    }



    /// <summary>
    /// A system for toggling (transporting) the player between two dimensions when a hotkey is pressed
    /// </summary>
    public class Timeswitch : ModSystem
    {
        const GlKeys TimeswitchHotkey = GlKeys.Y;
        const int OtherDimension = Dimensions.AltWorld;
        const double SquareRootOf2 = 1.41421356;


        // Server-side
        ICoreServerAPI sapi;
        IServerNetworkChannel serverChannel;
        Dictionary<string, TimeSwitchState> timeswitchStatesByPlayerUid = new Dictionary<string, TimeSwitchState>();
        bool dim2ChunksLoaded = false;
        bool loreEnabled = false;
        bool posEnabled = false;

        int baseChunkX;
        int baseChunkZ;
        int size = 3;
        int deactivateRadius = 2;

        Vec3d centerpos = new Vec3d();

        // Client-side
        ICoreClientAPI capi;
        IClientNetworkChannel clientChannel;




        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }


        public override void Start(ICoreAPI api)
        {
            base.Start(api);
        }


        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;

            capi.Input.RegisterHotKey("timeswitch", Lang.Get("Time switch"), TimeswitchHotkey, HotkeyType.CharacterControls);
            capi.Input.SetHotKeyHandler("timeswitch", OnHotkeyTimeswitch);

            clientChannel =
                api.Network.RegisterChannel("timeswitch")
               .RegisterMessageType(typeof(TimeSwitchState))
               .SetMessageHandler<TimeSwitchState>(OnStateReceived);
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            new CmdTimeswitch(api);

            this.sapi = api;
            loreEnabled = api.World.Config.GetBool("loreContent", true);
            if (!loreEnabled) return;

            api.Event.PlayerJoin += OnPlayerJoin;
            api.Event.SaveGameLoaded += OnSaveGameLoaded;
            api.Event.GameWorldSave += OnGameGettingSaved;

            serverChannel =
               api.Network.RegisterChannel("timeswitch")
               .RegisterMessageType(typeof(TimeSwitchState))
            ;

            api.Event.RegisterGameTickListener(PlayerEntryCheck, 500);
        }


        private void PlayerEntryCheck(float dt)
        {
            if (!posEnabled) return;

            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player.ConnectionState != EnumClientState.Playing) continue;

                if (WithinRange(player.Entity.ServerPos, deactivateRadius - 1))
                {
                    TimeSwitchState state;
                    if (!timeswitchStatesByPlayerUid.TryGetValue(player.PlayerUID, out state))
                    {
                        state = new TimeSwitchState(player.PlayerUID);
                        timeswitchStatesByPlayerUid[player.PlayerUID] = state;
                    }

                    if (!state.Enabled)
                    {
                        state.Enabled = true;
                        OnStartCommand(player);
                        player.SendMessage(GlobalConstants.GeneralChatGroup, "The seraph detects active temporal interference!", EnumChatType.Notification);
                        player.SendMessage(GlobalConstants.GeneralChatGroup, "You can press Y to activate the timeswitch", EnumChatType.Notification);
                    }
                }
                else if (player.Entity.ServerPos.Dimension == OtherDimension && !WithinRange(player.Entity.ServerPos, deactivateRadius))
                {
                    // Boot the player from the other dimension if has moved beyond deactivateRadius
                    ActivateTimeswitchServer(player);
                }
            }
        }


        private bool OnHotkeyTimeswitch(KeyCombination comb)
        {
            capi.SendChatMessage(string.Format("/timeswitch toggle"));
            return true;
        }


        private void OnGameGettingSaved()
        {
            //sapi.WorldManager.SaveGame.StoreData("timeswitchStates", SerializerUtil.Serialize(timeswitchStatesByPlayerUid
            int[] positions = new int[] { baseChunkX, baseChunkZ, size };
            sapi.WorldManager.SaveGame.StoreData("timeswitchPos", SerializerUtil.Serialize(positions));
        }


        private void OnSaveGameLoaded()
        {
            try
            {
                //byte[] data = sapi.WorldManager.SaveGame.GetData("timeswitchStates");

                //if (data != null) timeswitchStatesByPlayerUid = SerializerUtil.Deserialize<Dictionary<string, TimeSwitchState>>(data);
            }
            catch (Exception e)
            {
                sapi.World.Logger.Error("Failed loading timeswitchStates. Resetting.");
                sapi.World.Logger.Error(e);
                timeswitchStatesByPlayerUid = null;
            }
            if (timeswitchStatesByPlayerUid == null) timeswitchStatesByPlayerUid = new Dictionary<string, TimeSwitchState>();

            try
            {
                byte[] data = sapi.WorldManager.SaveGame.GetData("timeswitchPos");

                if (data != null)
                {
                    int[] positions = SerializerUtil.Deserialize<int[]>(data);
                    if (positions.Length >= 3)
                    {
                        baseChunkX = positions[0];
                        baseChunkZ = positions[1];
                        size = positions[2];

                        SetupCenterPos();

                        // We do not currently support more than one timeswitch position in a single map, maybe we can in future...
                    }
                }
            }
            catch (Exception e)
            {
                sapi.World.Logger.Error("Failed loading timeswitchPos. Maybe not yet worldgenned, or else use /timeswitch setpos to set it manually.");
                sapi.World.Logger.Error(e);
            }
        }


        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            TimeSwitchState state;
            if (timeswitchStatesByPlayerUid.TryGetValue(byPlayer.PlayerUID, out state))
            {
                //serverChannel.SendPacket(state, byPlayer);
            }
            else timeswitchStatesByPlayerUid[byPlayer.PlayerUID] = new TimeSwitchState(byPlayer.PlayerUID);

            if (byPlayer.Entity.Pos.Dimension == OtherDimension)
            {
                OnStartCommand(byPlayer);
            }
        }


        /// <summary>
        /// To be called when a player first enters a timeswitch region: it activates the region (loading alt-chunks) and pre-sends the alt dimension to the player
        /// </summary>
        /// <param name="player"></param>
        public void OnStartCommand(IServerPlayer player)
        {
            if (!loreEnabled || !posEnabled) return;

            LoadChunkColumns();
            if (player != null) ForceSendChunkColumns(player);
        }


        /// <summary>
        /// Client-side switching
        /// </summary>
        /// <param name="tsState"></param>
        private void ActivateTimeswitchClient(TimeSwitchState tsState)
        {
            EntityPlayer player = capi.World.Player.Entity;
            player.ChangeDimension(tsState.Activated ? OtherDimension : Dimensions.NormalWorld);
        }


        private bool WithinRange(EntityPos pos, int radius)
        {
            return pos.HorDistanceTo(centerpos) < radius;
        }


        /// <summary>
        /// Server-side switching
        /// </summary>
        /// <param name="byPlayer"></param>
        public void ActivateTimeswitchServer(IServerPlayer byPlayer)
        {
            if (!loreEnabled || !posEnabled) return;

            if (byPlayer.Entity.ServerPos.Dimension == Dimensions.NormalWorld)
            {
                // Prevent activation of hotkey if too far from position
                if (!WithinRange(byPlayer.Entity.ServerPos, deactivateRadius)) return;
            }

            TimeSwitchState tsState;
            if (timeswitchStatesByPlayerUid.TryGetValue(byPlayer.PlayerUID, out tsState))
            {
                tsState.Activated = byPlayer.Entity.Pos.Dimension == Dimensions.NormalWorld;
                byPlayer.Entity.ChangeDimension(tsState.Activated ? OtherDimension : Dimensions.NormalWorld);

                // Send to client:
                tsState.baseChunkX = baseChunkX;
                tsState.baseChunkZ = baseChunkZ;
                tsState.size = size;
                serverChannel.BroadcastPacket(tsState);
            }
        }


        private void OnStateReceived(TimeSwitchState state)
        {
            if (capi.World?.Player == null) return;

            if (state.playerUID == capi.World.Player.PlayerUID)
            {
                ActivateTimeswitchClient(state);
                MakeChunkColumnsVisible(state.baseChunkX, state.baseChunkZ, state.size, state.Activated ? OtherDimension : Dimensions.NormalWorld);
            }
            else
            {
                var otherPlayer = capi.World.PlayerByUid(state.playerUID);
                otherPlayer?.Entity?.ChangeDimension(state.Activated ? OtherDimension : Dimensions.NormalWorld);
            }
        }


        public void SetPos(BlockPos pos)
        {
            baseChunkX = pos.X / GlobalConstants.ChunkSize;
            baseChunkZ = pos.Z / GlobalConstants.ChunkSize;

            SetupCenterPos();
        }


        private void SetupCenterPos()
        {
            centerpos.Set(baseChunkX * GlobalConstants.ChunkSize + GlobalConstants.ChunkSize / 2, 0, baseChunkZ * GlobalConstants.ChunkSize + GlobalConstants.ChunkSize / 2);
            deactivateRadius = (size - 1) * GlobalConstants.ChunkSize + 1;

            posEnabled = true;
        }


        public void CopyBlocksToAltDimension(IBlockAccessor sourceblockAccess, IServerPlayer player)
        {
            if (!loreEnabled || !posEnabled) return;

            BlockPos start = new BlockPos((baseChunkX - size + 1) * GlobalConstants.ChunkSize, 0, (baseChunkZ - size + 1) * GlobalConstants.ChunkSize);
            BlockPos end = start.AddCopy(GlobalConstants.ChunkSize * (size * 2 - 1), 0, GlobalConstants.ChunkSize * (size * 2 - 1));
            start.Y = Math.Max(0, (sapi.World.SeaLevel - 8) / GlobalConstants.ChunkSize * GlobalConstants.ChunkSize);
            end.Y = Math.Min(sapi.WorldManager.MapSizeY, ((sapi.World.SeaLevel + 8) / GlobalConstants.ChunkSize + 2) * GlobalConstants.ChunkSize);
            BlockSchematic blocks = new BlockSchematic(sapi.World, sourceblockAccess, start, end, false);

            CreateChunkColumns();

            BlockPos originPos = start.AddCopy(0, OtherDimension * BlockPos.DimensionBoundary, 0);
            var blockAccess = sapi.World.BlockAccessor;
            blocks.Init(blockAccess);
            blocks.Place(blockAccess, sapi.World, originPos, EnumReplaceMode.ReplaceAll, true);
            blocks.PlaceDecors(blockAccess, originPos);

            if (player != null)
            {
                start.dimension = OtherDimension;
                end.dimension = OtherDimension;
                sapi.WorldManager.FullRelight(start, end, false);

                ForceSendChunkColumns(player);
            }
        }


        private void CreateChunkColumns()
        {
            for (int x = 0; x < size * 2 - 1; x++)
            {
                for (int z = 0; z < size * 2 - 1; z++)
                {
                    int cx = baseChunkX - size + 1 + x;
                    int cz = baseChunkZ - size + 1 + z;

                    sapi.WorldManager.CreateChunkColumnForDimension(cx, cz, OtherDimension);
                }
            }
        }


        public void LoadChunkColumns()
        {
            if (!loreEnabled || !posEnabled) return;

            if (dim2ChunksLoaded) return;
            dim2ChunksLoaded = true;

            for (int x = 0; x < size * 2 - 1; x++)
            {
                for (int z = 0; z < size * 2 - 1; z++)
                {
                    int cx = baseChunkX - size + 1 + x;
                    int cz = baseChunkZ - size + 1 + z;

                    // Ultimately we may need to add a test here to detect whether the chunk columns in the alt dimension are already loaded, otherwise there can be duplication in a multiplayer game

                    sapi.WorldManager.LoadChunkColumnForDimension(cx, cz, OtherDimension);
                }
            }
        }


        private void ForceSendChunkColumns(IServerPlayer player)
        {
            if (!loreEnabled || !posEnabled) return;

            for (int x = 0; x < size * 2 - 1; x++)
            {
                for (int z = 0; z < size * 2 - 1; z++)
                {
                    int cx = baseChunkX - size + 1 + x;
                    int cz = baseChunkZ - size + 1 + z;

                    sapi.WorldManager.ForceSendChunkColumn(player, cx, cz, OtherDimension);
                }
            }
        }


        /// <summary>
        /// Client-side
        /// </summary>
        private void MakeChunkColumnsVisible(int baseChunkX, int baseChunkZ, int size, int dimension)
        {
            for (int x = 0; x < size * 2 - 1; x++)
            {
                for (int z = 0; z < size * 2 - 1; z++)
                {
                    int cx = baseChunkX - size + 1 + x;
                    int cz = baseChunkZ - size + 1 + z;

                    capi.World.SetChunkColumnVisible(cx, cz, dimension);
                }
            }
        }

        StoryStructureLocation GenStoryStructLoc;
        GenStoryStructures GenGenStoryStructures;
        /// <summary>
        /// Called to set up the devastationLocation 
        /// </summary>
        /// <param name="structureLocation"></param>
        /// <param name="genStoryStructures"></param>
        public void InitPotentialGeneration(StoryStructureLocation structureLocation, GenStoryStructures genStoryStructures)
        {
            GenStoryStructLoc = structureLocation;
            GenGenStoryStructures = genStoryStructures;
        }

        public void AttemptGeneration(IWorldGenBlockAccessor worldgenBlockAccessor)
        {
            if (GenStoryStructLoc == null || GenStoryStructLoc.DidGenerateAdditional) return;

            if (!AreAllDim0ChunksGenerated()) return;

            CopyBlocksToAltDimension(worldgenBlockAccessor, null);

            PlaceSchematic(worldgenBlockAccessor, Dimensions.NormalWorld, "story/" + GenStoryStructLoc.Code + "-present");
            PlaceSchematic(sapi.World.BlockAccessor, OtherDimension, "story/" + GenStoryStructLoc.Code + "-past");

            if (size > 1)  // FullRelight extends 1 chunk out in all directions, and fails if null chunks found
            {
                BlockPos start = new BlockPos((baseChunkX - size + 2) * GlobalConstants.ChunkSize, 0, (baseChunkZ - size + 2) * GlobalConstants.ChunkSize, OtherDimension);
                BlockPos end = start.AddCopy(GlobalConstants.ChunkSize * (size * 2 - 3) - 1, sapi.WorldManager.MapSizeY, GlobalConstants.ChunkSize * (size * 2 - 3) - 1);
                start.Y = (sapi.World.SeaLevel - 8) / GlobalConstants.ChunkSize * GlobalConstants.ChunkSize;
                sapi.WorldManager.FullRelight(start, end, false);
            }

            GenStoryStructLoc.DidGenerateAdditional = true;
            GenGenStoryStructures.StoryStructureInstancesDirty = true;

            //TODO: add protection to dim2
        }

        private void PlaceSchematic(IBlockAccessor blockAccessor, int dim, string genSchematicName)
        {
            BlockSchematicPartial blocks = LoadSchematic(sapi, genSchematicName);
            if (blocks == null) return;

            blocks.InitMetaBlocks(blockAccessor);
            blocks.Init(blockAccessor);
            blocks.blockLayerConfig = GenGenStoryStructures.blockLayerConfig;

            BlockPos start = new BlockPos(baseChunkX * GlobalConstants.ChunkSize + GlobalConstants.ChunkSize / 2, 0, baseChunkZ * GlobalConstants.ChunkSize + GlobalConstants.ChunkSize / 2);
            start.Y = blockAccessor.GetRainMapHeightAt(start);
            start.Y += dim * BlockPos.DimensionBoundary;

            blocks.Place(blockAccessor, sapi.World, start, EnumReplaceMode.ReplaceAll, true);
            blocks.PlaceDecors(blockAccessor, start);

            //start.Sub(GlobalConstants.ChunkSize * (size - 1) + GlobalConstants.ChunkSize / 2, 0, GlobalConstants.ChunkSize * (size - 1) + GlobalConstants.ChunkSize / 2);
            //BlockPos end = start.AddCopy(GlobalConstants.ChunkSize * (size * 2 - 1), sapi.WorldManager.MapSizeY, GlobalConstants.ChunkSize * (size * 2 - 1));
            //sapi.WorldManager.FullRelight(start, end, false);
        }

        private bool AreAllDim0ChunksGenerated()
        {
            for (int cx = baseChunkX - size + 1; cx < baseChunkX + size; cx++)
            {
                for (int cz = baseChunkZ - size + 1; cz < baseChunkZ + size; cz++)
                {
                    IMapChunk mc = sapi.World.BlockAccessor.GetMapChunk(cx, cz);
                    if (mc == null) return false;
                    if (mc.CurrentPass <= EnumWorldGenPass.Vegetation) return false;   // Vegetation needs to be complete, as that involves placement of BlockPatches ie. surface clutter
                }
            }

            return true;
        }

        private BlockSchematicPartial LoadSchematic(ICoreServerAPI sapi, string schematicName)
        {
            IAsset asset = sapi.Assets.Get(new AssetLocation("worldgen/schematics/" + schematicName + ".json"));
            if (asset == null) return null;

            BlockSchematicPartial schematic = asset.ToObject<BlockSchematicPartial>();
            if (schematic == null)
            {
                sapi.World.Logger.Warning("Could not load timeswitching schematic {0}", schematicName);
                return null;
            }

            schematic.FromFileName = asset.Name;
            return schematic;
        }
    }
}
