using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockWater : BlockForFluidsLayer, IBlockFlowing
    {
        public string Flow { get; set; }
        public Vec3i FlowNormali { get; set; }
        public bool IsLava => false;
        public virtual bool HasNormalWaves => true;
        public int Height { get; set; }

        protected bool freezable;
        protected Block iceBlock;
        protected float freezingPoint = -4;
        protected bool isBoiling;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            Flow = Variant["flow"] is string f ? string.Intern(f) : null;
            FlowNormali = Flow != null ? Cardinal.FromInitial(Flow)?.Normali : null;
            Height = Variant["height"] is string h ? h.ToInt() : 7;
            int fspeed = Attributes["flowSpeed"].AsInt(1);
            if (fspeed > 1)
            {
                FlowNormali = FlowNormali?.Clone().Mul(fspeed);
            }


            freezable = Flow == "still" && Height == 7;
            if (Attributes != null)
            {
                freezable &= Attributes["freezable"].AsBool(true);

                iceBlock = api.World.GetBlock(AssetLocation.Create(Attributes["iceBlockCode"].AsString("lakeice"), Code.Domain));
                freezingPoint = Attributes["freezingPoint"].AsFloat(-4);
            }
            else
            {
                iceBlock = api.World.GetBlock(AssetLocation.Create("lakeice", Code.Domain));
            }

            isBoiling = HasBehavior<BlockBehaviorSteaming>();
        }


        public override float GetAmbientSoundStrength(IWorldAccessor world, BlockPos pos)
        {
            // Play water wave sound when above is air and below is a solid block
            return (world.BlockAccessor.GetBlockAbove(pos, 1).Id == 0 && world.BlockAccessor.IsSideSolid(pos.X, pos.Y - 1, pos.Z, BlockFacing.UP)) ? 1 : 0;
        }

        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                behavior.OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
            }
        }


        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        {
            extra = null;
            if (!GlobalConstants.MeltingFreezingEnabled) return false;

            if (freezable && offThreadRandom.NextDouble() < 0.6)
            {
                int rainY = world.BlockAccessor.GetRainMapHeightAt(pos);
                if (rainY <= pos.Y)
                {
                    BlockPos nPos = pos.Copy();
                    for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                    {
                        BlockFacing.HORIZONTALS[i].IterateThruFacingOffsets(nPos);
                        if (world.BlockAccessor.GetBlock(nPos, BlockLayersAccess.Fluid) is BlockLakeIce || world.BlockAccessor.GetBlock(nPos).Replaceable < 6000)
                        {
                            float temperature = world.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, api.World.Calendar.TotalDays).Temperature;
                            if (temperature <= freezingPoint)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            if (iceBlock == null) return;
            world.BlockAccessor.SetBlock(iceBlock.Id, pos, BlockLayersAccess.Fluid);
        }

        public override void OnGroundIdle(EntityItem entityItem)
        {
            entityItem.Die(EnumDespawnReason.Removed);

            if (entityItem.World.Side == EnumAppSide.Server)
            {
                Vec3d pos = entityItem.Pos.XYZ;

                WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(entityItem.Itemstack);
                float litres = (float)entityItem.Itemstack.StackSize / (props?.ItemsPerLitre ?? 1f);

                entityItem.World.SpawnCubeParticles(pos, entityItem.Itemstack, 0.75f, Math.Min(100, (int)(2 * litres)), 0.45f);
                entityItem.World.PlaySoundAt(new AssetLocation("sounds/environment/smallsplash"), (float)pos.X, (float)pos.Y, (float)pos.Z, null);

                BlockEntityFarmland bef = api.World.BlockAccessor.GetBlockEntity(pos.AsBlockPos) as BlockEntityFarmland;
                if (bef != null)
                {
                    bef.WaterFarmland(Height / 6f, false);
                    bef.MarkDirty(true);
                }
            }



            base.OnGroundIdle(entityItem);
        }



        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            Block oldBlock = world.BlockAccessor.GetBlock(blockSel.Position);
            if (oldBlock.DisplacesLiquids(world.BlockAccessor, blockSel.Position) && !oldBlock.IsReplacableBy(this))
            {
                failureCode = "notreplaceable";
                return false;
            }

            bool result = true;

            if (byPlayer != null && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                // Probably good idea to do so, so lets do it :P
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();

                failureCode = "claimed";
                return false;
            }

            bool preventDefault = false;

            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;

                bool behaviorResult = behavior.CanPlaceBlock(world, byPlayer, blockSel, ref handled, ref failureCode);

                if (handled != EnumHandling.PassThrough)
                {
                    result &= behaviorResult;
                    preventDefault = true;
                }

                if (handled == EnumHandling.PreventSubsequent) return result;
            }

            if (preventDefault) return result;

            return true;
        }


        public override float GetTraversalCost(BlockPos pos, EnumAICreatureType creatureType)
        {
            if (creatureType == EnumAICreatureType.SeaCreature && !isBoiling) return 0;
            return isBoiling && creatureType != EnumAICreatureType.HeatProofCreature ? 99999f : 5f;
        }

        public override void DetermineTopMiddlePos()
        {
            TopMiddlePos.Y = (Height + 1) / 8f;
        }

    }
}
