using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityCrock : BlockEntityContainer, IBlockEntityMealContainer
    {
        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "crock";

        public string RecipeCode { get; set; } = "";
        public InventoryBase inventory => inv;
        public float QuantityServings { get; set; }
        public bool Sealed { get; set; }

        MeshData currentMesh;
        BlockCrock ownBlock;


        public BlockEntityCrock()
        {
            inv = new InventoryGeneric(6, "crock-0", null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            this.ownBlock = Block as BlockCrock;
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (byItemStack != null)
            {
                RecipeCode = byItemStack.Attributes.GetString("recipeCode", "");
                QuantityServings = (float)byItemStack.Attributes.GetDecimal("quantityServings");
                Sealed = byItemStack.Attributes.GetBool("sealed");
            }
        }


        protected override float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
        {
            float mul = base.Inventory_OnAcquireTransitionSpeed(transType, stack, baseMul);
            mul *= ownBlock?.GetContainingTransitionModifierPlaced(Api.World, Pos, transType) ?? 1;
            return mul;
        }

        public override void OnBlockBroken()
        {
            //base.OnBlockBroken(); - don't drop contents
        }

        private MeshData getMesh(ITesselatorAPI tesselator)
        {
            BlockCrock block = Api.World.BlockAccessor.GetBlock(Pos) as BlockCrock;
            if (block == null) return null;

            ItemStack[] stacks = inventory.Where(slot => !slot.Empty).Select(slot => slot.Itemstack).ToArray();

            Vec3f rot = new Vec3f(0, block.Shape.rotateY, 0);

            return GetMesh(tesselator, Api, block, stacks, RecipeCode, rot);
        }


        public static MeshData GetMesh(ITesselatorAPI tesselator, ICoreAPI api, BlockCrock block, ItemStack[] stacks, string recipeCode, Vec3f rot)
        {
            Dictionary<string, MeshData> meshes = ObjectCacheUtil.GetOrCreate(api, "blockCrockMeshes", () => new Dictionary<string, MeshData>());
            MeshData mesh = null;
            
            
            
            AssetLocation labelLoc = block.LabelForContents(recipeCode, stacks);

            if (labelLoc == null)
            {
                return null;
            }

            string key = labelLoc.ToShortString() + block.Code.ToShortString() + "/" + rot.Y + "/" + rot.X + "/" + rot.Z;
            if (meshes.TryGetValue(key, out mesh))
            {
                return mesh;
            }

            return meshes[key] = block.GenMesh(api as ICoreClientAPI, labelLoc, rot, tesselator);
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            QuantityServings = (float)tree.GetDecimal("quantityServings");
            RecipeCode = tree.GetString("recipeCode", "");
            Sealed = tree.GetBool("sealed");

            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                currentMesh = null;
                MarkDirty(true);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetFloat("quantityServings", QuantityServings);
            tree.SetBool("sealed", Sealed);

            if (RecipeCode != null && RecipeCode != "")
            {
                tree.SetString("recipeCode", RecipeCode);
            }
        }



        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (currentMesh == null)
            {
                currentMesh = getMesh(tesselator);
                if (currentMesh == null) return false;
            }

            mesher.AddMeshData(currentMesh);
            return true;
        }



        public void ServeInto(IPlayer player, ItemSlot slot)
        {
            float servings = Math.Min(QuantityServings, slot.Itemstack.Collectible.Attributes["servingCapacity"].AsInt());

            if (inv[0].Empty && inv[1].Empty && inv[2].Empty && inv[3].Empty) return; // Crock is empty

            Block block = Api.World.GetBlock(AssetLocation.Create(slot.Itemstack.Collectible.Attributes["mealBlockCode"].AsString(), slot.Itemstack.Collectible.Code.Domain));
            ItemStack mealstack = new ItemStack(block);
            mealstack.StackSize = 1;

            (block as IBlockMealContainer).SetContents(RecipeCode, mealstack, GetNonEmptyContentStacks(), servings);

            if (slot.StackSize == 1)
            {
                slot.Itemstack = mealstack;
            }
            else
            {
                slot.TakeOut(1);
                if (!player.InventoryManager.TryGiveItemstack(mealstack, true))
                {
                    Api.World.SpawnItemEntity(mealstack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
                slot.MarkDirty();
            }

            QuantityServings -= servings;

            if (QuantityServings <= 0)
            {
                QuantityServings = 0;
                inventory.DiscardAll();
                RecipeCode = "";
            }

            currentMesh = null;
            MarkDirty(true);
        }
    }
}
