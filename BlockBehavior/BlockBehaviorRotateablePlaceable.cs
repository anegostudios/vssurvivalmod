using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public interface IRotatablePlaceable
    {
        float MeshAngleRad { get; set; }
        bool DoPartialSelection();
        Cuboidf[] GetCollisionBoxes();
        Cuboidf[] GetSelectionBoxes();
    }

    public class BlockBehaviorRotateablePlaceable : StrongBlockBehavior
    {
        protected ICoreAPI api;
        protected float intervalRad;
        public float OffsetRad;
        

        public BlockBehaviorRotateablePlaceable(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            intervalRad = GameMath.DEG2RAD * properties["intervalDeg"].AsFloat(45f);
            OffsetRad = GameMath.DEG2RAD * properties["offsetDeg"].AsFloat(0f);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;
        }


        public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos, ref EnumHandling handled)
        {
            var bem = block.GetInterface<IRotatablePlaceable>(world, pos);
            if (bem != null)
            {
                handled = EnumHandling.PreventDefault;
                return bem.DoPartialSelection();
            }

            return base.DoPartialSelection(world, pos, ref handled);
        }
        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, ref EnumHandling handled)
        {
            var bem = block.GetInterface<IRotatablePlaceable>(api.World, pos);
            if (bem != null)
            {
                handled = EnumHandling.PreventDefault;
                return bem.GetCollisionBoxes();
            }

            return null;
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, ref EnumHandling handled)
        {
            var bem = block.GetInterface<IRotatablePlaceable>(api.World, pos);
            if (bem != null)
            {
                handled = EnumHandling.PreventDefault;
                return bem.GetSelectionBoxes();
            }

            return null;
        }

        public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, ref EnumHandling handled)
        {
            return GetCollisionBoxes(blockAccessor, pos, ref handled);
        }

        public override Cuboidf GetParticleBreakBox(IBlockAccessor blockAccess, BlockPos pos, BlockFacing facing, ref EnumHandling handled)
        {
            return GetCollisionBoxes(blockAccess, pos, ref handled)[0];
        }


        // A not exactly pretty workaround
        Dictionary<BlockPos, float> recentPlacementRad = new Dictionary<BlockPos, float>();

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ref EnumHandling handling)
        {
            if (recentPlacementRad.TryGetValue(blockPos, out var angleRad))
            {
                recentPlacementRad.Remove(blockPos);
                block.GetInterface<IRotatablePlaceable>(api.World, blockPos).MeshAngleRad = angleRad;
            }
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack, ref EnumHandling handling)
        {
            BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
            double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
            double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
            float angleHor = (float)Math.Atan2(dx, dz);
            float roundRad = ((int)Math.Round(angleHor / intervalRad)) * intervalRad;
            recentPlacementRad[blockSel.Position] = roundRad;

            return base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack, ref handling);
        }




    }
}
