using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorHorizontalOrientable : BlockBehavior
    {
        string dropBlockFace = "north";
        string variantCode = "horizontalorientation";
        JsonItemStack drop = null;

        public BlockBehaviorHorizontalOrientable(Block block) : base(block)
        {
            if (!block.Variant.ContainsKey("horizontalorientation"))
            {
                variantCode = "side";
            }
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            if (properties["dropBlockFace"].Exists)
            {
                dropBlockFace = properties["dropBlockFace"].AsString();
            }
            if (properties["drop"].Exists)
            {
                drop = properties["drop"].AsObject<JsonItemStack>(null, block.Code.Domain);
                
            }
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            drop?.Resolve(api.World, "HorizontalOrientable drop for " + block.Code);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            handling = EnumHandling.PreventDefault;
            BlockFacing[] horVer = Block.SuggestedHVOrientation(byPlayer, blockSel);
            AssetLocation blockCode = block.CodeWithVariant(variantCode, horVer[0].Code);
            Block orientedBlock = world.BlockAccessor.GetBlock(blockCode);

            if (orientedBlock == null)
            {
                throw new System.NullReferenceException("Unable to to find a rotated block with code " + blockCode + ", you're maybe missing the side variant group of have a dash in your block code");
            }

            if (orientedBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                orientedBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
                return true;
            }
            return false;
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropQuantityMultiplier, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            if (drop?.ResolvedItemstack != null)
            {
                return new ItemStack[] { drop?.ResolvedItemstack.Clone() };
            }
            return new ItemStack[] { new ItemStack(world.BlockAccessor.GetBlock(block.CodeWithVariant(variantCode, dropBlockFace))) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            if (drop != null)
            {
                return drop?.ResolvedItemstack.Clone();
            }

            return new ItemStack(world.BlockAccessor.GetBlock(block.CodeWithVariant(variantCode, dropBlockFace)));
        }

        
        public override AssetLocation GetRotatedBlockCode(int angle, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            BlockFacing beforeFacing = BlockFacing.FromCode(block.Variant[variantCode]);
            int rotatedIndex = GameMath.Mod(beforeFacing.HorizontalAngleIndex - angle / 90, 4);
            BlockFacing nowFacing = BlockFacing.HORIZONTALS_ANGLEORDER[rotatedIndex];
            
            return block.CodeWithVariant(variantCode, nowFacing.Code);
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            BlockFacing facing = BlockFacing.FromCode(block.Variant[variantCode]);
            if (facing.Axis == axis)
            {
                return block.CodeWithVariant(variantCode, facing.Opposite.Code);
            }
            return block.Code;
        }


    }
}
