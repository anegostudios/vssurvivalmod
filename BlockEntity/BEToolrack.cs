using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityToolrack : BlockEntity, ITexPositionSource
    {
        public InventoryGeneric inventory;

        MeshData[] toolMeshes = new MeshData[4];


        public Size2i AtlasSize
        {
            get { return ((ICoreClientAPI)Api).BlockTextureAtlas.Size; }
        }

        CollectibleObject tmpItem;
        public TextureAtlasPosition this[string textureCode]
        {
            get {
                ToolTextures tt;

                var toolTextureSubIds = BlockToolRack.ToolTextureSubIds(Api);
                if (toolTextureSubIds.TryGetValue((Item)tmpItem, out tt))
                {
                    int textureSubId = 0;
                    if (tt.TextureSubIdsByCode.TryGetValue(textureCode, out textureSubId))
                    {
                        return ((ICoreClientAPI)Api).BlockTextureAtlas.Positions[textureSubId];
                    }

                    return ((ICoreClientAPI)Api).BlockTextureAtlas.Positions[tt.TextureSubIdsByCode.First().Value];
                }

                Api.Logger.Debug("Could not get item texture! textureCode: {0} Item: {1}",textureCode, tmpItem.Code);
                return ((ICoreClientAPI)Api).BlockTextureAtlas.UnknownTexturePosition;
            }
        }

        public BlockEntityToolrack() : base()
        {
            inventory = new InventoryGeneric(4, "toolrack", null, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            inventory.LateInitialize("toolrack-" + Pos.ToString(), api);
            inventory.ResolveBlocksOrItems();
            inventory.OnAcquireTransitionSpeed += Inventory_OnAcquireTransitionSpeed;

            if (api is ICoreClientAPI)
            {
                loadToolMeshes();
                api.Event.RegisterEventBusListener(OnEventBusEvent);
            }
        }

        private void OnEventBusEvent(string eventname, ref EnumHandling handling, IAttribute data)
        {
            if (eventname != "genjsontransform" && eventname != "oncloseedittransforms" &&
                eventname != "onapplytransforms") return;

            loadToolMeshes();
            MarkDirty(true);
        }

        protected virtual float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
        {
            return baseMul;
        }

        void loadToolMeshes()
        {
            BlockFacing facing = getFacing().GetCCW();
            if (facing == null) return;

            Vec3f origin = new Vec3f(0.5f, 0.5f, 0.5f);

            ICoreClientAPI capi = (ICoreClientAPI)Api;

            for (int i = 0; i < 4; i++)
            {
                toolMeshes[i] = null;
                ItemStack stack = inventory[i].Itemstack;
                if (stack == null) continue;

                tmpItem = stack.Collectible;

                var meshSource = stack.Collectible as IContainedMeshSource;
                if (meshSource != null)
                {
                    toolMeshes[i] = meshSource.GenMesh(stack, capi.BlockTextureAtlas, Pos);
                }
                else
                {
                    if (stack.Class == EnumItemClass.Item)
                    {
                        capi.Tesselator.TesselateItem(stack.Item, out toolMeshes[i], this);
                    }
                    else
                    {
                        capi.Tesselator.TesselateBlock(stack.Block, out toolMeshes[i]);
                    }
                }

                if (tmpItem.Attributes?["toolrackTransform"].Exists == true)
                {
                    ModelTransform transform = tmpItem.Attributes["toolrackTransform"].AsObject<ModelTransform>();
                    transform.EnsureDefaultValues();
                    toolMeshes[i].ModelTransform(transform);
                }

                float yOff = i > 1 ? (-1.8f / 16f) : 0;

                if (stack.Class == EnumItemClass.Item && stack.Item.Shape?.VoxelizeTexture == true)
                {
                    toolMeshes[i].Scale(origin, 0.33f, 0.33f, 0.33f);
                    toolMeshes[i].Translate(
                        (((i % 2) == 0) ? 0.23f : -0.3f),
                        ((i > 1) ? 0.2f : -0.3f) + yOff,
                        0.433f * ((facing.Axis == EnumAxis.X) ? -1 : 1)
                    );
                    toolMeshes[i].Rotate(origin, 0, facing.HorizontalAngleIndex * 90 * GameMath.DEG2RAD, 0);
                    toolMeshes[i].Rotate(origin, 180 * GameMath.DEG2RAD, 0, 0);
                }
                else
                {

                    toolMeshes[i].Scale(origin, 0.6f, 0.6f, 0.6f);
                    float x = ((i > 1) ? -0.2f : 0.3f);
                    float z = ((i % 2 == 0) ? 0.23f : -0.2f) * (facing.Axis == EnumAxis.X ? 1f : -1f);

                    toolMeshes[i].Translate(x, 0.433f + yOff, z);
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

            IItemStack stack = inventory[slot].Itemstack;

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
            if (stack == null || (stack.Collectible.Tool == null && stack.Collectible.Attributes?["rackable"].AsBool() != true)) return false;

            var stackName = player.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible.Code;
            player.InventoryManager.ActiveHotbarSlot.TryPutInto(Api.World, inventory[slot]);
            Api.World.Logger.Audit("{0} Put 1x{1} into Tool rack at {2}.",
                player.PlayerName,
                stackName,
                Pos
            );
            didInteract(player);
            return true;
        }

        bool TakeFromSlot(IPlayer player, int slot)
        {
            ItemStack stack = inventory[slot].TakeOutWhole();

            if (!player.InventoryManager.TryGiveItemstack(stack))
            {
                Api.World.SpawnItemEntity(stack, Pos);
            }
            var stackName = stack?.Collectible.Code;
            Api.World.Logger.Audit("{0} Took 1x{1} from Tool rack at {2}.",
                player.PlayerName,
                stackName,
                Pos
            );

            didInteract(player);
            return true;
        }

        void didInteract(IPlayer player)
        {
            Api.World.PlaySoundAt(new AssetLocation("sounds/player/buildhigh"), Pos, 0, player, false);
            if (Api is ICoreClientAPI) loadToolMeshes();
            MarkDirty(true);
        }

        public override void OnBlockRemoved()
        {

        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            for (int i = 0; i < 4; i++)
            {
                ItemStack stack = inventory[i].Itemstack;
                if (stack == null) continue;
                Api.World.SpawnItemEntity(stack, Pos);
            }
        }

        BlockFacing getFacing()
        {
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            BlockFacing facing = BlockFacing.FromCode(block.LastCodePart());
            return facing == null ? BlockFacing.NORTH : facing;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            ICoreClientAPI clientApi = (ICoreClientAPI)Api;
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
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

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            if (Api != null)
            {
                inventory.Api = Api;
                inventory.ResolveBlocksOrItems();
            }

            if (Api is ICoreClientAPI)
            {
                loadToolMeshes();
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
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
            foreach (var slot in inventory)
            {
                if (slot.Itemstack == null) continue;

                if (slot.Itemstack.Class == EnumItemClass.Item)
                {
                    itemIdMapping[slot.Itemstack.Item.Id] = slot.Itemstack.Item.Code;
                }
                else
                {
                    blockIdMapping[slot.Itemstack.Block.BlockId] = slot.Itemstack.Block.Code;
                }
                slot.Itemstack.Collectible.OnStoreCollectibleMappings(Api.World, slot ,blockIdMapping, itemIdMapping);
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            foreach (var slot in inventory)
            {
                if (slot.Itemstack == null) continue;
                if (!slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve))
                {
                    slot.Itemstack = null;
                } else
                {
                    slot.Itemstack.Collectible.OnLoadCollectibleMappings(worldForResolve, slot, oldBlockIdMapping, oldItemIdMapping, resolveImports);
                }
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            int i = 0;

            // The item meshes are drawn first on the right, then on the left: BlockInfo text description should match item positions i.e. 1, 0, 3, 2
            ItemStack slotsReverserLeftRight = null;
            foreach (var slot in inventory)
            {
                if (i % 2 == 0)
                {
                    slotsReverserLeftRight = slot.Itemstack;
                }
                else
                {
                    AddSlotItemInfo(sb, i - 1, slot.Itemstack);
                    AddSlotItemInfo(sb, i, slotsReverserLeftRight);
                }
                i++;
            }

            sb.AppendLineOnce();
            sb.ToString();
        }

        private void AddSlotItemInfo(StringBuilder sb, int i, ItemStack itemstack)
        {
            if (i == 2 && sb.Length > 0)
            {
                sb.Append("\n");
            }
            if (itemstack == null) return;

            if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
            {
                sb.Append(", ");
            }

            sb.Append(itemstack.GetName());
        }
    }
}
