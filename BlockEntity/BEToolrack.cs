using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockEntityToolrack : BlockEntity, IBlockShapeSupplier, ITexPositionSource
    {
        public InventoryGeneric inventory;

        MeshData[] toolMeshes = new MeshData[4];

        public int AtlasSize
        {
            get { return ((ICoreClientAPI)api).BlockTextureAtlas.Size; }
        }

        CollectibleObject tmpItem;
        public TextureAtlasPosition this[string textureCode]
        {
            get {
                ToolTextures tt = null;

                if (BlockToolRack.toolTextureSubIds.TryGetValue((Item)tmpItem, out tt))
                {
                    int textureSubId = 0;
                    if (tt.TextureSubIdsByCode.TryGetValue(textureCode, out textureSubId))
                    {
                        return ((ICoreClientAPI)api).BlockTextureAtlas.Positions[textureSubId];
                    }

                    return ((ICoreClientAPI)api).BlockTextureAtlas.Positions[tt.TextureSubIdsByCode.First().Value];
                }

                return null;
            }
        }

        public BlockEntityToolrack() : base()
        {
            inventory = new InventoryGeneric(4, "toolrack", null, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            inventory.LateInitialize("toolrack-" + pos.ToString(), api);
            inventory.ResolveBlocksOrItems();

            if (api is ICoreClientAPI)
            {
                loadToolMeshes();
            }
        }


        void loadToolMeshes()
        {
            BlockFacing facing = getFacing().GetCW();
            if (facing == null) return;

            Vec3f facingNormal = facing.Normalf;

            Vec3f origin = new Vec3f(0.5f, 0.5f, 0.5f);

            ICoreClientAPI clientApi = (ICoreClientAPI)api;

            for (int i = 0; i < 4; i++)
            {
                toolMeshes[i] = null;
                IItemStack stack = inventory.GetSlot(i).Itemstack;
                if (stack == null) continue;

                tmpItem = stack.Collectible;

                if (stack.Class == EnumItemClass.Item)
                {
                    clientApi.Tesselator.TesselateItem(stack.Item, out toolMeshes[i], this);
                } else
                {
                    clientApi.Tesselator.TesselateBlock(stack.Block, out toolMeshes[i]);
                }

               if (stack.Class == EnumItemClass.Item && stack.Item.Shape?.VoxelizeTexture == true)
                {
                    toolMeshes[i].Scale(origin, 0.33f, 0.33f, 0.33f);
                    toolMeshes[i].Translate(((i % 2) == 0) ? 0.23f : -0.3f, (i > 1) ? 0.2f : -0.3f, 0.429f * ((facing.Axis == EnumAxis.X) ? -1 : 1));
                    toolMeshes[i].Rotate(origin, 0, facing.HorizontalAngleIndex * 90 * GameMath.DEG2RAD, 0);
                    toolMeshes[i].Rotate(origin, 180 * GameMath.DEG2RAD, 0, 0);

                } else {

                    toolMeshes[i].Scale(origin, 0.6f, 0.6f, 0.6f);
                    float x = ((i > 1) ? -0.2f : 0.3f);
                    float z = ((i % 2 == 0) ? 0.23f : -0.2f) * (facing.Axis == EnumAxis.X ? 1 : -1);

                    toolMeshes[i].Translate(x, 0.429f, z);
                    toolMeshes[i].Rotate(origin, 0, facing.HorizontalAngleIndex * 90 * GameMath.DEG2RAD, GameMath.PIHALF);
                    toolMeshes[i].Rotate(origin, 0, GameMath.PIHALF, 0);
                }

                

            }
        }

        
        internal bool OnPlayerInteract(IPlayer byPlayer, Vec3d hit)
        {
            BlockFacing facing = getFacing();

            int slot = 0 + (hit.Y < 0.5 ? 2 : 0);

            if (facing == BlockFacing.NORTH && hit.X > 0.5) slot++;
            if (facing == BlockFacing.SOUTH && hit.X < 0.5) slot++;
            if (facing == BlockFacing.WEST && hit.Z > 0.5) slot++;
            if (facing == BlockFacing.EAST && hit.Z < 0.5) slot++;

            IItemStack stack = inventory.GetSlot(slot).Itemstack;

            if (stack != null)
            {
                return TakeFromSlot(byPlayer, slot);
            } else
            {
                return PutInSlot(byPlayer, slot);
            }
        }

        bool PutInSlot(IPlayer player, int slot)
        {
            IItemStack stack = player.InventoryManager.ActiveHotbarSlot.Itemstack;
            if (stack == null || stack.Collectible.Tool == null) return false;

            player.InventoryManager.ActiveHotbarSlot.TryPutInto(api.World, inventory.GetSlot(slot));

            didInteract(player);
            return true;
        }


        bool TakeFromSlot(IPlayer player, int slot)
        {
            ItemStack stack = inventory.GetSlot(slot).TakeOutWhole();
            
            if (!player.InventoryManager.TryGiveItemstack(stack))
            {
                api.World.SpawnItemEntity(stack, pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

            didInteract(player);
            return true;
        }
        

        void didInteract(IPlayer player)
        {
            api.World.PlaySoundAt(new AssetLocation("sounds/player/buildhigh"), pos.X, pos.Y, pos.Z, player, false);
            if (api is ICoreClientAPI) loadToolMeshes();
            MarkDirty(true);
        }



        public override void OnBlockRemoved()
        {
            
        }

        public override void OnBlockBroken()
        {
            for (int i = 0; i < 4; i++)
            {
                ItemStack stack = inventory.GetSlot(i).Itemstack;
                if (stack == null) continue;
                api.World.SpawnItemEntity(stack, pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }

        BlockFacing getFacing()
        {
            Block block = api.World.BlockAccessor.GetBlock(pos);
            BlockFacing facing = BlockFacing.FromCode(block.LastCodePart());
            return facing == null ? BlockFacing.NORTH : facing;
        }

        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            ICoreClientAPI clientApi = (ICoreClientAPI)api;
            Block block = api.World.BlockAccessor.GetBlock(pos);
            MeshData mesh = clientApi.TesselatorManager.GetDefaultBlockMesh(block);
            if (mesh == null) return true;

            mesher.AddMeshData(mesh);

            for (int i = 0; i < 4; i++)
            {
                if (toolMeshes[i] == null) continue;
                mesher.AddMeshData(toolMeshes[i]);
            }


            return true;
        }






        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);
            inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            if (api != null)
            {
                inventory.Api = api;
                inventory.ResolveBlocksOrItems();
            }

            if (api is ICoreClientAPI)
            {
                loadToolMeshes();
                api.World.BlockAccessor.MarkBlockDirty(pos);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
        }




        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            int q = inventory.QuantitySlots;
            for (int i = 0; i < q; i++)
            {
                ItemSlot slot = inventory.GetSlot(i);
                if (slot.Itemstack == null) continue;

                if (slot.Itemstack.Class == EnumItemClass.Item)
                {
                    itemIdMapping[slot.Itemstack.Item.Id] = slot.Itemstack.Item.Code;
                }
                else
                {
                    blockIdMapping[slot.Itemstack.Block.BlockId] = slot.Itemstack.Block.Code;
                }
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping)
        {
            int q = inventory.QuantitySlots;
            for (int i = 0; i < q; i++)
            {
                ItemSlot slot = inventory.GetSlot(i);
                if (slot.Itemstack == null) continue;
                slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve);
            }
        }

    }
}
