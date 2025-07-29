using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

#nullable disable

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
        [ProtoMember(7)]
        public int forcedY = 0;
        [ProtoMember(8)]
        public string failureReason = "";

        public TimeSwitchState() { }  // Parameter-less constructor used by NetworkChannel

        public TimeSwitchState(string uid)
        {
            playerUID = uid;
        }
    }

    [ProtoContract]
    public class DimensionSwitchForEntity
    {
        [ProtoMember(1)]
        public long entityId;

        [ProtoMember(2)]
        public int dimension;

        public DimensionSwitchForEntity() { }  // Parameter-less constructor used by NetworkChannel

        public DimensionSwitchForEntity(long entityId, int dimension)
        {
            this.entityId = entityId;
            this.dimension = dimension;
        }
    }


    public class ItemSkillTimeswitch : Item, ISkillItemRenderer
    {
        LoadedTexture iconTex;
        ICoreClientAPI capi;

        public static float timeSwitchCooldown;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (api is ICoreClientAPI capi)
            {
                UseTimeSwitchSkillClient(capi);
            }
        }

        public static void UseTimeSwitchSkillClient(ICoreClientAPI capi)
        {
            if (timeSwitchCooldown > 0) return;

            capi.SendChatMessage("/timeswitch toggle");
            capi.World.AddCameraShake(0.25f);
            timeSwitchCooldown = Timeswitch.CooldownTime / (capi.World.Player.WorldData.CurrentGameMode != EnumGameMode.Survival ? 3 : 1);  // reduce the cooldown in Creative mode
        }

        public override void OnLoaded(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client) return;

            capi = api as ICoreClientAPI;

            if (Attributes?["iconPath"].Exists == true)
            {
                var iconloc = AssetLocation.Create(Attributes["iconPath"].ToString(), Code.Domain).WithPathPrefix("textures/");
                iconTex = ObjectCacheUtil.GetOrCreate(api, "skillicon-" + Code, () =>
                {
                    return capi.Gui.LoadSvgWithPadding(iconloc, 64, 64, 5, ColorUtil.WhiteArgb);
                });
            }

            base.OnLoaded(api);
        }

        ElementBounds renderBounds = new ElementBounds();
        public void Render(float dt, float x, float y, float z)
        {
            if (capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Spectator) return;

            float shakex = ((float)capi.World.Rand.NextDouble() * 60 - 30) * Math.Max(0, timeSwitchCooldown - Timeswitch.CooldownTime * 0.8f);
            float shakey = ((float)capi.World.Rand.NextDouble() * 60 - 30) * Math.Max(0, timeSwitchCooldown - Timeswitch.CooldownTime * 0.8f);

            x += shakex;
            y += shakey;

            float guiscale = 8f / 13f * RuntimeEnv.GUIScale;
            capi.Render.Render2DTexture(iconTex.TextureId, x, y, iconTex.Width * guiscale, iconTex.Height * guiscale, z);

            double delta = iconTex.Height * 8f / 13f * GameMath.Clamp(timeSwitchCooldown / Timeswitch.CooldownTime * 2.5f, 0, 1);

            renderBounds.ParentBounds = capi.Gui.WindowBounds;
            renderBounds.fixedX = x / RuntimeEnv.GUIScale;
            renderBounds.fixedY = y / RuntimeEnv.GUIScale + delta;
            renderBounds.fixedWidth = iconTex.Width * 8f / 13f;
            renderBounds.fixedHeight = iconTex.Height * 8f / 13f - delta;
            renderBounds.CalcWorldBounds();


            capi.Render.PushScissor(renderBounds);

            var col = new Vec4f((float)GuiStyle.ColorTime1[0], (float)GuiStyle.ColorTime1[1], (float)GuiStyle.ColorTime1[2], (float)GuiStyle.ColorTime1[3]);
            capi.Render.Render2DTexture(iconTex.TextureId, x, y, iconTex.Width * guiscale, iconTex.Height * guiscale, z, col);

            timeSwitchCooldown = Math.Max(0, timeSwitchCooldown - dt);

            capi.Render.PopScissor();

            capi.Render.CheckGlError();
        }
    }


    /// <summary>
    /// A system for toggling (transporting) the player between two dimensions when a hotkey is pressed
    /// </summary>
    public class Timeswitch : ModSystem
    {
        public const float CooldownTime = 3f;

        const GlKeys TimeswitchHotkey = GlKeys.Y;
        const int OtherDimension = Dimensions.AltWorld;


        // Server-side
        ICoreServerAPI sapi;
        IServerNetworkChannel serverChannel;
        Dictionary<string, TimeSwitchState> timeswitchStatesByPlayerUid = new Dictionary<string, TimeSwitchState>();
        bool dim2ChunksLoaded = false;
        bool allowTimeswitch = false;
        bool posEnabled = false;

        int baseChunkX;
        int baseChunkZ;
        int size = 3;
        int deactivateRadius = 2;

        Vec3d centerpos = new Vec3d();

        CollisionTester collTester;

        // Client-side
        ICoreClientAPI capi;
        IClientNetworkChannel clientChannel;




        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }


        public override void Start(ICoreAPI api)
        {
            allowTimeswitch = api.World.Config.GetBool("loreContent", true) || api.World.Config.GetBool("allowTimeswitch", false);
        }


        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            capi.Input.RegisterHotKey("timeswitch", Lang.Get("Time switch"), TimeswitchHotkey, HotkeyType.CharacterControls);
            capi.Input.SetHotKeyHandler("timeswitch", OnHotkeyTimeswitch);

            clientChannel =
                api.Network.RegisterChannel("timeswitch")
               .RegisterMessageType(typeof(TimeSwitchState))
               .SetMessageHandler<TimeSwitchState>(OnStateReceived)
               .RegisterMessageType(typeof(DimensionSwitchForEntity))
               .SetMessageHandler<DimensionSwitchForEntity>(OnEntityDimensionSwitchPacketReceived);
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            new CmdTimeswitch(api);

            sapi = api;
            if (!allowTimeswitch) return;

            api.Event.PlayerJoin += OnPlayerJoin;
            api.Event.SaveGameLoaded += OnSaveGameLoaded;
            api.Event.GameWorldSave += OnGameGettingSaved;

            serverChannel =
               api.Network.RegisterChannel("timeswitch")
               .RegisterMessageType(typeof(TimeSwitchState))
               .RegisterMessageType(typeof(DimensionSwitchForEntity))
            ;

            api.Event.RegisterGameTickListener(PlayerEntryCheck, 500);

            collTester = new CollisionTester();
        }


        private void PlayerEntryCheck(float dt)
        {
            if (!posEnabled) return;

            var skillStack = new ItemStack(sapi.World.GetItem("timeswitch"));

            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player.ConnectionState != EnumClientState.Playing) continue;

                var skillSlot = player.InventoryManager.GetHotbarInventory()[10];

                if (WithinRange(player.Entity.ServerPos, deactivateRadius - 1))
                {
                    if (!timeswitchStatesByPlayerUid.TryGetValue(player.PlayerUID, out TimeSwitchState state))
                    {
                        state = new TimeSwitchState(player.PlayerUID);
                        timeswitchStatesByPlayerUid[player.PlayerUID] = state;
                    }

                    if (skillSlot.Empty && player.Entity.Alive)
                    {
                        skillSlot.Itemstack = skillStack;
                        skillSlot.MarkDirty();
                    }

                    if (!state.Enabled)
                    {
                        state.Enabled = true;
                        OnStartCommand(player);
                        player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.GetL(player.LanguageCode, "message-timeswitch-detected"), EnumChatType.Notification);
                        player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.GetL(player.LanguageCode, "message-timeswitch-controls"), EnumChatType.Notification);
                    }
                }
                else if (!WithinRange(player.Entity.ServerPos, deactivateRadius))
                {
                    if (!skillSlot.Empty)
                    {
                        skillSlot.Itemstack = null;
                        skillSlot.MarkDirty();
                    }

                    if (player.Entity.ServerPos.Dimension == OtherDimension)
                    {
                        // Boot the player from the other dimension if has moved beyond deactivateRadius
                        ActivateTimeswitchServer(player, true, out string ignore);
                    }

                    if (!timeswitchStatesByPlayerUid.TryGetValue(player.PlayerUID, out TimeSwitchState state))
                    {
                        state = new TimeSwitchState(player.PlayerUID);
                        timeswitchStatesByPlayerUid[player.PlayerUID] = state;
                    }
                    state.Enabled = false;
                }
            }
        }


        private bool OnHotkeyTimeswitch(KeyCombination comb)
        {
            ItemSkillTimeswitch.UseTimeSwitchSkillClient(capi);
            return true;
        }


        private void OnGameGettingSaved()
        {
            int[] positions = new int[] { baseChunkX, baseChunkZ, size };
            sapi.WorldManager.SaveGame.StoreData("timeswitchPos", SerializerUtil.Serialize(positions));
        }


        private void OnSaveGameLoaded()
        {
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
            if (timeswitchStatesByPlayerUid.TryGetValue(byPlayer.PlayerUID, out TimeSwitchState state))
            {
                //serverChannel.SendPacket(state, byPlayer);
            }
            else timeswitchStatesByPlayerUid[byPlayer.PlayerUID] = new TimeSwitchState(byPlayer.PlayerUID);

            if (byPlayer.Entity.Pos.Dimension == OtherDimension)
            {
                OnStartCommand(byPlayer);
                int cx = (int)byPlayer.Entity.Pos.X / GlobalConstants.ChunkSize;
                int cy = (int)byPlayer.Entity.Pos.Y / GlobalConstants.ChunkSize;
                int cz = (int)byPlayer.Entity.Pos.Z / GlobalConstants.ChunkSize;
                sapi.WorldManager.SendChunk(cx, cy, cz, byPlayer, false);   // force send one chunk in the normal world, to send the MapRegion, for the structure permissions check system
            }
        }


        /// <summary>
        /// To be called when a player first enters a timeswitch region: it activates the region (loading alt-chunks) and pre-sends the alt dimension to the player
        /// </summary>
        /// <param name="player"></param>
        public void OnStartCommand(IServerPlayer player)
        {
            if (!allowTimeswitch || !posEnabled) return;

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
            if (tsState.forcedY != 0) player.SidedPos.Y = tsState.forcedY;
            player.ChangeDimension(tsState.Activated ? OtherDimension : Dimensions.NormalWorld);
        }


        private bool WithinRange(EntityPos pos, int radius)
        {
            return pos.HorDistanceTo(centerpos) < radius;
        }


        /// <summary>
        /// Server-side switching
        /// </summary>
        /// <param name="player"></param>
        /// <param name="raiseToWorldSurface">If true, ensure the player is lifted up to be on top of a solid block; if false the switching will be disabled if the position in the other dim is impossible</param>
        /// <param name="failurereason"></param>
        public bool ActivateTimeswitchServer(IServerPlayer player, bool raiseToWorldSurface, out string failurereason)
        {
            bool result = ActivateTimeswitchInternal(player, raiseToWorldSurface, out failurereason);

            if (!result && failurereason != null)
            {
                TimeSwitchState tempState = new TimeSwitchState();
                tempState.failureReason = failurereason;
                serverChannel.SendPacket(tempState, player);
            }

            return result;
        }

        private bool ActivateTimeswitchInternal(IServerPlayer byPlayer, bool forced, out string failurereason)
        {
            failurereason = null;
            if (!allowTimeswitch || !posEnabled) return false;

            if (byPlayer.Entity.MountedOn != null)
            {
                failurereason = "mounted";
                return false;
            }

            if (byPlayer.Entity.ServerPos.Dimension == Dimensions.NormalWorld)
            {
                if (!timeswitchStatesByPlayerUid.TryGetValue(byPlayer.PlayerUID, out TimeSwitchState state))
                {
                    state = new TimeSwitchState(byPlayer.PlayerUID);
                    timeswitchStatesByPlayerUid[byPlayer.PlayerUID] = state;
                }
                if (!state.Enabled) return false;    // No error message in this case, the player is just a long way from the timeswitch and it has not yet been enabled

                // Prevent activation of hotkey if too far from position (or if dim2 is not yet loaded)
                if (!WithinRange(byPlayer.Entity.ServerPos, deactivateRadius))
                {
                    failurereason = "outofrange";
                    return false;
                }

                if (!dim2ChunksLoaded)
                {
                    failurereason = "wait";
                    return false;
                }

                if (genStoryStructLoc != null && !genStoryStructLoc.DidGenerateAdditional)    // genStoryStructLoc is null in a Creative Flat world
                {
                    failurereason = "wait";
                    return false;   // Not yet finished generating
                }
            }

            bool forceYToWorldSurface = forced;
            if (genStoryStructLoc != null)
            {
                double distanceFromTowerX = Math.Max(0, Math.Abs(byPlayer.Entity.ServerPos.X - genStoryStructLoc.CenterPos.X - 0.5) - 9.5);
                double distanceFromTowerZ = Math.Max(0, Math.Abs(byPlayer.Entity.ServerPos.Z - genStoryStructLoc.CenterPos.Z - 0.5) - 9.5);
                int towerBlocksConeHeightY = storyTowerBaseY + (int)Math.Max(distanceFromTowerX, distanceFromTowerZ) * 3;    // 9.5 and 3 are values based on the 1.20 design of the exploded Devastation Area Tower
                                                                                                                             // Only raiseToWorldSurface outside the tower: either forced transition (on edge of active area) or player y-height is at or below a certain cone, and player is neither flying, gliding nor falling fast
                forced |= byPlayer.Entity.ServerPos.Y <= towerBlocksConeHeightY && !byPlayer.Entity.Controls.IsFlying && !byPlayer.Entity.Controls.Gliding && byPlayer.Entity.ServerPos.Motion.Y > EntityBehaviorHealth.FallDamageYMotionThreshold;
            }

            bool farFromTimeswitch = !WithinRange(byPlayer.Entity.ServerPos, deactivateRadius + 2 * GlobalConstants.ChunkSize);
            int targetDimension = byPlayer.Entity.Pos.Dimension == Dimensions.NormalWorld ? OtherDimension : Dimensions.NormalWorld;

            if (timeswitchStatesByPlayerUid.TryGetValue(byPlayer.PlayerUID, out TimeSwitchState tsState))
            {
                tsState.forcedY = 0;   // Reset this to 0 before anything else, to prevent perma-falling!

                // First check that the timeswitch can be used just here, and/or move the player (outside the tower) to the surface of the other world
                // ... but we should not do either of things if the player is already far from the Timeswitch pos (e.g. re-spawning player after death in dim2)

                if (byPlayer.WorldData.CurrentGameMode == EnumGameMode.Survival && !farFromTimeswitch)
                {
                    // If feet not on ground: we still force if would otherwise collide
                    if (forceYToWorldSurface && (byPlayer.Entity.OnGround || OtherDimensionPositionWouldCollide(byPlayer.Entity, targetDimension, false)))
                    {
                        RaisePlayerToTerrainSurface(byPlayer.Entity, targetDimension, tsState);
                    }
                    else if (OtherDimensionPositionWouldCollide(byPlayer.Entity, targetDimension, true))
                    {
                        failurereason = "blocked";
                        return false;
                    }
                }

                tsState.Activated = targetDimension == OtherDimension;
                byPlayer.Entity.ChangeDimension(targetDimension);

                // Send to client:
                tsState.baseChunkX = baseChunkX;
                tsState.baseChunkZ = baseChunkZ;
                tsState.size = size;
                serverChannel.BroadcastPacket(tsState);

                spawnTeleportParticles(byPlayer.Entity.ServerPos);


                return true;
            }

            return false;
        }

        private void spawnTeleportParticles(EntityPos pos)
        {
            int r = 53;
            int g = 221;
            int b = 172;

            var teleportParticles = new SimpleParticleProperties(
                150, 200,
                (r << 16) | (g << 8) | (b << 0) | (100 << 24),
                new Vec3d(pos.X - 0.5, pos.Y, pos.Z - 0.5),
                new Vec3d(pos.X + 0.5, pos.Y + 1.8, pos.Z + 0.5),
                new Vec3f(-0.7f, -0.7f, -0.7f),
                new Vec3f(1.4f, 1.4f, 1.4f),
                2f,
                0,
                0.1f,
                0.2f,
                EnumParticleModel.Quad
            );

            teleportParticles.addLifeLength = 1f;
            teleportParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -10f);

            int dim = pos.Dimension;
            // Spawn in dim 1
            sapi.World.SpawnParticles(teleportParticles);
            sapi.World.PlaySoundAt(new AssetLocation("sounds/effect/timeswitch"), pos.X, pos.Y, pos.Z, null, false, 16);

            // Spawn in dim 2
            teleportParticles.MinPos.Y += dim * BlockPos.DimensionBoundary;
            sapi.World.SpawnParticles(teleportParticles);
            sapi.World.PlaySoundAt(new AssetLocation("sounds/effect/timeswitch"), pos.X, pos.Y + dim * BlockPos.DimensionBoundary, pos.Z, null, false, 16);
        }


        private void OnStateReceived(TimeSwitchState state)
        {
            if (capi.World?.Player == null) return;

            if (state.failureReason.Length > 0)
            {
                capi.TriggerIngameError(capi.World, state.failureReason, Lang.Get("ingameerror-timeswitch-" + state.failureReason));
                if (state.failureReason == "blocked") ItemSkillTimeswitch.timeSwitchCooldown = 0;
                return;
            }

            if (state.playerUID == capi.World.Player.PlayerUID)
            {
                ActivateTimeswitchClient(state);
                MakeChunkColumnsVisibleClient(state.baseChunkX, state.baseChunkZ, state.size, state.Activated ? OtherDimension : Dimensions.NormalWorld);
            }
            else
            {
                var otherPlayer = capi.World.PlayerByUid(state.playerUID);
                otherPlayer?.Entity?.ChangeDimension(state.Activated ? OtherDimension : Dimensions.NormalWorld);
            }
        }

        public void ChangeEntityDimensionOnClient(Entity entity, int dimension)
        {
            serverChannel.BroadcastPacket(new DimensionSwitchForEntity(entity.EntityId, dimension));
        }

        private void OnEntityDimensionSwitchPacketReceived(DimensionSwitchForEntity packet)
        {
            Entity entity = capi.World.GetEntityById(packet.entityId);

            if (entity == null) return;

            entity.Pos.Dimension = packet.dimension;
            entity.ServerPos.Dimension = packet.dimension;

            long newchunkindex3d = capi.World.ChunkProvider.ChunkIndex3D(entity.Pos);
            capi.World.UpdateEntityChunk(entity, newchunkindex3d);
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


        /// <summary>
        /// Called by '/timeswitch copy' command
        /// </summary>
        /// <param name="sourceblockAccess"></param>
        /// <param name="player"></param>
        public void CopyBlocksToAltDimension(IBlockAccessor sourceblockAccess, IServerPlayer player)
        {
            if (!allowTimeswitch || !posEnabled) return;

            BlockPos start = new BlockPos((baseChunkX - size + 1) * GlobalConstants.ChunkSize, 0, (baseChunkZ - size + 1) * GlobalConstants.ChunkSize);
            BlockPos end = start.AddCopy(GlobalConstants.ChunkSize * (size * 2 - 1), 0, GlobalConstants.ChunkSize * (size * 2 - 1));
            start.Y = Math.Max(0, (sapi.World.SeaLevel - 8) / GlobalConstants.ChunkSize * GlobalConstants.ChunkSize);
            end.Y = sapi.WorldManager.MapSizeY;
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


        /// <summary>
        /// Called by '/timeswitch relight' command
        /// </summary>
        /// <param name="sourceblockAccess"></param>
        /// <param name="player"></param>
        public void RelightCommand(IBlockAccessor sourceblockAccess, IServerPlayer player)
        {
            RelightAltDimension();
            ForceSendChunkColumns(player);
        }


        private void CreateChunkColumns()
        {
            for (int x = 0; x <= size * 2; x++)
            {
                for (int z = 0; z <= size * 2; z++)
                {
                    int cx = baseChunkX - size + x;
                    int cz = baseChunkZ - size + z;

                    sapi.WorldManager.CreateChunkColumnForDimension(cx, cz, OtherDimension);
                }
            }
        }


        public void LoadChunkColumns()
        {
            if (!allowTimeswitch || !posEnabled) return;

            if (dim2ChunksLoaded) return;

            for (int x = 0; x < size * 2 - 1; x++)
            {
                for (int z = 0; z < size * 2 - 1; z++)
                {
                    int cx = baseChunkX - size + 1 + x;
                    int cz = baseChunkZ - size + 1 + z;

                    // Ultimately we may need to add a test here to detect whether the individual chunk columns in the alt dimension are already loaded, otherwise there can be duplication in a multiplayer game
                    sapi.WorldManager.LoadChunkColumnForDimension(cx, cz, OtherDimension);
                }
            }

            dim2ChunksLoaded = true;
        }


        private void ForceSendChunkColumns(IServerPlayer player)
        {
            if (!allowTimeswitch || !posEnabled) return;

            int maxSize = size * 2;
            int czBase = baseChunkZ - size;
            for (int x = 0; x <= maxSize; x++)
            {
                int cx = baseChunkX - size + x;
                for (int z = 0; z <= maxSize; z++)
                {
                    sapi.WorldManager.ForceSendChunkColumn(player, cx, czBase + z, OtherDimension);
                }
            }
        }


        /// <summary>
        /// Client-side
        /// </summary>
        private void MakeChunkColumnsVisibleClient(int baseChunkX, int baseChunkZ, int size, int dimension)
        {
            for (int x = 0; x <= size * 2; x++)
            {
                for (int z = 0; z <= size * 2; z++)
                {
                    int cx = baseChunkX - size + x;
                    int cz = baseChunkZ - size + z;

                    capi.World.SetChunkColumnVisible(cx, cz, dimension);
                }
            }
        }

        StoryStructureLocation genStoryStructLoc;
        GenStoryStructures genGenStoryStructures;
        int storyTowerBaseY;
        /// <summary>
        /// Called to set up the devastationLocation. Returns the size
        /// </summary>
        /// <param name="structureLocation"></param>
        /// <param name="genStoryStructures"></param>
        public int SetupDim2TowerGeneration(StoryStructureLocation structureLocation, GenStoryStructures genStoryStructures)
        {
            genStoryStructLoc = structureLocation;
            genGenStoryStructures = genStoryStructures;
            storyTowerBaseY = structureLocation.CenterPos.Y + 10;   // 10 is hard-coded fudge based on current schematic with a grass mound below the tower, that information is not found in any asset, I guess we could instead look for the lowest non-topsoil non-air block but just as fudgy because it assumes which blocks are in the schematic
            sapi.Logger.VerboseDebug("Setup dim2 " + (baseChunkX * GlobalConstants.ChunkSize) + ", " + (baseChunkZ * GlobalConstants.ChunkSize));
            return size;
        }

        public void AttemptGeneration(IWorldGenBlockAccessor worldgenBlockAccessor)
        {
            if (genStoryStructLoc == null || genStoryStructLoc.DidGenerateAdditional) return;

            if (!AreAllDim0ChunksGenerated(worldgenBlockAccessor)) return;
            sapi.Logger.VerboseDebug("Timeswitch dim 2 generation: finished stage 1");

            var startPos = genStoryStructLoc.Location.Start.AsBlockPos;
            startPos.dimension = OtherDimension;
            PlaceSchematic(sapi.World.BlockAccessor, "story/" + genStoryStructLoc.Code + "-past", startPos);
            sapi.Logger.VerboseDebug("Timeswitch dim 2 generation: finished stage 2");

            RelightAltDimension();

            AddClaimForDim(OtherDimension);

            genStoryStructLoc.DidGenerateAdditional = true;
            genGenStoryStructures.StoryStructureInstancesDirty = true;      // Mark as done in the savegame only after everything is complete; if there was an exit or crash previously during dim2 tower generation, this may mean code attempts to place dim2 tower twice, but that's better than not at all

            sapi.Logger.VerboseDebug("Timeswitch dim 2 generation: finished stage 3");
            // Send updates of the newly generated chunks to all players in range, otherwise they may have old copies
            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player.ConnectionState != EnumClientState.Playing) continue;

                if (WithinRange(player.Entity.ServerPos, deactivateRadius + 2))
                {
                    ForceSendChunkColumns(player);
                }
            }
            sapi.Logger.VerboseDebug("Timeswitch dim 2 generation: finished stage 4");
        }

        private void RelightAltDimension()
        {
            if (size > 0)
            {
                BlockPos start = new BlockPos((baseChunkX - size) * GlobalConstants.ChunkSize, 0, (baseChunkZ - size) * GlobalConstants.ChunkSize, OtherDimension);
                BlockPos end = start.AddCopy(GlobalConstants.ChunkSize * (size * 2 + 1) - 1, sapi.WorldManager.MapSizeY - 1, GlobalConstants.ChunkSize * (size * 2 + 1) - 1);
                start.Y = (sapi.World.SeaLevel - 8) / GlobalConstants.ChunkSize * GlobalConstants.ChunkSize;
                sapi.WorldManager.FullRelight(start, end, false);
            }
        }

        private void AddClaimForDim(int dim)
        {
            int centerX = baseChunkX * GlobalConstants.ChunkSize + GlobalConstants.ChunkSize / 2;
            int centerZ = baseChunkZ * GlobalConstants.ChunkSize + GlobalConstants.ChunkSize / 2;
            int radius = size * GlobalConstants.ChunkSize + GlobalConstants.ChunkSize / 2;
            int dimY = dim * BlockPos.DimensionBoundary;
            var struclocDeva = new Cuboidi(
                centerX - radius, dimY + 0, centerZ - radius,
                centerX + radius, dimY + sapi.WorldManager.MapSizeY, centerZ + radius);

            var claims = sapi.World.Claims.Get(struclocDeva.Center.AsBlockPos);
            if (claims == null || claims.Length == 0)
            {
                var storyStructureConf = genGenStoryStructures.scfg.Structures.First(s => s.Code == genStoryStructLoc.Code);
                sapi.World.Claims.Add(new LandClaim()
                {
                    Areas = new List<Cuboidi>() { struclocDeva },
                    Description = "Past Dimension",
                    ProtectionLevel = storyStructureConf.ProtectionLevel,
                    LastKnownOwnerName = "custommessage-thepast",
                    AllowUseEveryone = storyStructureConf.AllowUseEveryone,
                    AllowTraverseEveryone = storyStructureConf.AllowTraverseEveryone
                });
            }
        }

        private void PlaceSchematic(IBlockAccessor blockAccessor, string genSchematicName, BlockPos start)
        {
            BlockSchematicPartial blocks = LoadSchematic(sapi, genSchematicName);
            if (blocks == null) return;

            blocks.Init(blockAccessor);
            blocks.blockLayerConfig = genGenStoryStructures.blockLayerConfig;

            blocks.Place(blockAccessor, sapi.World, start, EnumReplaceMode.ReplaceAllNoAir, true);
            blocks.PlaceDecors(blockAccessor, start);
        }

        private bool AreAllDim0ChunksGenerated(IBlockAccessor wgenBlockAccessor)
        {
            var blockAccess = sapi.World.BlockAccessor;  // We use the standard blockAccessor - wgen version may be doing weird peek stuff
            for (int cx = baseChunkX - size + 1; cx < baseChunkX + size; cx++)
            {
                for (int cz = baseChunkZ - size + 1; cz < baseChunkZ + size; cz++)
                {
                    IMapChunk mc = blockAccess.GetMapChunk(cx, cz);
                    if (mc == null) return false;
                    if (mc.CurrentPass <= EnumWorldGenPass.Vegetation)
                    {
                        return false;
                    }
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

            schematic.FromFileName =
                asset.Location.Domain == GlobalConstants.DefaultDomain ?
                    asset.Name : $"{asset.Location.Domain}:{asset.Name}";
            return schematic;
        }


        private bool OtherDimensionPositionWouldCollide(EntityPlayer entity, int otherDim, bool allowTolerance)
        {
            Vec3d tmpVec = entity.ServerPos.XYZ;
            tmpVec.Y = entity.ServerPos.Y + otherDim * BlockPos.DimensionBoundary;

            var reducedBox = entity.CollisionBox.Clone();
            if (allowTolerance)
            {
                reducedBox.OmniNotDownGrowBy(-EntityBehaviorPlayerPhysics.ClippingToleranceOnDimensionChange);
            }

            return collTester.IsColliding(sapi.World.BlockAccessor, reducedBox, tmpVec, false);

            // push out will be handled by BehaviorPlayerPhysics client-side as player physics is client-side only
        }


        private void RaisePlayerToTerrainSurface(EntityPlayer entity, int targetDimension, TimeSwitchState tss)
        {
            double px = entity.ServerPos.X;
            double py = entity.ServerPos.Y;
            double pz = entity.ServerPos.Z;

            Cuboidd entityBox = entity.CollisionBox.ToDouble().Translate(px, py, pz);

            int minX = (int)entityBox.X1;
            int minZ = (int)entityBox.Z1;
            int maxX = (int)entityBox.X2;
            int maxZ = (int)entityBox.Z2;

            int terrainY = 0;
            BlockPos bp = new BlockPos(targetDimension);
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    bp.Set(x, terrainY, z);
                    int y;
                    if (targetDimension == Dimensions.NormalWorld)
                    {
                        y = entity.World.BlockAccessor.GetRainMapHeightAt(bp);
                        if (y > storyTowerBaseY)    // Overcomes the fact that the dim0 tower schematic placement places exploded blocks high in the air above terrain (which raise the rainmapheight)
                        {
                            y = getWorldSurfaceHeight(entity.World.BlockAccessor, bp);
                        }
                    }
                    else y = getWorldSurfaceHeight(entity.World.BlockAccessor, bp);
                    if (y > terrainY) terrainY = y;
                }
            }

            if (terrainY > 0) tss.forcedY = terrainY + 1;   // Add 1 because we want to place the player's feet immediately *above* this world surface block
        }

        private int getWorldSurfaceHeight(IBlockAccessor blockAccessor, BlockPos bp)
        {
            while (bp.Y < blockAccessor.MapSizeY)
            {
                Block b = blockAccessor.GetBlock(bp, BlockLayersAccess.Solid);
                if (!b.SideIsSolid(bp, BlockFacing.UP.Index)) return bp.Y - 1;   // Return the block below this air/grass block, i.e. the blockPos.Y of the world surface block
                bp.Up();
            }
            return 0;
        }
    }
}
