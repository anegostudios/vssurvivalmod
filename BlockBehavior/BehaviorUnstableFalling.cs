using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{

    /// <summary>
    /// Spawns an EntityBlockFalling when the user places a block that has air underneath it or if a neighbor block is
    /// removed and causes air to be underneath it. Also has optional functionality to prevent a block being placed if it is unstable.
    /// Uses the code "UnstableFalling".
    /// </summary>
    [DocumentAsJson]
    [AddDocumentationProperty("AttachableFaces", "The faces that this block could be attached from which will prevent it from falling.", "System.String[]", "Optional", "None")]
    [AddDocumentationProperty("AttachmentAreas", "A list of attachment areas per face that determine what blocks can be attached to.", "System.Collections.Generic.Dictionary{System.String,Vintagestory.API.Datastructures.RotatableCube}", "Optional", "None")]
    [AddDocumentationProperty("AttachmentArea", "A single attachment area that determine what blocks can be attached to. Used if AttachmentAreas is not supplied.", "Vintagestory.API.Mathtools.Cuboidi", "Optional", "None")]
    [AddDocumentationProperty("AllowUnstablePlacement","Can this block be placed in an unstable position?","System.Boolean","Optional","False",true)]
    public class BlockBehaviorUnstableFalling : BlockBehavior
    {
        /// <summary>
        /// If true, then the block can be placed even in an position where it'll be unstable.
        /// </summary>
        [DocumentAsJson("Optional", "False")]
        bool ignorePlaceTest;

        /// <summary>
        /// A list of block types which this block can always be attached to, regardless if there is a correct attachment area.
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        AssetLocation[] exceptions;

        /// <summary>
        /// Can this block fall horizontally?
        /// </summary>
        [DocumentAsJson("Optional", "False")]
        public bool fallSideways;

        /// <summary>
        /// A multiplier for the number of dust particles for the falling block. A value of 0 means no dust particles.
        /// </summary>
        [DocumentAsJson("Optional", "0")]
        float dustIntensity;

        /// <summary>
        /// If <see cref="fallSideways"/> is enabled, this is the chance that the block will fall sideways instead of straight down.
        /// </summary>
        [DocumentAsJson("Optional", "0.3")]
        float fallSidewaysChance = 0.3f;

        /// <summary>
        /// The path to the sound to play when the block falls.
        /// </summary>
        [DocumentAsJson("Optional", "None")]
        AssetLocation fallSound;

        /// <summary>
        /// A multiplier of damage dealt to an entity when hit by the falling block. Damage depends on falling height.
        /// </summary>
        [DocumentAsJson("Optional", "1")]
        float impactDamageMul;

        /// <summary>
        /// A set of attachment areas for the unstable block. 
        /// </summary>
        Cuboidi[] attachmentAreas;

        /// <summary>
        /// The faces that this block could be attached from which will prevent it from falling.
        /// </summary>
        BlockFacing[] attachableFaces;

        public BlockBehaviorUnstableFalling(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            attachableFaces = null;

            if (properties["attachableFaces"].Exists)
            {
                string[] faces = properties["attachableFaces"].AsArray<string>();
                attachableFaces = new BlockFacing[faces.Length];

                for (int i = 0; i < faces.Length; i++)
                {
                    attachableFaces[i] = BlockFacing.FromCode(faces[i]);
                }
            }
            
            var areas = properties["attachmentAreas"].AsObject<Dictionary<string, RotatableCube>>(null);
            attachmentAreas = new Cuboidi[6];
            if (areas != null)
            {
                foreach (var val in areas)
                {
                    val.Value.Origin.Set(8, 8, 8);
                    BlockFacing face = BlockFacing.FromFirstLetter(val.Key[0]);
                    attachmentAreas[face.Index] = val.Value.RotatedCopy().ConvertToCuboidi();
                }
            } else
            {
                attachmentAreas[4] = properties["attachmentArea"].AsObject<Cuboidi>(null);
            }

            ignorePlaceTest = properties["ignorePlaceTest"].AsBool(false);
            exceptions = properties["exceptions"].AsObject(System.Array.Empty<AssetLocation>(), block.Code.Domain);
            fallSideways = properties["fallSideways"].AsBool(false);
            dustIntensity = properties["dustIntensity"].AsFloat(0);

            fallSidewaysChance = properties["fallSidewaysChance"].AsFloat(0.3f);
            string sound = properties["fallSound"].AsString(null);
            if (sound != null)
            {
                fallSound = AssetLocation.Create(sound, block.Code.Domain);
            }

            impactDamageMul = properties["impactDamageMul"].AsFloat(1f);
        }

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            handling = EnumHandling.PassThrough;
            if (ignorePlaceTest) return true;

            Cuboidi attachmentArea = attachmentAreas[4];

            BlockPos pos = blockSel.Position.DownCopy();
            Block onBlock = world.BlockAccessor.GetBlock(pos);
            if (blockSel != null && !IsAttached(world.BlockAccessor, blockSel.Position) && !onBlock.CanAttachBlockAt(world.BlockAccessor, block, pos, BlockFacing.UP, attachmentArea) && block.Attributes?["allowUnstablePlacement"].AsBool() != true && !onBlock.WildCardMatch(exceptions))
            {
                handling = EnumHandling.PreventSubsequent;
                failureCode = "requiresolidground";
                return false;
            }

            return true;
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ref EnumHandling handling)
        {
            TryFalling(world, blockPos, ref handling);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handling)
        {
            base.OnNeighbourBlockChange(world, pos, neibpos, ref handling);

            if (world.Side == EnumAppSide.Client) return;

            EnumHandling bla = EnumHandling.PassThrough;
            TryFalling(world, pos, ref bla);
        }

        private bool TryFalling(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            if (world.Side != EnumAppSide.Server) return false;
            if (!fallSideways && IsAttached(world.BlockAccessor, pos)) return false;

            ICoreServerAPI sapi = (world as IServerWorldAccessor).Api as ICoreServerAPI;
            if (!sapi.World.Config.GetBool("allowFallingBlocks")) return false;
             
            if (IsReplacableBeneath(world, pos) || (fallSideways && world.Rand.NextDouble() < fallSidewaysChance && IsReplacableBeneathAndSideways(world, pos)))
            {
                BlockPos ourPos = pos.Copy();
                // Must run a frame later. This method is called from OnBlockPlaced, but at this point - if this is a freshly settled falling block, then the BE does not have its full data yet (because EntityBlockFalling makes a SetBlock, then only calls FromTreeAttributes on the BE
                sapi.Event.EnqueueMainThreadTask(()=>{
                    var block = world.BlockAccessor.GetBlock(ourPos);
                    if (this.block != block) return; // Block was already removed

                    // Prevents duplication
                    Entity entity = world.GetNearestEntity(ourPos.ToVec3d().Add(0.5, 0.5, 0.5), 1, 1.5f, (e) =>
                    {
                        return e is EntityBlockFalling ebf && ebf.initialPos.Equals(ourPos);
                    });
                    if (entity != null) return;

                    var be = world.BlockAccessor.GetBlockEntity(ourPos);
                    EntityBlockFalling entityBf = new EntityBlockFalling(block, be, ourPos, fallSound, impactDamageMul, true, dustIntensity);

                    world.SpawnEntity(entityBf);
                }, "falling");

                handling = EnumHandling.PreventSubsequent;
                return true;
            }

            handling = EnumHandling.PassThrough;
            return false;
        }


        public virtual bool IsAttached(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockPos tmpPos;

            if (attachableFaces == null)    // shorter code path for no attachableFaces specified (common case) - we test only the block below
            {
                tmpPos = pos.DownCopy();
                Block block = blockAccessor.GetBlock(tmpPos);
                return block.CanAttachBlockAt(blockAccessor, this.block, tmpPos, BlockFacing.UP, attachmentAreas[5]);
            }

            tmpPos = new BlockPos();
            for (int i = 0; i < attachableFaces.Length; i++)
            {
                BlockFacing face = attachableFaces[i];

                tmpPos.Set(pos).Add(face);
                Block block = blockAccessor.GetBlock(tmpPos);
                if (block.CanAttachBlockAt(blockAccessor, this.block, tmpPos, face.Opposite, attachmentAreas[face.Index]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsReplacableBeneathAndSideways(IWorldAccessor world, BlockPos pos)
        {
            for (int i = 0; i < 4; i++)
            {
                BlockFacing facing = BlockFacing.HORIZONTALS[i];

                Block nBlock = world.BlockAccessor.GetBlockOrNull(pos.X + facing.Normali.X, pos.Y + facing.Normali.Y, pos.Z + facing.Normali.Z);
                if (nBlock != null && nBlock.Replaceable >= 6000)
                {
                    nBlock = world.BlockAccessor.GetBlockOrNull(pos.X + facing.Normali.X, pos.Y + facing.Normali.Y - 1, pos.Z + facing.Normali.Z);
                    if (nBlock != null && nBlock.Replaceable >= 6000)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsReplacableBeneath(IWorldAccessor world, BlockPos pos)
        {
            Block bottomBlock = world.BlockAccessor.GetBlockBelow(pos);
            return bottomBlock.Replaceable > 6000;
        }
    }
}
