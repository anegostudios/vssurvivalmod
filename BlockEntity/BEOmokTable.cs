using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    public class BlockEntityOmokTable : BlockEntityDisplay
    {
        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "omoktable";
        public override string AttributeTransformCode => "onshelfTransform";

        public float MeshAngleRad { get; set; }

        InventoryGeneric inv;
        int size = 15;

        public BlockEntityOmokTable()
        {
            inv = new InventoryGeneric(size*size, "omoktable-0", null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            CollectibleObject colObj = slot.Itemstack?.Collectible;
            bool placeable = colObj?.Attributes != null && colObj.Attributes["omokpiece"].AsBool(false) == true;

            if (slot.Empty || !placeable)
            {
                if (TryTake(byPlayer, blockSel))
                {
                    return true;
                }
                return false;
            }
            else
            {
                if (placeable)
                {
                    AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;
                    if (TryPut(slot, blockSel))
                    {
                        Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                        return true;
                    }

                    return false;
                }
            }

            return false;
        }



        private bool TryPut(ItemSlot slot, BlockSelection blockSel)
        {
            int index = blockSel.SelectionBoxIndex;
            if (index < 0 || index >= inv.Count) return false;

            if (inv[index].Empty)
            {
                int moved = slot.TryPutInto(Api.World, inv[index]);
                updateMeshes();
                MarkDirty(true);
                return moved > 0;
            }

            return false;
        }

        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            int index = blockSel.SelectionBoxIndex;
            if (index < 0 || index >= inv.Count) return false;

            if (!inv[index].Empty)
            {
                ItemStack stack = inv[index].TakeOut(1);
                if (byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    AssetLocation sound = stack.Block?.Sounds?.Place;
                    Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                }

                if (stack.StackSize > 0)
                {
                    Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }

                updateMeshes();
                MarkDirty(true);
                return true;
            }

            return false;
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            Matrixf mat = new Matrixf();
            for (int i = 0; i < size*size; i++)
            {
                ItemSlot slot = Inventory[i];
                if (slot.Empty)
                {
                    continue;
                }
                
                mat.Identity();
                int dx = i % size;
                int dz = i / size;
                mat.Translate((0.6f + dx) / 16f, 0, (0.6f + dz) / 16f);
                mesher.AddMeshData(getMesh(slot.Itemstack), mat.Values);
            }

            return false;
        }

        public override void updateMeshes()
        {
            for (int i = 0; i < DisplayedItems; i++)
            {
                updateMesh(i);
            }
        }

        protected override float[][] genTransformationMatrices()
        {
            throw new NotImplementedException();
        }
    }
}
