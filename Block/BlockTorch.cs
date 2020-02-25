using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockTorch : Block
    {
        bool IsExtinct => Variant["variant"] == "extinct";

        public override EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            if (IsExtinct)
            {
                return secondsIgniting > 1 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
            }

            return EnumIgniteState.NotIgnitablePreventDefault;
        }

        public override void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            if (IsExtinct)
            {
                handling = EnumHandling.PreventDefault;
                AssetLocation loc = new AssetLocation(FirstCodePart() + "-" + Variant["orientation"]);
                Block block = api.World.BlockAccessor.GetBlock(loc);
                if (block != null)
                {
                    api.World.BlockAccessor.SetBlock(block.Id, pos);
                }

                return;
            }
        }

        public override void OnAttackingWith(IWorldAccessor world, Entity byEntity, Entity attackedEntity, ItemSlot itemslot)
        {
            base.OnAttackingWith(world, byEntity, attackedEntity, itemslot);

            if (attackedEntity != null && byEntity.World.Side == EnumAppSide.Server && api.World.Rand.NextDouble() < 0.1)
            {
                attackedEntity.Ignite();
            }
        }

        public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            base.OnHeldAttackStop(secondsPassed, slot, byEntity, blockSelection, entitySel);

        }


        public override string GetHeldTpIdleAnimation(ItemSlot activeHotbarSlot, Entity forEntity, EnumHand hand)
        {
            IPlayer player = (forEntity as EntityPlayer)?.Player;

            if (forEntity.AnimManager.IsAnimationActive("sleep", "wave", "cheer", "shrug", "cry", "nod", "facepalm", "bow", "laugh", "rage"))
            {
                return null;
            }

            if (player?.InventoryManager?.ActiveHotbarSlot != null && !player.InventoryManager.ActiveHotbarSlot.Empty && hand == EnumHand.Left)
            {
                ItemStack stack = player.InventoryManager.ActiveHotbarSlot.Itemstack;
                if (stack?.Collectible?.GetHeldTpIdleAnimation(player.InventoryManager.ActiveHotbarSlot, forEntity, EnumHand.Right) != null) return null;

                if (player?.Entity?.Controls.LeftMouseDown == true && stack?.Collectible?.GetHeldTpHitAnimation(player.InventoryManager.ActiveHotbarSlot, forEntity) != null) return null;
            }

            return hand == EnumHand.Left ? "holdinglanternlefthand" : "holdinglanternrighthand";
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (byPlayer.Entity.Controls.Sneak)
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
                if (TryAttachTo(world, blockSel.Position, blockSel.Face)) return true;
            }

            // Otherwise attach to any possible face

            BlockFacing[] faces = BlockFacing.ALLFACES;
            for (int i = 0; i < faces.Length; i++)
            {
                if (faces[i] == BlockFacing.DOWN) continue;

                if (TryAttachTo(world, blockSel.Position, faces[i])) return true;
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
            if (FirstCodePart(1) == "burnedout") return new ItemStack[0];

            Block block = world.BlockAccessor.GetBlock(CodeWithParts("up"));
            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("up"));
            return new ItemStack(block);
        }


        public override void OnNeighourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (HasBehavior<BlockBehaviorUnstableFalling>())
            {
                base.OnNeighourBlockChange(world, pos, neibpos);
                return;
            }

            if (!CanTorchStay(world.BlockAccessor, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }

        bool TryAttachTo(IWorldAccessor world, BlockPos blockpos, BlockFacing onBlockFace)
        {
            BlockFacing onFace = onBlockFace;
            //if (onFace.IsHorizontal) onFace = onFace.GetOpposite(); - why is this here? Breaks attachment

            BlockPos attachingBlockPos = blockpos.AddCopy(onBlockFace.GetOpposite());
            Block block = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlockId(attachingBlockPos));

            if (block.CanAttachBlockAt(world.BlockAccessor, this, attachingBlockPos, onFace))
            {
                int blockId = world.BlockAccessor.GetBlock(CodeWithParts(onBlockFace.Code)).BlockId;
                world.BlockAccessor.SetBlock(blockId, blockpos);
                return true;
            }

            return false;
        }

        bool CanTorchStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            Block block = blockAccessor.GetBlock(blockAccessor.GetBlockId(pos.AddCopy(facing.GetOpposite())));
            return block.CanAttachBlockAt(blockAccessor, this, pos, facing);
        }

        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace)
        {
            return false;
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            if (LastCodePart() == "up") return Code;

            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[((360 - angle) / 90 + BlockFacing.FromCode(LastCodePart()).HorizontalAngleIndex) % 4];
            return CodeWithParts(newFacing.Code);
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
        {
            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            if (facing.Axis == axis)
            {
                return CodeWithParts(facing.GetOpposite().Code);
            }
            return Code;
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            return CanTorchStay(blockAccessor, pos) && base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldGenRand);
        }

    }
}
