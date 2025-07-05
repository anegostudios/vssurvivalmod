using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Forces a block to only allow placement on the side of another block. Requires use of the "horizontalorientation" variant group.
    /// Uses the code "HorizontalAttachable".
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "HorizontalAttachable",
	///		"properties": {
	///			"handleDrops": false,
	///			"attachmentAreas": {
	///				"north": {
	///					"x1": 7,
	///					"y1": 0,
	///					"z1": 15,
	///					"x2": 8,
	///					"y2": 6,
	///					"z2": 15,
	///					"rotateY": 180
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
	///]
    /// </code>
    /// <code lang="json">
    ///"variantgroups": [
	///	{
	///		"code": "side",
	///		"loadFromProperties": "abstract/horizontalorientation"
	///	}
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorHorizontalAttachable : BlockBehavior
    {
        /// <summary>
        /// Should the drops be handled by this behavior? If true, then uses values from <see cref="dropBlockFace"/> or <see cref="dropBlock"/>.
        /// </summary>
        [DocumentAsJson("Optional", "False")]
        bool handleDrops = true;

        /// <summary>
        /// The 'face' variant to drop when this block is mined, if <see cref="handleDrops"/> is set and <see cref="dropBlock"/> is not set.
        /// </summary>
        [DocumentAsJson("Optional", "north")]
        string dropBlockFace = "north";

        /// <summary>
        /// A custom block to drop when this block is mined, if <see cref="handleDrops"/> is set.
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        string dropBlock = null;

        /// <summary>
        /// A list of cuboids for each face which define where the object should be attached for each.
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        Dictionary<string, Cuboidi> attachmentAreas;

        public BlockBehaviorHorizontalAttachable(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            handleDrops = properties["handleDrops"].AsBool(true);

            if (properties["dropBlockFace"].Exists)
            {
                dropBlockFace = properties["dropBlockFace"].AsString();
            }
            if (properties["dropBlock"].Exists)
            {
                dropBlock = properties["dropBlock"].AsString();
            }

            var areas = properties["attachmentAreas"].AsObject<Dictionary<string, RotatableCube>>(null);
            attachmentAreas = new Dictionary<string, Cuboidi>();
            if (areas != null)
            {
                foreach (var val in areas)
                {
                    val.Value.Origin.Set(8, 8, 8);
                    attachmentAreas[val.Key] = val.Value.RotatedCopy().ConvertToCuboidi();
                }
            }
            else
            {
                attachmentAreas["up"] = properties["attachmentArea"].AsObject<Cuboidi>(null);
            }
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            handling = EnumHandling.PreventDefault;

            // Prefer selected block face
            if (blockSel.Face.IsHorizontal)
            {
                if (TryAttachTo(world, byPlayer, blockSel, itemstack, ref failureCode)) return true;
            }

            // Otherwise attach to any possible face
            BlockFacing[] faces = BlockFacing.HORIZONTALS;
            blockSel = blockSel.Clone();
            for (int i = 0; i < faces.Length; i++)
            {
                blockSel.Face = faces[i];
                if (TryAttachTo(world, byPlayer, blockSel, itemstack, ref failureCode)) return true;
            }

            failureCode = "requirehorizontalattachable";

            return false;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropQuantityMultiplier, ref EnumHandling handled)
        {
            if (handleDrops)
            {
                handled = EnumHandling.PreventDefault;
                if (dropBlock != null)
                {
                    return new ItemStack[] { new ItemStack(world.BlockAccessor.GetBlock(new AssetLocation(dropBlock))) };
                }
                return new ItemStack[] { new ItemStack(world.BlockAccessor.GetBlock(block.CodeWithParts(dropBlockFace))) };

            } else
            {
                handled = EnumHandling.PassThrough;
                return null;
            }
            
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (dropBlock != null)
            {
                return new ItemStack(world.BlockAccessor.GetBlock(new AssetLocation(dropBlock)));
            }

            return new ItemStack(world.BlockAccessor.GetBlock(block.CodeWithParts(dropBlockFace)));
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (!CanBlockStay(world, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }


        bool TryAttachTo(IWorldAccessor world, IPlayer player, BlockSelection blockSel, ItemStack itemstack, ref string failureCode)
        {
            BlockFacing oppositeFace = blockSel.Face.Opposite;

            BlockPos attachingBlockPos = blockSel.Position.AddCopy(oppositeFace);
            Block attachingBlock = world.BlockAccessor.GetBlock(attachingBlockPos);
            Block orientedBlock = world.BlockAccessor.GetBlock(block.CodeWithParts(oppositeFace.Code));

            Cuboidi attachmentArea = null;
            attachmentAreas?.TryGetValue(oppositeFace.Code, out attachmentArea);

            if (attachingBlock.CanAttachBlockAt(world.BlockAccessor, block, attachingBlockPos, blockSel.Face, attachmentArea) && orientedBlock.CanPlaceBlock(world, player, blockSel, ref failureCode))
            {
                orientedBlock.DoPlaceBlock(world, player, blockSel, itemstack);
                return true;
            }

            return false;
        }

        bool CanBlockStay(IWorldAccessor world, BlockPos pos)
        {
            string[] parts = block.Code.Path.Split('-');
            BlockFacing facing = BlockFacing.FromCode(parts[parts.Length - 1]);
            Block attachingblock = world.BlockAccessor.GetBlock(pos.AddCopy(facing));

            Cuboidi attachmentArea = null;
            attachmentAreas?.TryGetValue(facing.Code, out attachmentArea);

            return attachingblock.CanAttachBlockAt(world.BlockAccessor, block, pos.AddCopy(facing), facing.Opposite, attachmentArea);
        }


        public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace, ref EnumHandling handled, Cuboidi attachmentArea = null)
        {
            handled = EnumHandling.PreventDefault;
            return false;
        }


        public override AssetLocation GetRotatedBlockCode(int angle, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            BlockFacing beforeFacing = BlockFacing.FromCode(block.LastCodePart());
            int rotatedIndex = GameMath.Mod(beforeFacing.HorizontalAngleIndex - angle / 90, 4);
            BlockFacing nowFacing = BlockFacing.HORIZONTALS_ANGLEORDER[rotatedIndex];

            return block.CodeWithParts(nowFacing.Code);
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            BlockFacing facing = BlockFacing.FromCode(block.LastCodePart());
            if (facing.Axis == axis)
            {
                return block.CodeWithParts(facing.Opposite.Code);
            }
            return block.Code;
        }
    }
}
