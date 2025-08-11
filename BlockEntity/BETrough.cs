using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ContentConfig
    {
        public string Code;
        public JsonItemStack Content;
        public int QuantityPerFillLevel;
        public int MaxFillLevels;
        public string[] ShapesPerFillLevel;
        public string TextureCode;
    }

    public class DoubleTroughPoiDummy : IAnimalFoodSource
    {
        BlockEntityTrough be;

        public DoubleTroughPoiDummy(BlockEntityTrough be)
        {
            this.be = be;
        }

        public Vec3d Position { get; set; }

        public string Type => be.Type;

        public float ConsumeOnePortion(Entity entity)
        {
            return be.ConsumeOnePortion(entity);
        }

        public bool IsSuitableFor(Entity entity, CreatureDiet diet)
        {
            return be.IsSuitableFor(entity, diet);
        }
    }

    public class BlockEntityTrough : BlockEntityContainer, ITexPositionSource, IAnimalFoodSource
    {
        internal InventoryGeneric inventory;
        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "trough";

        ITexPositionSource blockTexPosSource;
        public Size2i AtlasSize => (Api as ICoreClientAPI).BlockTextureAtlas.Size;

        public Vec3d Position => Pos.ToVec3d().Add(0.5, 0.5, 0.5);
        public string Type => "food";
        
        MeshData currentMesh;

        string contentCode = "";

        DoubleTroughPoiDummy dummypoi;

        public ContentConfig[] contentConfigs => Api.ObjectCache["troughContentConfigs-" + Block.Code] as ContentConfig[];


        public bool IsFull
        {
            get
            {
                ItemStack[] stacks = GetNonEmptyContentStacks();
                ContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
                if (config == null) return false;

                return stacks.Length != 0 && stacks[0].StackSize >= config.QuantityPerFillLevel * config.MaxFillLevels;
            }
        }


        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode != "contents") return blockTexPosSource[textureCode];
                var config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
                var configTextureCode = config?.TextureCode;
                if(configTextureCode?.Equals("*") == true)
                {
                    configTextureCode = "contents-" + Inventory.FirstNonEmptySlot.Itemstack.Collectible.Code.ToShortString();
                }
                return configTextureCode != null ? blockTexPosSource[configTextureCode] : blockTexPosSource[textureCode];
            }
        }


        public BlockEntityTrough()
        {
            inventory = new InventoryGeneric(4, null, null, (id, inv) => new ItemSlotTrough(this, inv));
            inventory.OnGetAutoPushIntoSlot = (face, slot) =>
            {
                if (IsFull) return null;
                WeightedSlot wslot = inventory.GetBestSuitedSlot(slot);
                return wslot.slot;
            };
        }


        public bool IsSuitableFor(Entity entity, CreatureDiet diet)
        {
            if (inventory.Empty || diet == null) return false;
            var config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
            var contentResolvedItemstack = config?.Content?.ResolvedItemstack ?? ResolveWildcardContent(config, entity.World);
            
            if (contentResolvedItemstack == null) return false;

            bool hasSuitableFood = diet.Matches(contentResolvedItemstack) && inventory[0].StackSize >= config.QuantityPerFillLevel;
            if (hasSuitableFood && Block is BlockTroughBase trough)
            {
                return !(trough.UnsuitableForEntity(entity.Code.Path));   // e.g. prohibit chickens from eating from large trough
            }
            return false;
        }

        private ItemStack ResolveWildcardContent(ContentConfig config, IWorldAccessor worldAccessor)
        {
            if (config?.Content?.Code == null) return null;
            var searchObjects = new List<CollectibleObject>();
            
            switch (config.Content.Type)
            {
                case EnumItemClass.Block:
                    searchObjects.AddRange(worldAccessor.SearchBlocks(config.Content.Code));
                    break;
                case EnumItemClass.Item:
                    searchObjects.AddRange(worldAccessor.SearchItems(config.Content.Code));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(config.Content.Type));
            }
            
            foreach (var item in searchObjects)
            {
                if (item.Code.Equals(Inventory.FirstNonEmptySlot?.Itemstack?.Item?.Code))
                {
                    return new ItemStack(item);
                }
            }

            return null;
        }


        public float ConsumeOnePortion(Entity entity)
        {
            ContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
            if (config == null || inventory.Empty) return 0f;

            inventory[0].TakeOut(config.QuantityPerFillLevel);

            if (inventory[0].Empty)
            {
                contentCode = "";
            }
            inventory[0].MarkDirty();

            MarkDirty(true);
            return 1f;
        }



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (Api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                if (currentMesh == null)
                {
                    currentMesh = GenMesh();
                }
            } else
            {
                Api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);

                BlockTroughDoubleBlock doubleblock = Block as BlockTroughDoubleBlock;

                if (doubleblock != null)
                {
                    dummypoi = new DoubleTroughPoiDummy(this) { Position = doubleblock.OtherPartPos(Pos).ToVec3d().Add(0.5, 0.5, 0.5) };
                    Api.ModLoader.GetModSystem<POIRegistry>().AddPOI(dummypoi);
                }


            }

            inventory.SlotModified += Inventory_SlotModified;
        }

        private void Inventory_SlotModified(int id)
        {
            ContentConfig config = ItemSlotTrough.getContentConfig(Api.World, contentConfigs, inventory[id]);
            this.contentCode = config?.Code;

            if (Api.Side == EnumAppSide.Client) currentMesh = GenMesh();
            MarkDirty(true);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }


        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
                if (dummypoi != null) Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(dummypoi);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (Api?.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
                if (dummypoi != null) Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(dummypoi);
            }
        }



        internal MeshData GenMesh()
        {
            if (Block == null) return null;
            ItemStack firstStack = inventory[0].Itemstack;
            if (firstStack == null) return null;

            string shapeLoc = "";
            ICoreClientAPI capi = Api as ICoreClientAPI;

            if (contentCode == "" || contentConfigs == null)
            {
                if (firstStack.Collectible.Code.Path == "rot")
                {
                    shapeLoc = "block/wood/trough/"+(Block.Variant["part"] == "small" ? "small" : "large")+"/rotfill" + GameMath.Clamp(firstStack.StackSize / 4, 1, 4);
                }
                else
                {
                    return null;
                }
            } else
            {
                ContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
                if (config == null) return null;

                int fillLevel = Math.Max(0, firstStack.StackSize / config.QuantityPerFillLevel - 1);
                shapeLoc = config.ShapesPerFillLevel[Math.Min(config.ShapesPerFillLevel.Length - 1, fillLevel)];
            }



            Vec3f rotation = new Vec3f(Block.Shape.rotateX, Block.Shape.rotateY, Block.Shape.rotateZ);

            blockTexPosSource = capi.Tesselator.GetTextureSource(Block);
            Shape shape = Shape.TryGet(Api, "shapes/" + shapeLoc + ".json");
            capi.Tesselator.TesselateShape("betroughcontentsleft", shape, out MeshData meshbase, this, rotation);

            BlockTroughDoubleBlock doubleblock = Block as BlockTroughDoubleBlock;

            if (doubleblock != null)
            {
                capi.Tesselator.TesselateShape("betroughcontentsright", shape, out MeshData meshadd, this, rotation.Add(0, 180, 0));
                BlockFacing facing = doubleblock.OtherPartFacing();
                meshadd.Translate(facing.Normalf);
                meshbase.AddMeshData(meshadd);
            }

            return meshbase;
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(currentMesh);
            return false;
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot handSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (handSlot.Empty) return false;

            ItemStack[] stacks = GetNonEmptyContentStacks();


            ContentConfig contentConf = ItemSlotTrough.getContentConfig(Api.World, contentConfigs, handSlot);
            if (contentConf == null) return false;

            // Add new
            if (stacks.Length == 0)
            {
                if (handSlot.StackSize >= contentConf.QuantityPerFillLevel)
                {
                    inventory[0].Itemstack = handSlot.TakeOut(contentConf.QuantityPerFillLevel);
                    inventory[0].MarkDirty();
                    return true;
                }

                return false;
            }

            // Or merge
            bool canAdd =
                handSlot.Itemstack.Equals(Api.World, stacks[0], GlobalConstants.IgnoredStackAttributes) &&
                handSlot.StackSize >= contentConf.QuantityPerFillLevel &&
                stacks[0].StackSize < contentConf.QuantityPerFillLevel * contentConf.MaxFillLevels
            ;

            if (canAdd)
            {
                handSlot.TakeOut(contentConf.QuantityPerFillLevel);
                inventory[0].Itemstack.StackSize += contentConf.QuantityPerFillLevel;
                inventory[0].MarkDirty();
                return true;
            }
            
            return false;
        }




        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetString("contentCode", contentCode);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            contentCode = tree.GetString("contentCode");

            if (Api?.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            ItemStack firstStack = inventory[0].Itemstack;

            if (contentConfigs == null)
            {
                return;
            }

            ContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);

            if (config == null && firstStack != null)
            {
                dsc.AppendLine(firstStack.StackSize + "x " + firstStack.GetName());
            }


            if (config == null || firstStack == null) return;

            int fillLevel = firstStack.StackSize / config.QuantityPerFillLevel;

            dsc.AppendLine(Lang.Get("Portions: {0}", fillLevel));

            ItemStack contentsStack = config.Content.ResolvedItemstack ?? ResolveWildcardContent(config, forPlayer.Entity.World);

            if (contentsStack == null) return;

            dsc.AppendLine(Lang.Get(contentsStack.GetName()));

            HashSet<string> creatureNames = new HashSet<string>();
            foreach (var entityType in Api.World.EntityTypes)
            {
                var attr = entityType.Attributes;
                if (attr?["creatureDiet"].AsObject<CreatureDiet>()?.Matches(contentsStack) != true) continue;

                if ((Block as BlockTroughBase)?.UnsuitableForEntity(entityType.Code.Path) == true) continue;

                string code = attr?["creatureDietGroup"].AsString() ?? attr?["handbook"]["groupcode"].AsString() ?? entityType.Code.Domain + ":item-creature-" + entityType.Code.Path;
                creatureNames.Add(Lang.Get(code));
            }

            if (creatureNames.Count <= 0) dsc.AppendLine(Lang.Get("trough-unsuitable"));
            else dsc.AppendLine(Lang.Get("trough-suitable", string.Join(", ", creatureNames)));
        }
    }
}
