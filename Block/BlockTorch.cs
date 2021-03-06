﻿using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockTorch : Block
    {
        bool IsExtinct => Variant["variant"] == "extinct";

        Dictionary<string, Cuboidi> attachmentAreas;

        public Block ExtinctVariant { get; private set; }

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

            AssetLocation loc = AssetLocation.Create(FirstCodePart() + "-extinct-up", Code.Domain);
            ExtinctVariant = api.World.GetBlock(loc);
        }

        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            if (api.World.Side == EnumAppSide.Server && byEntity.Swimming && !IsExtinct && ExtinctVariant != null)
            {
                api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), byEntity.Pos.X + 0.5, byEntity.Pos.Y + 0.75, byEntity.Pos.Z + 0.5, null, false, 16);
                
                int q = slot.Itemstack.StackSize;
                slot.Itemstack = new ItemStack(ExtinctVariant);
                slot.Itemstack.StackSize = q;
                slot.MarkDirty();
            }
            
        }

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

            float stormstr = api.ModLoader.GetModSystem<SystemTemporalStability>().StormStrength;

            if (!IsExtinct && attackedEntity != null && byEntity.World.Side == EnumAppSide.Server && api.World.Rand.NextDouble() < 0.1 + stormstr)
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

            if (forEntity.AnimManager.IsAnimationActive("sleep", "wave", "cheer", "shrug", "cry", "nod", "facepalm", "bow", "laugh", "rage", "scythe", "bowaim", "bowhit"))
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


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (HasBehavior<BlockBehaviorUnstableFalling>())
            {
                base.OnNeighbourBlockChange(world, pos, neibpos);
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

            BlockPos attachingBlockPos = blockpos.AddCopy(onBlockFace.Opposite);
            Block block = world.BlockAccessor.GetBlock(attachingBlockPos);

            Cuboidi attachmentArea = null;
            attachmentAreas?.TryGetValue(onBlockFace.Opposite.Code, out attachmentArea);

            if (block.CanAttachBlockAt(world.BlockAccessor, this, attachingBlockPos, onFace, attachmentArea))
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
            if (LastCodePart() == "up") return Code;

            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[((360 - angle) / 90 + BlockFacing.FromCode(LastCodePart()).HorizontalAngleIndex) % 4];
            return CodeWithParts(newFacing.Code);
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
        {
            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            if (facing.Axis == axis)
            {
                return CodeWithParts(facing.Opposite.Code);
            }
            return Code;
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            return CanTorchStay(blockAccessor, pos) && base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldGenRand);
        }

    }
}
