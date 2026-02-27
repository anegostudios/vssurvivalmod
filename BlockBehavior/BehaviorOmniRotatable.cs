// RotateBehavior by Milo Christiansen
//
// To the extent possible under law, the person who associated CC0 with
// this project has waived all copyright and related or neighboring rights
// to this project.
//
// You should have received a copy of the CC0 legalcode along with this
// work.  If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.ServerMods
{
    public enum EnumSlabPlaceMode
    {
        Auto,
        Horizontal,
        Vertical
    }

    /// <summary>
    /// Allows complex rotation of a block depending on placement angle.
    /// Also has behavior of placing the block in the crafting grid to set a 'slab placement mode'.
    /// Requires the "rot" variant with the 6 directional states.
    /// Uses the "OmniRotatable" code.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "OmniRotatable",
	///		"properties": {
	///			"rotateSides": true,
	///			"facing": "block"
	///		}
	///	}
	///]
    /// </code>
    /// <code lang="json">
    ///"variantgroups": [
	///	{
	///		"code": "rot",
	///		"states": [ "north", "east", "south", "west", "up", "down" ]
	///	}
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorOmniRotatable : BlockBehavior
    {
        private bool rotateH = false;
        private bool rotateV = false;
        private bool rotateV4 = false;

        /// <summary>
        /// Determines where to angle the block against. 
        /// Set to "player" for placement based on the players angle. Set to "block" for placement based on the block side.
        /// </summary>
        [DocumentAsJson("Optional", "player")]
        private string facing = "player";

        /// <summary>
        /// If a slab placement mode has not been set, should the block be automatically rotated?
        /// </summary>
        [DocumentAsJson("Optional", "False")]
        private bool rotateSides = false;

        /// <summary>
        /// The chance that this block will drop its drops. Values over 1 will have no effect.
        /// </summary>
        [DocumentAsJson("Optional", "1")]
        private float dropChance = 1f;


        public string Rot => block.Variant["rot"];

        public BlockBehaviorOmniRotatable(Block block) : base(block)
        {
            
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            rotateH = properties["rotateH"].AsBool(rotateH);
            rotateV = properties["rotateV"].AsBool(rotateV);
            rotateV4 = properties["rotateV4"].AsBool(rotateV4);
            rotateSides = properties["rotateSides"].AsBool(rotateSides);
            facing = properties["facing"].AsString(facing);

            dropChance = properties["dropChance"].AsFloat(1);
        }



        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            handling = EnumHandling.PreventDefault;
            AssetLocation blockCode = null;
            Block orientedBlock;

            EnumSlabPlaceMode mode = itemstack.Attributes == null ? EnumSlabPlaceMode.Auto : (EnumSlabPlaceMode)itemstack.Attributes.GetInt("slabPlaceMode", 0);

            if (mode == EnumSlabPlaceMode.Horizontal)
            {
                string side = blockSel.HitPosition.Y < 0.5 ? "down" : "up";
                if (blockSel.Face.IsVertical) side = blockSel.Face.Opposite.Code;

                blockCode = block.CodeWithVariant("rot", side);
                orientedBlock = world.BlockAccessor.GetBlock(blockCode);
                if (orientedBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
                {
                    world.BlockAccessor.SetBlock(orientedBlock.BlockId, blockSel.Position);
                    return true;
                }
                return false;
            }

            if (mode == EnumSlabPlaceMode.Vertical)
            {
                BlockFacing[] hv = Block.SuggestedHVOrientation(byPlayer, blockSel);
                string side = hv[0].Code;
                if (blockSel.Face.IsHorizontal) side = blockSel.Face.Opposite.Code;

                blockCode = block.CodeWithVariant("rot", side);

                orientedBlock = world.BlockAccessor.GetBlock(blockCode);
                if (orientedBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
                {
                    world.BlockAccessor.SetBlock(orientedBlock.BlockId, blockSel.Position);
                    return true;
                }
                return false;
            }


            if (rotateSides)
            {
                // Simple 6 state rotator.

                if (facing.Equals("block", StringComparison.CurrentCultureIgnoreCase))
                {
                    var x = Math.Abs(blockSel.HitPosition.X - 0.5);
                    var y = Math.Abs(blockSel.HitPosition.Y - 0.5);
                    var z = Math.Abs(blockSel.HitPosition.Z - 0.5);
                    switch (blockSel.Face.Axis)
                    {
                        case EnumAxis.X:
                            if (z < 0.3 && y < 0.3)
                            {
                                blockCode = block.CodeWithVariant("rot", blockSel.Face.Opposite.Code);
                            }
                            else if (z > y)
                            {
                                blockCode = block.CodeWithVariant("rot", blockSel.HitPosition.Z < 0.5 ? "north" : "south");
                            }
                            else
                            {
                                blockCode = block.CodeWithVariant("rot", blockSel.HitPosition.Y < 0.5 ? "down" : "up");
                            }
                            break;

                        case EnumAxis.Y:
                            if (z < 0.3 && x < 0.3)
                            {
                                blockCode = block.CodeWithVariant("rot", blockSel.Face.Opposite.Code);
                            }
                            else if (z > x)
                            {
                                blockCode = block.CodeWithVariant("rot", blockSel.HitPosition.Z < 0.5 ? "north" : "south");
                            }
                            else
                            {
                                blockCode = block.CodeWithVariant("rot", blockSel.HitPosition.X < 0.5 ? "west" : "east");
                            }
                            break;

                        case EnumAxis.Z:
                            if (x < 0.3 && y < 0.3)
                            {
                                blockCode = block.CodeWithVariant("rot", blockSel.Face.Opposite.Code);
                            }
                            else if (x > y)
                            {
                                blockCode = block.CodeWithVariant("rot", blockSel.HitPosition.X < 0.5 ? "west" : "east");
                            }
                            else
                            {
                                blockCode = block.CodeWithVariant("rot", blockSel.HitPosition.Y < 0.5 ? "down" : "up");
                            }
                            break;
                    }
                }
                else
                {
                    if (blockSel.Face.IsVertical)
                    {
                        blockCode = block.CodeWithVariant("rot", blockSel.Face.Opposite.Code);
                    }
                    else
                    {
                        blockCode = block.CodeWithVariant("rot", BlockFacing.HorizontalFromYaw(byPlayer.Entity.Pos.Yaw).Code);
                    }
                }
            }
            else if (rotateH || rotateV)
            {
                // Complex 4/8/16 state rotator.
                string h = "north";
                string v = "up";
                if (blockSel.Face.IsVertical)
                {
                    v = blockSel.Face.Code;
                    h = BlockFacing.HorizontalFromYaw(byPlayer.Entity.Pos.Yaw).Code;
                }
                else if (rotateV4)
                {
                    if (facing == "block")
                    {
                        h = blockSel.Face.Opposite.Code;
                    }
                    else
                    {
                        // Default to player facing.
                        h = BlockFacing.HorizontalFromYaw(byPlayer.Entity.Pos.Yaw).Code;
                    }
                    switch (blockSel.Face.Axis)
                    {
                        case EnumAxis.X:
                            // Find the axis farther from the center.
                            if (Math.Abs(blockSel.HitPosition.Z - 0.5) > Math.Abs(blockSel.HitPosition.Y - 0.5))
                            {
                                v = blockSel.HitPosition.Z < 0.5 ? "left" : "right";
                            }
                            else
                            {
                                v = blockSel.HitPosition.Y < 0.5 ? "up" : "down";
                            }
                            break;

                        case EnumAxis.Z:
                            if (Math.Abs(blockSel.HitPosition.X - 0.5) > Math.Abs(blockSel.HitPosition.Y - 0.5))
                            {
                                v = blockSel.HitPosition.X < 0.5 ? "left" : "right";
                            }
                            else
                            {
                                v = blockSel.HitPosition.Y < 0.5 ? "up" : "down";
                            }
                            break;
                    }
                }
                else
                {
                    v = blockSel.HitPosition.Y < 0.5 ? "up" : "down";
                }

                if (rotateH && rotateV)
                {
                    blockCode = block.CodeWithVariants(new string[] { "v", "rot" }, new string[] { v, h });
                }
                else if (rotateH)
                {
                    blockCode = block.CodeWithVariant("rot", h);
                }
                else if (rotateV)
                {
                    blockCode = block.CodeWithVariant("rot", v);
                }
            }

            if (blockCode == null)
            {
                blockCode = this.block.Code;
            }

            orientedBlock = world.BlockAccessor.GetBlock(blockCode);
            if (orientedBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                world.BlockAccessor.SetBlock(orientedBlock.BlockId, blockSel.Position);
                return true;
            }

            return false;
        }

        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, IRecipeBase byRecipe, ref EnumHandling handled)
        {
            ItemSlot inputSlot = allInputslots.FirstOrDefault(s => !s.Empty);

            Block inBlock = inputSlot.Itemstack.Block;

            if (inBlock == null || !inBlock.HasBehavior<BlockBehaviorOmniRotatable>())
            {
                base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe, ref handled);
                return;
            }

            int mode = inputSlot.Itemstack.Attributes.GetInt("slabPlaceMode", 0);
            int nowMode = (mode + 1) % 3;
            if (nowMode == 0)
            {
                outputSlot.Itemstack.Attributes.RemoveAttribute("slabPlaceMode");
            } else
            {
                outputSlot.Itemstack.Attributes.SetInt("slabPlaceMode", nowMode);
            }
            

            base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe, ref handled);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            ItemStack[] drops = block.GetDrops(world, pos, null);

            return drops != null && drops.Length > 0 ? drops[0] : new ItemStack(block);
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropChanceMultiplier, ref EnumHandling handling)
        {
            if (dropChance < 1)
            {
                if (world.Rand.NextDouble() > dropChance)
                {
                    handling = EnumHandling.PreventDefault;
                    return Array.Empty<ItemStack>();
                }
            }

            return base.GetDrops(world, pos, byPlayer, ref dropChanceMultiplier, ref handling);
        }


        public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace, ref EnumHandling handling, Cuboidi attachmentArea = null)
        {
            if (Rot == "down")
            {
                handling = EnumHandling.PreventDefault;
                return blockFace == BlockFacing.DOWN || (attachmentArea != null && attachmentArea.Y2 < 8);
            }
            if (Rot == "up")
            {
                handling = EnumHandling.PreventDefault;
                return blockFace == BlockFacing.UP || (attachmentArea != null && attachmentArea.Y1 > 7);
            }

            return base.CanAttachBlockAt(world, block, pos, blockFace, ref handling, attachmentArea);
        }

        public override AssetLocation GetRotatedBlockCode(int angle, ref EnumHandling handling)
        {
            BlockFacing curFacing = BlockFacing.FromCode(block.Variant["rot"]);
            if (curFacing == null) return block.Code;
            if (curFacing.IsVertical) return block.Code;

            handling = EnumHandling.PreventDefault;
            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[((360 - angle) / 90 + curFacing.HorizontalAngleIndex) % 4];


            if (rotateV4)
            {
                string v = block.Variant["v"];
                if (angle == 90 && (curFacing == BlockFacing.WEST || curFacing == BlockFacing.EAST) || (angle == 270 && curFacing == BlockFacing.SOUTH))
                {
                    if (block.Variant["v"] == "left") v = "right";  
                    if (block.Variant["v"] == "right") v = "left";
                }

                return block.CodeWithVariants(new string[] { "rot", "v" }, new string[] { newFacing.Code, v });
            }

            return block.CodeWithVariant("rot", newFacing.Code);
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            BlockFacing curFacing = BlockFacing.FromCode(block.Variant["rot"]);
            if (curFacing.Axis == axis) return block.CodeWithVariant("rot", curFacing.Opposite.Code);

            return block.Code;
        }

        public override AssetLocation GetVerticallyFlippedBlockCode(ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            BlockFacing curFacing = BlockFacing.FromCode(block.Variant["rot"]);
            if (curFacing.IsVertical) return block.CodeWithVariant("rot", curFacing.Opposite.Code);

            curFacing = BlockFacing.FromCode(block.Variant["v"]);
            if (curFacing != null && curFacing.IsVertical) return block.CodeWithParts(curFacing.Opposite.Code, block.LastCodePart());


            return block.Code;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            EnumSlabPlaceMode mode = (EnumSlabPlaceMode)itemstack.Attributes.GetInt("slabPlaceMode", 0);
            if (mode == EnumSlabPlaceMode.Vertical)
            {
                renderinfo.Transform = renderinfo.Transform.Clone();
                renderinfo.Transform.Rotation.X = -80;
                renderinfo.Transform.Rotation.Y = 0;
                renderinfo.Transform.Rotation.Z = -22.5f;
            }
            if (mode == EnumSlabPlaceMode.Horizontal)
            {
                renderinfo.Transform = renderinfo.Transform.Clone();
                renderinfo.Transform.Rotation.X = 5;
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        public override string GetHeldBlockInfo(IWorldAccessor world, ItemSlot inSlot)
        {
            EnumSlabPlaceMode mode = (EnumSlabPlaceMode)inSlot.Itemstack.Attributes.GetInt("slabPlaceMode", 0);
            switch (mode)
            {
                case EnumSlabPlaceMode.Auto:
                    return Lang.Get("slab-placemode-auto") + "\n";
                case EnumSlabPlaceMode.Horizontal:
                    return Lang.Get("slab-placemode-horizontal") + "\n";
                case EnumSlabPlaceMode.Vertical:
                    return Lang.Get("slab-placemode-vertical") + "\n";

            }

            return base.GetHeldBlockInfo(world, inSlot);
        }
    }
}
