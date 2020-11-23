using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockLayeredSlowDig : Block
    {
        public int Layers()
        {
            int layer = 0;
            int.TryParse(Code.Path.Split('-')[1], out layer);
            return layer;
        }

        public Block GetPrevLayer(IWorldAccessor world)
        {
            int layer = 0;
            int.TryParse(Code.Path.Split('-')[1], out layer);

            string basecode = CodeWithoutParts(1);

            if (layer > 1) return world.BlockAccessor.GetBlock(CodeWithPath(basecode + "-" + (layer - 1)));
            return null;
        }

        public Block GetNextLayer(IWorldAccessor world)
        {
            int layer = 0;
            int.TryParse(Code.Path.Split('-')[1], out layer);

            string basecode = CodeWithoutParts(1);

            if (layer < 7) return world.BlockAccessor.GetBlock(CodeWithPath(basecode + "-" + (layer + 1)));
            return world.BlockAccessor.GetBlock(CodeWithPath(basecode.Replace("layer", "block")));
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

            if (block is BlockLayeredSlowDig)
            {
                Block nextBlock = ((BlockLayeredSlowDig)block).GetNextLayer(world);
                if (nextBlock == null) return false;

                world.BlockAccessor.SetBlock(nextBlock.BlockId, blockSel.Position.AddCopy(blockSel.Face.Opposite));

                return true;
            }

            block = world.BlockAccessor.GetBlock(blockSel.Position);
            

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
                            world.SpawnItemEntity(drops[i], new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
                        }
                    }

                    world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos.X, pos.Y, pos.Z, byPlayer);
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
