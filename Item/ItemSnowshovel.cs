using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ModSystemSnowShoveling : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => true;

        protected Dictionary<string, Vec3d> snowshovelingplayers = new();
        protected ICoreServerAPI sapi;
        protected ICoreClientAPI capi;
        protected ILoadedSound shovellingSound;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.RegisterGameTickListener(snowShovelTickServer, 75);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.RegisterGameTickListener(snowShovelTickClient, 75);
        }

        float lookingAtSnowAccum = 0f;

        private void snowShovelTickClient(float dt)
        {
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
            foreach (var (uid, prevpos) in snowshovelingplayers)
            {
                var plr = sapi.World.PlayerByUid(uid);
                if (plr.Entity.BlockSelection == null) continue;

                var nowPos = plr.Entity.BlockSelection.FullPosition;
                if (!plr.Entity.Controls.Forward) continue;

                if (nowPos.DistanceTo(prevpos) > 0.01)
                {
                    var vw = plr.Entity.Pos.GetViewVector().ToVec3d();
                    vw.Y = 0;
                    var facing = BlockFacing.FromVector(vw);
                    var dir = facing.Normald;

                    var fromPos = prevpos.AsBlockPos;

                    prevpos.Add((nowPos - prevpos).Normalize().Mul(0.4));
                    prevpos.Y = nowPos.Y;

                    Block block = getSnowyBlock(fromPos);
                    if (block == null) continue;

                    // 1. Try to add snow left or right, if height is > 3
                    // does not work well / is unsatisfying
                    /*if (block.GetSnowLevel(fromPos) >= 3)
                    {
                        bool left = sapi.World.Rand.NextDouble() < 0;

                        if (left && tryMoveSnowTo(fromPos, dir.RotatedCopy(GameMath.PIHALF))) continue;
                        if (!left && tryMoveSnowTo(fromPos, dir.RotatedCopy(-GameMath.PIHALF))) continue;
                        if (!left && tryMoveSnowTo(fromPos, dir.RotatedCopy(GameMath.PIHALF))) continue;
                        if (left && tryMoveSnowTo(fromPos, dir.RotatedCopy(-GameMath.PIHALF))) continue;
                    }*/

                    // 2. Try to just add snow in front
                    if (tryMoveSnowTo(fromPos, dir)) continue;


                    // 3. Can't move snow, no effect


                }
            }
        }

        private Block getSnowyBlock(BlockPos fromPos)
        {
            var block = sapi.World.BlockAccessor.GetBlock(fromPos);
            if (block.GetSnowLevel(fromPos) == 0)
            {
                var abovePos = fromPos.UpCopy();
                block = sapi.World.BlockAccessor.GetBlock(abovePos);
                if (block.GetSnowLevel(abovePos) == 0) return null;
            }

            return block;
        }

        // Returns true only if *all* snow was moved
        public bool tryMoveSnowTo(BlockPos fromPos, Vec3d toDir)
        {
            Block snowyBlock = getSnowyBlock(fromPos);
            if (snowyBlock == null) return true;

            var ba = sapi.World.BlockAccessor;

            toDir.X = Math.Round(toDir.X);
            toDir.Y = Math.Round(toDir.Y);
            toDir.Z = Math.Round(toDir.Z);

            var frontPos = fromPos + toDir.AsBlockPos;
            var frontUpPos = frontPos.UpCopy();
            var frontBlock = ba.GetBlock(frontPos);
            var frontUpBlock = ba.GetBlock(frontUpPos);

            float snowlevel = (float)Math.Ceiling(snowyBlock.GetSnowLevel(fromPos)); // Chiseled blocks returns 0.5 snow. Ugh.
            float movedSnowLevel = 0;
            var frontUpSnowLevel = frontUpBlock.GetSnowLevel(frontUpPos);

            // We might be aiming at a snowblock above ground directly
            if (snowyBlock is BlockSnow || snowyBlock is BlockSnowLayer)
            {
                var frontBelowPos = frontPos.DownCopy();
                var frontBelowBlock = ba.GetBlock(frontBelowPos);

                if (frontBlock.Id == 0)
                {
                    
                    float frontBelowSnow = frontBelowBlock.GetSnowLevel(frontBelowPos);
                    if (frontBelowSnow > 0)
                    {
                        snowyBlock = snowyBlock.GetSnowCoveredVariant(fromPos, snowlevel + frontBelowSnow);
                        sapi.World.BlockAccessor.SetBlock(frontBelowBlock.GetSnowCoveredVariant(frontBelowPos, 0).Id, frontBelowPos);
                    }

                    sapi.World.BlockAccessor.SetBlock(snowyBlock.Id, frontPos);
                    sapi.World.BlockAccessor.SetBlock(0, fromPos);

                    if (!frontBelowBlock.SideIsSolid(frontBelowPos, BlockFacing.UP.Index) && frontBelowBlock.Attributes?.IsTrue("canShovelSnowOnto") != true)
                    {
                        snowyBlock.GetBehavior<BlockBehaviorUnstableFalling>()?.createFallingBlock(sapi.World, frontPos);
                    }

                    spawnSnowParticles(fromPos, toDir);
                    return true;
                }
                
            }

            // Not if there is already a snow block above
            if (frontUpSnowLevel == 0)
            {
                movedSnowLevel = snowlevel;

                // We're at a block edge
                if (frontBlock.Id == 0)
                {
                    var extractedSnowBlock = sapi.World.GetBlock("snowlayer-1").GetSnowCoveredVariant(fromPos, snowlevel);
                    var snowFreeBlock = snowyBlock.GetSnowCoveredVariant(fromPos, 0);

                    sapi.World.BlockAccessor.SetBlock(extractedSnowBlock.Id, frontPos);
                    snowyBlock.PerformSnowLevelUpdate(ba, fromPos, snowFreeBlock, 0);

                    if (!sapi.World.BlockAccessor.GetBlock(frontPos.DownCopy()).SideIsSolid(frontPos.DownCopy(), BlockFacing.UP.Index)) {
                        extractedSnowBlock.GetBehavior<BlockBehaviorUnstableFalling>()?.createFallingBlock(sapi.World, frontPos);
                    }
                }
                else
                {
                    while (movedSnowLevel > 0)
                    {
                        var moreSnowyBlock = frontBlock.GetSnowCoveredVariant(frontPos, frontBlock.snowLevel + movedSnowLevel);
                        if (moreSnowyBlock != null && frontBlock.Id != moreSnowyBlock.Id)
                        {
                            frontBlock.PerformSnowLevelUpdate(ba, frontPos, moreSnowyBlock, frontBlock.snowLevel + movedSnowLevel);
                            snowyBlock.PerformSnowLevelUpdate(ba, fromPos, snowyBlock.GetSnowCoveredVariant(fromPos, snowlevel - movedSnowLevel), snowlevel - movedSnowLevel);
                            spawnSnowParticles(fromPos, toDir);
                            break;
                        }

                        movedSnowLevel = Math.Max(0, movedSnowLevel - 1);
                    }
                }
            }

            float remainingSnow = snowlevel - movedSnowLevel;
            if (remainingSnow == 0) return true;



            // If we weren't able to move all the snow, lets pile it up
            if (remainingSnow > 0 && (frontBlock.AllowSnowCoverage(sapi.World, frontPos) || frontBlock.Attributes?.IsTrue("canShovelSnowOnto") == true) && (frontUpBlock.BlockMaterial == EnumBlockMaterial.Snow || frontUpBlock.Id == 0))
            {
                var refBlock = frontUpBlock;
                if (frontUpSnowLevel == 0) refBlock = sapi.World.GetBlock("snowlayer-1");

                float belowSnow = frontBlock.GetSnowLevel(frontPos);

                var newSnowLevel = Math.Min(8, frontUpSnowLevel + remainingSnow + belowSnow);
                remainingSnow = Math.Max(0, (frontUpSnowLevel + remainingSnow) - newSnowLevel);

                sapi.World.BlockAccessor.SetBlock(refBlock.GetSnowCoveredVariant(frontUpPos, newSnowLevel).Id, frontUpPos);

                snowyBlock.PerformSnowLevelUpdate(ba, fromPos, snowyBlock.GetSnowCoveredVariant(fromPos, remainingSnow), remainingSnow);

                if (belowSnow > 0)
                {
                    frontBlock.PerformSnowLevelUpdate(ba, frontPos, frontBlock.GetSnowCoveredVariant(frontPos, 0), 0);
                }

                spawnSnowParticles(fromPos, toDir);
                return true;
            }

            return false;
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
            snowshovelingplayers.Add(uid, aimPos);
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

            if (blockSel != null)
            {
                byEntity.Stats.Set("walkspeed", "snowshovelingmod", -0.4f, true);
                (byEntity as EntityPlayer).walkSpeed = byEntity.Stats.GetBlended("walkspeed");

                handling = EnumHandHandling.PreventDefault;
                api.ModLoader.GetModSystem<ModSystemSnowShoveling>().BeginSnowShoveling((byEntity as EntityPlayer).PlayerUID, blockSel.FullPosition);
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            byEntity.Stats.Remove("walkspeed", "snowshovelingmod");
            (byEntity as EntityPlayer).walkSpeed = byEntity.Stats.GetBlended("walkspeed");

            api.ModLoader.GetModSystem<ModSystemSnowShoveling>().StopSnowShoveling((byEntity as EntityPlayer).PlayerUID);
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.Stats.Remove("walkspeed", "snowshovelingmod");
            (byEntity as EntityPlayer).walkSpeed = byEntity.Stats.GetBlended("walkspeed");

            api.ModLoader.GetModSystem<ModSystemSnowShoveling>().StopSnowShoveling((byEntity as EntityPlayer).PlayerUID);

            return true;
        }
    }
}
