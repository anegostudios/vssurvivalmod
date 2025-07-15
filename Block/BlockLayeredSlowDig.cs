using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockLayeredSlowDig : Block
    {
        protected string layerGroupCode = "height";
        protected AssetLocation fullBlockCode = new AssetLocation("air");

        public override void OnLoaded(ICoreAPI api)
        {
            if (Attributes != null)
            {
                layerGroupCode = Attributes["layerGroupCode"].AsString("height");
                fullBlockCode = AssetLocation.Create(Attributes["fullBlockCode"].AsString("air"), Code.Domain);
            }
        }

        public int CountLayers()
        {
            int.TryParse(Variant[layerGroupCode], out int layer);
            return layer;
        }

        public Block GetPrevLayer(IWorldAccessor world)
        {
            int layer = CountLayers();
            if (layer > 1) return world.BlockAccessor.GetBlock(CodeWithVariant(layerGroupCode, "" + (layer-1)));
            return null;
        }

        public Block GetNextLayer(IWorldAccessor world)
        {
            int layer = CountLayers();
            if (layer < 7) return world.BlockAccessor.GetBlock(CodeWithVariant(layerGroupCode, "" + (layer + 1)));
            return world.BlockAccessor.GetBlock(fullBlockCode);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                failureCode = "claimed";
                return false;
            }

            Block block = world.BlockAccessor.GetBlock(blockSel.Position.AddCopy(blockSel.Face.Opposite));

            if (block is BlockLayeredSlowDig blocklsd)
            {
                Block nextBlock = blocklsd.GetNextLayer(world);
                if (nextBlock == null) return false;

                world.BlockAccessor.SetBlock(nextBlock.BlockId, blockSel.Position.AddCopy(blockSel.Face.Opposite));
                return true;
            }

            base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
            return true;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            Block prev = GetPrevLayer(world);
            if (prev != null)
            {
                if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
                {
                    ItemStack[] drops = GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

                    if (drops != null)
                    {
                        for (int i = 0; i < drops.Length; i++)
                        {
                            world.SpawnItemEntity(drops[i], pos, null);
                        }
                    }

                    world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos, -0.5, byPlayer);
                }

                if (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    world.BlockAccessor.SetBlock(prev.BlockId, pos);
                } else
                {
                    world.BlockAccessor.SetBlock(0, pos);
                }

                return;
            }

            base.OnBlockBroken(world, pos, byPlayer);
        }
        


        public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            return false;
        }


    }
}
