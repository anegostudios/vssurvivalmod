using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockGroundAndSideAttachable : Block
    {
        Dictionary<string, Cuboidi> attachmentAreas;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            var areas = Attributes?["attachmentAreas"].AsObject<Dictionary<string, RotatableCube>>(null);
            if (areas != null)
            {
                attachmentAreas = new Dictionary<string, Cuboidi>();
                foreach (var val in areas)
                {
                    val.Value.Origin.Set(8, 8, 8);
                    attachmentAreas[val.Key] = val.Value.RotatedCopy().ConvertToCuboidi();
                }
            }
        }


        public override string GetHeldTpIdleAnimation(ItemSlot activeHotbarSlot, Entity forEntity, EnumHand hand)
        {
            IPlayer player = (forEntity as EntityPlayer)?.Player;

            if (forEntity.AnimManager.IsAnimationActive("sleep", "wave", "cheer", "shrug", "cry", "nod", "facepalm", "bow", "laugh", "rage", "scythe", "bowaim", "bowhit"))
            {
                return null;
            }

            if (player?.InventoryManager?.ActiveHotbarSlot != null && !player.InventoryManager.ActiveHotbarSlot.Empty && hand == EnumHand.Left)
            {
                ItemStack stack = player.InventoryManager.ActiveHotbarSlot.Itemstack;
                if (stack?.Collectible?.GetHeldTpIdleAnimation(player.InventoryManager.ActiveHotbarSlot, forEntity, EnumHand.Right) != null)
                {
                    return null;
                }

                if (player?.Entity?.Controls.LeftMouseDown == true)
                {
                    var anim = stack?.Collectible?.GetHeldTpHitAnimation(player.InventoryManager.ActiveHotbarSlot, forEntity);
                    if (anim != null && anim != "knap") return null;
                }
            }

            return hand == EnumHand.Left ? "holdinglanternlefthand" : "holdinglanternrighthand";
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (byPlayer.Entity.Controls.ShiftKey)
            {
                failureCode = "__ignore__";
                return false;
            }

            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            // Prefer selected block face
            if (blockSel.Face.IsHorizontal || blockSel.Face == BlockFacing.UP)
            {
                if (TryAttachTo(world, blockSel.Position, blockSel.Face, itemstack)) return true;
            }

            // Otherwise attach to any possible face

            BlockFacing[] faces = BlockFacing.ALLFACES;
            for (int i = 0; i < faces.Length; i++)
            {
                if (faces[i] == BlockFacing.DOWN) continue;

                if (TryAttachTo(world, blockSel.Position, faces[i], itemstack)) return true;
            }

            failureCode = "requireattachable";

            return false;
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return GetHandbookDropsFromBreakDrops(handbookStack, forPlayer);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithVariant("orientation", "up"));
            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithVariant("orientation", "up"));
            return new ItemStack(block);
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (HasBehavior<BlockBehaviorUnstableFalling>())
            {
                base.OnNeighbourBlockChange(world, pos, neibpos);
                return;
            }

            if (!CanStay(world.BlockAccessor, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }

        bool TryAttachTo(IWorldAccessor world, BlockPos blockpos, BlockFacing onBlockFace, ItemStack byItemstack)
        {
            BlockFacing onFace = onBlockFace;

            BlockPos attachingBlockPos = blockpos.AddCopy(onBlockFace.Opposite);
            Block block = world.BlockAccessor.GetBlock(attachingBlockPos);

            Cuboidi attachmentArea = null;
            attachmentAreas?.TryGetValue(onBlockFace.Opposite.Code, out attachmentArea);

            if (block.CanAttachBlockAt(world.BlockAccessor, this, attachingBlockPos, onFace, attachmentArea))
            {
                int blockId = world.BlockAccessor.GetBlock(CodeWithVariant("orientation", onBlockFace.Code)).BlockId;
                world.BlockAccessor.SetBlock(blockId, blockpos, byItemstack);
                return true;
            }

            return false;
        }

        bool CanStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockFacing facing = BlockFacing.FromCode(Variant["orientation"]);
            BlockPos attachingBlockPos = pos.AddCopy(facing.Opposite);

            Block block = blockAccessor.GetBlock(attachingBlockPos);

            Cuboidi attachmentArea = null;
            attachmentAreas?.TryGetValue(facing.Opposite.Code, out attachmentArea);

            return block.CanAttachBlockAt(blockAccessor, this, attachingBlockPos, facing, attachmentArea);
        }

        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            return false;
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            if (Variant["orientation"] == "up") return Code;

            BlockFacing oldFacing = BlockFacing.FromCode(Variant["orientation"]);
            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[((360 - angle) / 90 + oldFacing.HorizontalAngleIndex) % 4];

            return CodeWithParts(newFacing.Code);
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
        {
            BlockFacing facing = BlockFacing.FromCode(Variant["orientation"]);
            if (facing.Axis == axis)
            {
                return CodeWithVariant("orientation", facing.Opposite.Code);
            }
            return Code;
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
        {
            return CanStay(blockAccessor, pos) && base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldGenRand, attributes);
        }


    }
}
