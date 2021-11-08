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
            base.OnLoaded(api);

            string facingletters = Variant["coating"];

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
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            return TryPlaceBlockForWorldGen(world.BlockAccessor, blockSel.Position, blockSel.Face);
        }

        
        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return GetHandbookDropsFromBreakDrops(handbookStack, forPlayer);
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
            Block block = world.BlockAccessor.GetBlock(CodeWithVariant("coating", "d"));
            return new ItemStack(block);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            string newFacingLetters = "";
            foreach (BlockFacing facing in ownFacings)
            {
                Block block = world.BlockAccessor.GetBlock(pos.X + facing.Normali.X, pos.Y + facing.Normali.Y, pos.Z + facing.Normali.Z);

                if (block.SideSolid[facing.Opposite.Index])
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

            Block newblock = world.GetBlock(CodeWithVariant("coating", newFacingLetters));
            world.BlockAccessor.SetBlock(newblock.BlockId, pos);
        }

        


        public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            return false;
        }


        public string getSolidFacesAtPos(IBlockAccessor blockAccessor, BlockPos pos)
        {
            string facings = "";

            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                Block block = blockAccessor.GetBlock(pos.X + facing.Normali.X, pos.Y + facing.Normali.Y, pos.Z + facing.Normali.Z);

                if (block.SideSolid[facing.Opposite.Index])
                {
                    facings += facing.Code.Substring(0, 1);
                }
            }

            return facings;
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            return TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace);
        }

        public bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace)
        {
            if (pos.Y < 16) return false;

            if (blockAccessor.GetBlock(pos).Replaceable < 6000) return false; // Don't place where there's solid blocks

            string facings = getSolidFacesAtPos(blockAccessor, pos);

            if (facings.Length > 0)
            {
                Block block = blockAccessor.GetBlock(CodeWithVariant("coating", facings));
                blockAccessor.SetBlock(block.BlockId, pos);
            }

            return true;
        }
    }
}
