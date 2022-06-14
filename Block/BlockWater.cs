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
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockWater : BlockForLiquidsLayer, IBlockFlowing
    {
        public string Flow { get; set; }
        public Vec3i FlowNormali { get; set; }
        public bool IsLava => false;

        public int Height { get; set; }

        bool freezable;
        Block iceBlock;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            Flow = Variant["flow"] is string f ? string.Intern(f) : null;
            FlowNormali = Flow != null ? Cardinal.FromInitial(Flow)?.Normali : null;
            Height = Variant["height"] is string h ? h.ToInt() : 7;

            freezable = Flow == "still" && Height == 7;
            iceBlock = api.World.GetBlock(new AssetLocation("lakeice"));
        }


        public override bool ShouldPlayAmbientSound(IWorldAccessor world, BlockPos pos)
        {
            // Play water wave sound when above is air and below is a solid block
            return
                world.BlockAccessor.GetBlockId(pos.X, pos.Y + 1, pos.Z) == 0 &&
                world.BlockAccessor.IsSideSolid(pos.X, pos.Y - 1, pos.Z, BlockFacing.UP);
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
                        if (world.BlockAccessor.GetLiquidBlock(nPos) is BlockLakeIce || world.BlockAccessor.GetBlock(nPos).Replaceable < 6000)
                        {
                            ClimateCondition conds = world.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.NowValues);
                            if (conds != null && conds.Temperature < -4)
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
            world.BlockAccessor.SetLiquidBlock(iceBlock.Id, pos);
        }

        public override void OnGroundIdle(EntityItem entityItem)
        {
            entityItem.Die(EnumDespawnReason.Removed);

            if (entityItem.World.Side == EnumAppSide.Server)
            {
                Vec3d pos = entityItem.ServerPos.XYZ;

                WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(entityItem.Itemstack);
                float litres = (float)entityItem.Itemstack.StackSize / props.ItemsPerLitre;

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
            if (oldBlock.SideSolid.OnSidesAndBase() && !oldBlock.IsReplacableBy(this))
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
    }
}
