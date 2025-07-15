using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Allows a block to be attached to any face, including vertical faces. 
    /// Requires states from "abstract/horizontalorientation", as well as additional "up" and "down" states.
    /// Uses the code "OmniAttachable".
    /// </summary>
    /// <example>
    /// <code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "OmniAttachable",
	///		"properties": {
	///			"facingCode": "position",
	///			"attachmentAreas": {
	///				"down": {
	///					"x1": 7,
	///					"y1": 15,
	///					"z1": 7,
	///					"x2": 8,
	///					"y2": 15,
	///					"z2": 8
	///				},
	///				"up": {
	///					...
	///				},
	///				"north": {
	///					...
	///				},
	///				"east": {
	///					...
	///				},
	///				"south": {
	///					...
	///				},
	///				"west": {
	///					...
	///				}
	///			}
	///		}
	///	}
	///],
    /// </code>
    /// <code lang="json">
    ///"variantgroups": [
	///	{
	///		"code": "position",
	///		"states": [ "up", "down" ],
	///		"loadFromProperties": "abstract/horizontalorientation"
	///	}
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorOmniAttachable : BlockBehavior
    {
        /// <summary>
        /// The variant code that defines the required states.
        /// </summary>
        [DocumentAsJson("Optional", "orientation")]
        public string facingCode = "orientation";

        /// <summary>
        /// A set of attachment cuboids for each attached face.
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        Dictionary<string, Cuboidi> attachmentAreas;

        public BlockBehaviorOmniAttachable(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            facingCode = properties["facingCode"].AsString("orientation");

            var areas = properties["attachmentAreas"].AsObject<Dictionary<string, RotatableCube>>(null);
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

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            handling = EnumHandling.PreventDefault;

            // Prefer selected block face
            if (TryAttachTo(world, byPlayer, blockSel.Position, blockSel.HitPosition, blockSel.Face, itemstack)) return true;


            // Otherwise attach to any possible face
            BlockFacing[] faces = BlockFacing.ALLFACES;
            for (int i = 0; i < faces.Length; i++)
            {
                //if (faces[i] == BlockFacing.DOWN) continue; - what for? o.O

                if (TryAttachTo(world, byPlayer, blockSel.Position, blockSel.HitPosition, faces[i], itemstack)) return true;
            }

            failureCode = "requireattachable";

            return false;
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropQuantityMultiplier, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            Block droppedblock = world.BlockAccessor.GetBlock(block.CodeWithVariant(facingCode, "up"));
            return new ItemStack[] { new ItemStack(droppedblock) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            Block pickedblock = world.BlockAccessor.GetBlock(block.CodeWithVariant(facingCode, "up"));
            return new ItemStack(pickedblock);
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (!CanStay(world, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }

        bool TryAttachTo(IWorldAccessor world, IPlayer byPlayer, BlockPos blockpos, Vec3d hitPosition, BlockFacing onBlockFace, ItemStack itemstack)
        {
            BlockPos attachingBlockPos = blockpos.AddCopy(onBlockFace.Opposite);
            Block attachingBlock = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlockId(attachingBlockPos));

            BlockFacing onFace = onBlockFace;

            Block hereBlock = world.BlockAccessor.GetBlock(blockpos);

            Cuboidi attachmentArea = null;
            attachmentAreas?.TryGetValue(onBlockFace.Code, out attachmentArea);

            if (hereBlock.Replaceable >= 6000 && attachingBlock.CanAttachBlockAt(world.BlockAccessor, block, attachingBlockPos, onFace, attachmentArea))
            {
                Block orientedBlock = world.BlockAccessor.GetBlock(block.CodeWithVariant(facingCode, onBlockFace.Code));
                orientedBlock.DoPlaceBlock(world, byPlayer, new BlockSelection() { Position = blockpos, HitPosition = hitPosition, Face = onFace }, itemstack);
                return true;
            }

            return false;
        }

        bool CanStay(IWorldAccessor world, BlockPos pos)
        {
            BlockFacing facing = BlockFacing.FromCode(block.Variant[facingCode]);
            BlockPos attachingBlockPos = pos.AddCopy(facing.Opposite);
            Block attachedblock = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlockId(attachingBlockPos));

            BlockFacing onFace = facing;

            Cuboidi attachmentArea = null;
            attachmentAreas?.TryGetValue(facing.Code, out attachmentArea);

            return attachedblock.CanAttachBlockAt(world.BlockAccessor, block, attachingBlockPos, onFace, attachmentArea);
        }

        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, ref EnumHandling handled, Cuboidi attachmentArea = null)
        {
            handled = EnumHandling.PreventDefault;

            return false;
        }

        public override AssetLocation GetRotatedBlockCode(int angle, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (block.Variant[facingCode] == "up" || block.Variant[facingCode] == "down") return block.Code;

            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[((360 - angle) / 90 + BlockFacing.FromCode(block.Variant[facingCode]).HorizontalAngleIndex) % 4];
            return block.CodeWithParts(newFacing.Code);
        }

        public override AssetLocation GetVerticallyFlippedBlockCode(ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            
            return block.Variant[facingCode] == "up" ? block.CodeWithVariant(facingCode, "down") : block.CodeWithVariant(facingCode, "up");
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            BlockFacing facing = BlockFacing.FromCode(block.Variant[facingCode]);
            if (facing.Axis == axis)
            {
                return block.CodeWithVariant(facingCode, facing.Opposite.Code);
            }
            return block.Code;
        }
    }
}
