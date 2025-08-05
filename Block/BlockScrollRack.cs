using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockScrollRack : BlockShapeMaterialFromAttributes
    {
        public Cuboidf[] slotsHitBoxes = null!;
        public string[] slotSide = null!;
        public int[] oppositeSlotIndex = null!;
        
        public override string MeshKey { get; } = "ScrollrackMeshes";
        public Dictionary<string, int[]> slotsBySide = new Dictionary<string, int[]>();

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            LoadTypes();
        }

        public override void LoadTypes()
        {
            base.LoadTypes();
            slotsHitBoxes = Attributes["slotsHitBoxes"].AsObject<Cuboidf[]>();
            slotSide = Attributes["slotSide"].AsObject<string[]>();
            oppositeSlotIndex = Attributes["oppositeSlotIndex"].AsObject<int[]>();

            for (int i = 0; i < slotSide.Length; i++)
            {
                var side = slotSide[i];
                if (slotsBySide.TryGetValue(side, out int[]? slots))
                {
                    slots = slots.Append(i);
                }
                else
                {
                    slots = [i];
                }

                slotsBySide[side] = slots;
            }
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var beb = GetBlockEntity<BlockEntityScrollRack>(pos);
            return beb?.getOrCreateSelectionBoxes() ?? base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            var beb = GetBlockEntity<BlockEntityScrollRack>(pos);
            if (beb != null)
            {
                var mat = Matrixf.Create().Translate(0.5f, 0.5f, 0.5f).RotateY(beb.MeshAngleRad).Translate(-0.5f, -0.5f, -0.5f).Values;
                blockModelData = GetOrCreateMesh(beb.Type, beb.Material).Clone().MatrixTransform(mat);
                decalModelData = GetOrCreateMesh(beb.Type, beb.Material, null, decalTexSource).Clone().MatrixTransform(mat);
                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            var beb = GetBlockEntity<BlockEntityScrollRack>(pos);
            beb?.clearUsableSlots();

            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var beshelf = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityScrollRack;
            if (beshelf != null) return beshelf.OnInteract(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            return Lang.Get("block-scrollrack-" + itemStack.Attributes.GetString("material"));
        }
    }
}
