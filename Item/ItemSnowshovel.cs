using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class ModSystemSnowShoveling : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => true;

        protected Dictionary<string, Vec3d> snowshovelingplayers = new();
        protected ICoreServerAPI? sapi;
        protected ICoreClientAPI? capi;
        protected ILoadedSound? shovellingSound;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.RegisterGameTickListener(snowShovelTickServer, 75);
            sapi.Event.PlayerDisconnect += OnPlayerPlayerDisconnect;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.RegisterGameTickListener(snowShovelTickClient, 75);
        }

        private void OnPlayerPlayerDisconnect(IServerPlayer byPlayer)
        {
            StopSnowShoveling(byPlayer.PlayerUID);
        }

        float lookingAtSnowAccum = 0f;

        private void snowShovelTickClient(float dt)
        {
            ArgumentNullException.ThrowIfNull(capi);
            if (capi.World.Player.CurrentBlockSelection?.Block?.BlockMaterial == EnumBlockMaterial.Snow)
            {
                lookingAtSnowAccum = 1;
            } else
            {
                lookingAtSnowAccum -= dt;
            }

            if (snowshovelingplayers.Count > 0 && capi.World.Player.Entity.Controls.Forward && lookingAtSnowAccum > 0)
            {
                if (shovellingSound == null)
                {
                    shovellingSound = capi.World.LoadSound(new SoundParams()
                    {
                        Location = "sounds/effect/snowshovelling",
                        Range = 16,
                        RelativePosition = true,
                        ShouldLoop = true,
                        DisposeOnFinish = false,
                        Volume = 0
                    });
                }

                if (!shovellingSound.IsPlaying)
                {
                    shovellingSound.Start();
                    shovellingSound.FadeIn(0.2f, null);
                }
            }
            else
            {
                if (shovellingSound != null && shovellingSound.IsPlaying)
                {
                    shovellingSound.FadeOutAndStop(0.4f);
                }
            }
        }

        private void snowShovelTickServer(float dt)
        {
            ArgumentNullException.ThrowIfNull(sapi);
            int snowCapacity = 8; // Numbers between 4 and 12 generally seem to work well
            foreach (var (uid, prevpos) in snowshovelingplayers)
            {
                var plr = sapi.World.PlayerByUid(uid);
                if (plr.Entity.BlockSelection == null) continue;

                var nowPos = plr.Entity.BlockSelection.FullPosition;
                if (!plr.Entity.Controls.Forward) continue;

                if (nowPos.DistanceTo(prevpos) > 0.01)
                {
                    Vec3d toDir = plr.Entity.Pos.GetViewVector().ToVec3d();
                    toDir.Y = 0;
                    toDir.Normalize();
                    // No lock to cardinal directions, so diagonal pushing is allowed

                    var fromPos = prevpos.AsBlockPos;

                    prevpos.Add((nowPos - prevpos).Normalize().Mul(0.4));
                    prevpos.Y = nowPos.Y;

                    int snowLayers = Math.Min(snowCapacity, (int)Math.Ceiling(CountSnowLayers(fromPos)));

                    // 1. On tall stacks, some snow may fall off to the left or right
                    if (snowLayers > 3)
                    {
                        int amount1 = sapi.World.Rand.Next(snowLayers) / 3;
                        int amount2 = sapi.World.Rand.Next(snowLayers) / 3;
                        // Left/right order doesn't theoretically matter
                        ShoveSnowColumn(fromPos, toDir.RotatedCopy(GameMath.PIHALF), amount1);
                        ShoveSnowColumn(fromPos, toDir.RotatedCopy(-GameMath.PIHALF), amount2);
                    }

                    // 2. Try to just add snow in front
                    ShoveSnowColumn(fromPos, toDir, snowCapacity);

                    // 3. May fail to move all snow, with no further effect
                }
            }
        }

        protected float CountSnowLayers(BlockPos fromPos)
        {
            ArgumentNullException.ThrowIfNull(sapi);
            Block block = sapi.World.BlockAccessor.GetBlock(fromPos);
            float snow = block.GetSnowLevel(fromPos);
            BlockPos tempPos = fromPos.UpCopy();
            Block aboveBlock = sapi.World.BlockAccessor.GetBlock(tempPos);
            snow += aboveBlock.GetSnowLevel(tempPos);
            tempPos.Down().Down();
            Block belowBlock = sapi.World.BlockAccessor.GetBlock(tempPos);
            snow += belowBlock.GetSnowLevel(tempPos);
            
            return snow;
        }

        public void ShoveSnowColumn(BlockPos fromPos, Vec3d toDir, float maxLayers)
        {
            if (maxLayers <= 0) return;

            toDir.X = Math.Round(toDir.X);
            toDir.Y = Math.Round(toDir.Y);
            toDir.Z = Math.Round(toDir.Z);

            // Shovel only sideways, not up/down
            if (toDir.X == 0 && toDir.Z == 0) return;

            ArgumentNullException.ThrowIfNull(sapi);
            var blockAccessor = sapi.World.BlockAccessor;

            BlockPos currentSourcePos = fromPos;

            // Want to get the locally highest snowy block, where "locally" is within one block up/down from target
            BlockPos tempPos = fromPos.UpCopy();
            Block aboveBlock = blockAccessor.GetBlock(tempPos);
            float aboveSnow = aboveBlock.GetSnowLevel(tempPos);
            if (aboveSnow > 0)
            {
                currentSourcePos = fromPos.UpCopy();
                Block twiceAboveBlock = blockAccessor.GetBlock(tempPos.Up());
                float twiceAboveSnow = twiceAboveBlock.GetSnowLevel(tempPos);
                if (twiceAboveSnow > 0)
                {
                    // Too deep to shovel here
                    return;
                }
            }
            else
            {
                Block block = blockAccessor.GetBlock(fromPos);
                float baseSnow = block.GetSnowLevel(fromPos);
                if (baseSnow == 0)
                {
                    // In case the player is walking on top of the snow with the shovel
                    currentSourcePos = tempPos.Down().Down();
                    Block belowBlock = blockAccessor.GetBlock(tempPos);
                    float belowSnow = belowBlock.GetSnowLevel(tempPos);
                    if (belowSnow <= 0)
                    {
                        // No snow to shovel
                        return;
                    }
                }
            }

            float initialSnow = blockAccessor.GetBlock(currentSourcePos).GetSnowLevel(currentSourcePos);

            BlockPos frontPos = fromPos + toDir.AsBlockPos;
            // Handles flat movement and falling
            frontPos.Y = Math.Min(frontPos.Y, currentSourcePos.Y);
            bool needsFall = TryMoveSnow(currentSourcePos, frontPos, maxLayers, true);
            spawnSnowParticles(fromPos, toDir);
            float remainingSnow = blockAccessor.GetBlock(currentSourcePos).GetSnowLevel(currentSourcePos);

            maxLayers -= initialSnow - remainingSnow;

            if (remainingSnow == 0)
            {
                currentSourcePos.Down();
                initialSnow = blockAccessor.GetBlock(currentSourcePos).GetSnowLevel(currentSourcePos);
                needsFall = needsFall || TryMoveSnow(currentSourcePos, frontPos, maxLayers, true);
                remainingSnow = blockAccessor.GetBlock(currentSourcePos).GetSnowLevel(currentSourcePos);
            }
            if (needsFall)
            {
                var falling = blockAccessor.GetBlock(frontPos).GetBehavior<BlockBehaviorUnstableFalling>();
                if (falling != null)
                {
                    falling.createFallingBlock(sapi.World, frontPos);
                }
            }

            maxLayers -= initialSnow - remainingSnow;

            // Handle uphill (or upsnow) movement
            frontPos.Up();
            TryMoveSnow(currentSourcePos, frontPos, maxLayers, false);
        }

        // Moves snow from one position to another, and triggers neighbor updates. Returns true if a falling block update is needed.
        public bool TryMoveSnow(BlockPos fromPos, BlockPos toPos, float maxLevelMoved, bool allowFalling)
        {
            if (maxLevelMoved <= 0) return false;

            ArgumentNullException.ThrowIfNull(sapi);
            Block snowBlock = sapi.World.GetBlock("snowblock") ?? throw new Exception("Expected snow block to be included in game files");

            Block fromBlock = sapi.World.BlockAccessor.GetBlock(fromPos);
            float fromLevel = (float)Math.Ceiling(fromBlock.GetSnowLevel(fromPos)); // Ceiling, because chiseled blocks return 0.5f
            Block toBlock = sapi.World.BlockAccessor.GetBlock(toPos);
            float toLevel = (float)Math.Ceiling(toBlock.GetSnowLevel(toPos));

            // Move snow layers into target block
            // Deliberately skip checking block replaceable values, because the snow cover system does not do that in general
            float toMove = (float)Math.Min(maxLevelMoved, fromLevel);
            // Special case for air, to become snow block or snow layers
            Block snowSink = toBlock.Id == 0 ? snowBlock : toBlock;
            Block? newlySnowyBlock = snowSink.GetSnowCoveredVariant(toPos, toMove + toLevel);

            Block? snowlessBlock = fromBlock.GetSnowCoveredVariant(fromPos, 0);
            if (snowlessBlock == null)
            {
                // This should never happen, but returning early prevents snow duplication just in case it does
                return false;
            }

            bool attached = true;
            float remainingSnow = fromLevel;
            if (newlySnowyBlock != null)
            {
                BlockPos downPos = toPos.DownCopy();
                attached = sapi.World.BlockAccessor.GetBlock(downPos).CanAttachBlockAt(sapi.World.BlockAccessor, newlySnowyBlock, downPos, BlockFacing.UP);
                var falling = newlySnowyBlock.GetBehavior<BlockBehaviorUnstableFalling>();
                if (falling != null && !attached && (!allowFalling || falling.FallingEntityAlreadyExists(sapi.World, toPos)))
                {
                    return false;
                }

                toBlock.PerformSnowLevelUpdate(sapi.World.BlockAccessor, toPos, newlySnowyBlock, toMove + toLevel);
                sapi.World.BlockAccessor.TriggerNeighbourBlockUpdate(toPos);
                float snowLevelAfter = (float)Math.Ceiling(newlySnowyBlock.GetSnowLevel(toPos));
                remainingSnow = fromLevel + toLevel - snowLevelAfter;

                // Don't trigger the fall from here directly, in case the situation changes later
            }
            // Liquids melt the snow
            else if (toBlock.IsLiquid())
            {
                remainingSnow = Math.Max(0, fromLevel - maxLevelMoved);
            }

            // And remove snow from the starting block
            if (fromLevel > remainingSnow)
            {
                Block desnowedBlock = fromBlock.GetSnowCoveredVariant(fromPos, remainingSnow) ?? snowlessBlock;
                fromBlock.PerformSnowLevelUpdate(sapi.World.BlockAccessor, fromPos, desnowedBlock, remainingSnow);
                sapi.World.BlockAccessor.TriggerNeighbourBlockUpdate(fromPos);
            }

            return !attached;
        }

        private void spawnSnowParticles(BlockPos fromPos, Vec3d toDir)
        {
            var uppos = fromPos.AddCopy(0, 0.2f, 0).ToVec3d();
            sapi.World.SpawnParticles(
                30, ColorUtil.WhiteArgb, uppos, uppos.AddCopy(1, 0.3f, 1), new Vec3f(-2f + (float)toDir.X, 0.2f, -2f + (float)toDir.Z), new Vec3f(2f + (float)toDir.X, 2f, 2f + (float)toDir.Z),
                2, 1, 1f, EnumParticleModel.Cube
            );

            var dustParticles = FallingBlockParticlesModSystem.dustParticles.Clone(sapi.World);
            dustParticles.Color = ColorUtil.WhiteArgb;
            dustParticles.Color &= 0xffffff;
            dustParticles.Color |= (150 << 24);
            dustParticles.MinPos.Set(uppos);
            dustParticles.MinSize = 0.35f;
            dustParticles.LifeLength = 2f;
            dustParticles.AddPos.Y = 0.15f;
            dustParticles.MinVelocity.Set(-0.2f, 0f, -0.2f);
            dustParticles.AddVelocity.Set(0.4f, 0f, 0.4f);
            dustParticles.MinQuantity = 1;
            dustParticles.AddQuantity = 2;
            sapi.World.SpawnParticles(dustParticles);
        }

        public void BeginSnowShoveling(string uid, Vec3d aimPos)
        {
            snowshovelingplayers[uid] = aimPos;
        }

        public void StopSnowShoveling(string uid)
        {
            snowshovelingplayers.Remove(uid);
        }
    }

    public class ItemSnowshovel : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);

            if (handling == EnumHandHandling.NotHandled && blockSel != null)
            {
                byEntity.Stats.Set("walkspeed", "snowshovelingmod", -0.4f, true);
                if (byEntity is EntityPlayer byPlayer)
                {
                    byPlayer.walkSpeed = byEntity.Stats.GetBlended("walkspeed");
                    api.ModLoader.GetModSystem<ModSystemSnowShoveling>().BeginSnowShoveling(byPlayer.PlayerUID, blockSel.FullPosition);
                }

                handling = EnumHandHandling.PreventDefault;
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            byEntity.Stats.Remove("walkspeed", "snowshovelingmod");
            if (byEntity is EntityPlayer byPlayer)
            {
                byPlayer.walkSpeed = byEntity.Stats.GetBlended("walkspeed");
                api.ModLoader.GetModSystem<ModSystemSnowShoveling>().StopSnowShoveling(byPlayer.PlayerUID);
            }
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.Stats.Remove("walkspeed", "snowshovelingmod");
            if (byEntity is EntityPlayer byPlayer)
            {
                byPlayer.walkSpeed = byEntity.Stats.GetBlended("walkspeed");
                api.ModLoader.GetModSystem<ModSystemSnowShoveling>().StopSnowShoveling(byPlayer.PlayerUID);
            }

            return true;
        }
    }
}
