using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{

    /// <summary>
    /// Only allows a block to exist if it is attached to another block.
    /// Uses the code "unstable".
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "Unstable",
	///		"properties": {
	///			"attachedToFaces": [ "up" ]
	///		}
	///	}
	///]
    /// </code></example>
    [DocumentAsJson]
    [AddDocumentationProperty("AttachedToFaces","A list of faces on this block that can be attached to other blocks.", "System.String[]", "Optional", "Down")]
    [AddDocumentationProperty("AttachmentAreas", "A list of attachment areas per face that determine what blocks can be attached to.", "System.Collections.Generic.Dictionary{System.String,Vintagestory.API.Datastructures.RotatableCube}", "Optional", "None")]
    public class BlockBehaviorUnstable : BlockBehavior
    {

        /// <summary>
        /// A list of faces on this block that can be attached to other blocks.
        /// </summary>
        BlockFacing[] AttachedToFaces;

        /// <summary>
        /// A list of attachment areas per face that determine what blocks can be attached to.
        /// </summary>
        Dictionary<string, Cuboidi> attachmentAreas;

        public BlockBehaviorUnstable(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            AttachedToFaces = new BlockFacing[] { BlockFacing.DOWN };

            if (properties["attachedToFaces"].Exists)
            {
                string[] faces = properties["attachedToFaces"].AsArray<string>();
                AttachedToFaces = new BlockFacing[faces.Length];

                for (int i = 0; i < faces.Length; i++)
                {
                    AttachedToFaces[i] = BlockFacing.FromCode(faces[i]);
                }
            }

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


        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            if (!IsAttached(world.BlockAccessor, blockSel.Position))
            {
                handling = EnumHandling.PreventSubsequent;
                failureCode = "requireattachable";
                return false;
            }

            return true;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handled)
        {
            if (!IsAttached(world.BlockAccessor, pos))
            {
                handled = EnumHandling.PreventDefault;
                world.BlockAccessor.BreakBlock(pos, null);
                return;
            }

            base.OnNeighbourBlockChange(world, pos, neibpos, ref handled);
        }




        public virtual bool IsAttached(IBlockAccessor blockAccessor, BlockPos pos)
        {
            for (int i = 0; i < AttachedToFaces.Length; i++)
            {
                BlockFacing face = AttachedToFaces[i];

                Block block = blockAccessor.GetBlock(pos.AddCopy(face));

                Cuboidi attachmentArea = null;
                attachmentAreas?.TryGetValue(face.Code, out attachmentArea);

                if (block.CanAttachBlockAt(blockAccessor, this.block, pos.AddCopy(face), face.Opposite, attachmentArea))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
