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
    public class BlockFullCoating : Block
    {
        BlockFacing[] ownFacings;
        Cuboidf[] selectionBoxes;

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos)
        {
            return selectionBoxes;
        }

        public override void OnLoaded(ICoreAPI api)
        {
            string facingletters = FirstCodePart(1);

            ownFacings = new BlockFacing[facingletters.Length];
            selectionBoxes = new Cuboidf[ownFacings.Length];

            for (int i = 0; i < facingletters.Length; i++)
            {
                ownFacings[i] = BlockFacing.FromFirstLetter(facingletters[i]);
                switch(facingletters[i])
                {
                    case 'n':
                        selectionBoxes[i] = new Cuboidf(0, 0, 0, 1, 1, 0.0625f);
                        break;
                    case 'e':
                        selectionBoxes[i] = new Cuboidf(0, 0, 0, 1, 1, 0.0625f).RotatedCopy(0, 270, 0, new Vec3d(0.5, 0.5, 0.5));
                        break;
                    case 's':
                        selectionBoxes[i] = new Cuboidf(0, 0, 0, 1, 1, 0.0625f).RotatedCopy(0, 180, 0, new Vec3d(0.5, 0.5, 0.5));
                        break;
                    case 'w':
                        selectionBoxes[i] = new Cuboidf(0, 0, 0, 1, 1, 0.0625f).RotatedCopy(0, 90, 0, new Vec3d(0.5, 0.5, 0.5));
                        break;
                    case 'u':
                        selectionBoxes[i] = new Cuboidf(0, 0, 0, 1, 0.0625f, 1).RotatedCopy(180, 0, 0, new Vec3d(0.5, 0.5, 0.5));
                        break;
                    case 'd':
                        selectionBoxes[i] = new Cuboidf(0, 0, 0, 1, 0.0625f, 1);
                        break;
                }
                
            }
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                failureCode = "claimed";
                return false;
            }

            if (!IsSuitablePosition(world, blockSel.Position, ref failureCode))
            {
                return false;
            }

            return TryPlaceBlockForWorldGen(world.BlockAccessor, blockSel.Position, blockSel.Face, world.Rand);
        }

        public override BlockDropItemStack[] GetDropsForHandbook(IWorldAccessor world, BlockPos pos, IPlayer byPlayer)
        {
            return GetHandbookDropsFromBreakDrops(world, pos, byPlayer);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            int quantity = 0;
            for (int i = 0; i < ownFacings.Length; i++) quantity += world.Rand.NextDouble() > Drops[0].Quantity.nextFloat() ? 1 : 0;

            ItemStack stack = Drops[0].ResolvedItemstack.Clone();
            stack.StackSize = Math.Max(1, (int)quantity);
            return new ItemStack[] { stack };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("d"));
            return new ItemStack(block);
        }

        public override void OnNeighourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            string newFacingLetters = "";
            foreach (BlockFacing facing in ownFacings)
            {
                Block block = world.BlockAccessor.GetBlock(pos.X + facing.Normali.X, pos.Y + facing.Normali.Y, pos.Z + facing.Normali.Z);

                if (block.SideSolid[facing.GetOpposite().Index])
                {
                    newFacingLetters += facing.Code.Substring(0, 1);
                }
            }

            if (ownFacings.Length <= newFacingLetters.Length) return;

            if (newFacingLetters.Length == 0)
            {
                world.BlockAccessor.BreakBlock(pos, null);
                return;
            }

            int diff = newFacingLetters.Length - ownFacings.Length;
            for (int i = 0; i < diff; i++)
            {
                world.SpawnItemEntity(Drops[0].GetNextItemStack(), pos.ToVec3d().AddCopy(0.5, 0.5, 0.5));
            }

            Block newblock = world.GetBlock(CodeWithPath(FirstCodePart() + "-" + newFacingLetters));
            world.BlockAccessor.SetBlock(newblock.BlockId, pos);
        }

        


        public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace)
        {
            return false;
        }


        public string getSolidFacesAtPos(IBlockAccessor blockAccessor, BlockPos pos)
        {
            string facings = "";

            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                Block block = blockAccessor.GetBlock(pos.X + facing.Normali.X, pos.Y + facing.Normali.Y, pos.Z + facing.Normali.Z);

                if (block.SideSolid[facing.GetOpposite().Index])
                {
                    facings += facing.Code.Substring(0, 1);
                }
            }

            return facings;
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, Random worldGenRand)
        {
            if (pos.Y < 16) return false;

            string facings = getSolidFacesAtPos(blockAccessor, pos);

            if (facings.Length > 0)
            {
                Block block = blockAccessor.GetBlock(CodeWithPath(FirstCodePart() + "-" + facings));
                blockAccessor.SetBlock(block.BlockId, pos);
            }

            return true;
        }


    }
}
