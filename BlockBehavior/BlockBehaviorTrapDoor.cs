﻿using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Marks a block as an animated trapdoor. Requires the use of the <see cref="BEBehaviorTrapDoor"/> block entity behavior.
    /// Uses the "TrapDoor" code.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{
	///		"name": "TrapDoor" 
	///	}
	///],
    ///...
    ///"attributes": {
	///	"airtightByType": {
	///		"trapdoor-solid*": true,
	///		"*": false
	///	},
	///	"openSound": "sounds/block/door",
	///	"closeSound": "sounds/block/door"
	///}
    /// </code></example>
    [DocumentAsJson]
    [AddDocumentationProperty("TriggerSound", "The sound to play when the trapdoor is opened or closed.", "System.String", "Optional", "sounds/block/door", true)]
    public class BlockBehaviorTrapDoor : StrongBlockBehavior
    {
        /// <summary>
        /// The sound to play when the trapdoor is opened.
        /// </summary>
        [DocumentAsJson("Optional", "sounds/block/door", true)]
        public AssetLocation OpenSound;

        /// <summary>
        /// The sound to play when the trapdoor is closed.
        /// </summary>
        [DocumentAsJson("Optional", "sounds/block/door", true)]
        public AssetLocation CloseSound;

        /// <summary>
        /// Can the trapdoor be opened by hand?
        /// </summary>
        [DocumentAsJson("Optional", "True", true)]
        public bool handopenable;

        /// <summary>
        /// Is the trapdoor classed as being airtight?
        /// </summary>
        [DocumentAsJson("Optional", "True", true)]
        public bool airtight;

        ICoreAPI api;
        public MeshData animatableOrigMesh;
        public Shape animatableShape;
        public string animatableDictKey;

        public BlockBehaviorTrapDoor(Block block) : base(block)
        {
            airtight = block.Attributes["airtight"].AsBool(true);
            handopenable = block.Attributes["handopenable"].AsBool(true);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;
            OpenSound = CloseSound = AssetLocation.Create(block.Attributes["triggerSound"].AsString("sounds/block/door"));

            if (block.Attributes["openSound"].Exists) OpenSound = AssetLocation.Create(block.Attributes["openSound"].AsString("sounds/block/door"));
            if (block.Attributes["closeSound"].Exists) CloseSound = AssetLocation.Create(block.Attributes["closeSound"].AsString("sounds/block/door"));

            base.OnLoaded(api);
        }


        public override void Activate(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs, ref EnumHandling handled)
        {
            var beh = world.BlockAccessor.GetBlockEntity(blockSel.Position)?.GetBehavior<BEBehaviorTrapDoor>();

            bool opened = !beh.Opened;
            if (activationArgs != null)
            {
                opened = activationArgs.GetBool("opened", opened);
            }

            if (beh.Opened != opened)
            {
                beh.ToggleDoorState(null, opened);
            }
        }

        public override void OnDecalTesselation(IWorldAccessor world, MeshData decalMesh, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            var beh = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorTrapDoor>();
            if (beh != null)
            {
                decalMesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, beh.RotRad, 0);
            }
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            handling = EnumHandling.PreventDefault;
            BlockPos pos = blockSel.Position;
            IBlockAccessor ba = world.BlockAccessor;

            if (ba.GetBlock(pos, BlockLayersAccess.Solid).Id == 0 && block.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return placeDoor(world, byPlayer, itemstack, blockSel, pos, ba);
            }

            return false;
        }

        public bool placeDoor(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, BlockPos pos, IBlockAccessor ba)
        {
            ba.SetBlock(block.BlockId, pos);
            var bh = ba.GetBlockEntity(pos)?.GetBehavior<BEBehaviorTrapDoor>();
            bh.OnBlockPlaced(itemstack, byPlayer, blockSel);
            return true;
        }

        
        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventSubsequent;
            return blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorTrapDoor>()?.ColSelBoxes ?? null;
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventSubsequent;
            return blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorTrapDoor>()?.ColSelBoxes ?? null;
        }

        public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventSubsequent;
            return blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorTrapDoor>()?.ColSelBoxes ?? null;
        }

        public override Cuboidf GetParticleBreakBox(IBlockAccessor blockAccess, BlockPos pos, BlockFacing facing, ref EnumHandling handled)
        {
            return base.GetParticleBreakBox(blockAccess, pos, facing, ref handled);
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData, ref EnumHandling handled)
        {
            var beh = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorTrapDoor>();

            if (beh.Opened)
            {
                decalModelData = decalModelData.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 90 * GameMath.DEG2RAD, 0, 0);
                decalModelData = decalModelData.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 1, -1f, 1);
            }
            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData, ref handled);
        }

        public override void GetHeldItemName(StringBuilder sb, ItemStack itemStack)
        {
            doorNameWithMaterial(sb);
        }

        public override void GetPlacedBlockName(StringBuilder sb, IWorldAccessor world, BlockPos pos)
        {
            // Already set in Block.GetPlacedBlockName()
        }

        private void doorNameWithMaterial(StringBuilder sb)
        {
            if (block.Variant.ContainsKey("wood"))
            {
                string doorname = sb.ToString();
                sb.Clear();
                sb.Append(Lang.Get("doorname-with-material", doorname, Lang.Get("material-" + block.Variant["wood"])));
            }
        }

        public override float GetLiquidBarrierHeightOnSide(BlockFacing face, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            var beh = block.GetBEBehavior<BEBehaviorTrapDoor>(pos);
            if (beh == null) return 0f;

            if (!beh.IsSideSolid(face)) return 0f;

            if (!airtight) return 0f;

            return 1f;
        }

        public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            var beh = block.GetBEBehavior<BEBehaviorTrapDoor>(pos);
            if (beh == null) return 0;

            if (type == EnumRetentionType.Sound) return beh.IsSideSolid(facing) ? 3 : 0;

            if (!airtight) return 0;
            if (api.World.Config.GetBool("openDoorsNotSolid", false)) return beh.IsSideSolid(facing) ? getInsulation(pos) : 0;
            return (beh.IsSideSolid(facing) || beh.IsSideSolid(facing.Opposite)) ? getInsulation(pos) : 3; // Also check opposite so the door can be facing inwards or outwards.
        }

        int getInsulation(BlockPos pos)
        {
            var mat = block.GetBlockMaterial(api.World.BlockAccessor, pos);
            if (mat == EnumBlockMaterial.Ore || mat == EnumBlockMaterial.Stone || mat == EnumBlockMaterial.Soil || mat == EnumBlockMaterial.Ceramic)
            {
                return -1;
            }
            return 1;
        }
    }
}
