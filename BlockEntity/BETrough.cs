using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ContentConfig
    {
        public string Code;
        public JsonItemStack Content;
        public int QuantityPerFillLevel;
        public int MaxFillLevels;
        public AssetLocation[] Foodfor;
        public string[] ShapesPerFillLevel;
        public string TextureCode;
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
        
        BlockFacing facing;
        MeshData currentMesh;

        ContentConfig[] contentConfigs;

        string contentCode = "";

        public ContentConfig[] ContentConfig => contentConfigs;

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
                ContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);

                return config.TextureCode != null ? blockTexPosSource[config.TextureCode] : blockTexPosSource[textureCode];
            }
        }


        public BlockEntityTrough()
        {
            inventory = new InventoryGeneric(4, null, null, (id, inv) => new ItemSlotTrough(this, inv));
        }


        public bool IsSuitableFor(Entity entity)
        {
            ContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
            if (config == null) return false;

            for (int i = 0; i < config.Foodfor.Length; i++)
            {
                if (WildcardUtil.Match(config.Foodfor[i], entity.Code))
                {
                    return inventory[0].StackSize >= config.QuantityPerFillLevel;
                }
            }

            return false; 
        }



        public float ConsumeOnePortion()
        {
            ContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
            if (config == null) return 0f;

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

            facing = BlockFacing.FromCode(Block.LastCodePart());

            if (contentConfigs == null)
            {
                contentConfigs = Block.Attributes?["contentConfig"]?.AsObject<ContentConfig[]>();
                if (contentConfigs == null) return;

                foreach (var val in contentConfigs)
                {
                    val.Content.Resolve(Api.World, "troughcontentconfig");
                }
            }

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
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (Api?.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }



        internal MeshData GenMesh()
        {
            if (Block == null || contentCode == "" || contentConfigs == null) return null;
            ContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
            if (config == null) return null;

            ICoreClientAPI capi = Api as ICoreClientAPI;

            ItemStack firstStack = inventory[0].Itemstack;
            if (firstStack == null) return null;

            int fillLevel = Math.Max(0, firstStack.StackSize / config.QuantityPerFillLevel - 1);
            string shapeLoc = config.ShapesPerFillLevel[Math.Min(config.ShapesPerFillLevel.Length - 1, fillLevel)];

            Vec3f rotation = new Vec3f(Block.Shape.rotateX, Block.Shape.rotateY, Block.Shape.rotateZ);
            MeshData meshbase;
            MeshData meshadd;

            blockTexPosSource = capi.Tesselator.GetTexSource(Block);
            Shape shape = Api.Assets.TryGet("shapes/" + shapeLoc + ".json").ToObject<Shape>();
            capi.Tesselator.TesselateShape("betroughcontentsleft", shape, out meshbase, this, rotation);

            BlockTroughDoubleBlock doubleblock = Block as BlockTroughDoubleBlock;

            if (doubleblock != null)
            {
                capi.Tesselator.TesselateShape("betroughcontentsright", shape, out meshadd, this, rotation.Add(0, 180, 0));
                BlockFacing facing = doubleblock.OtherPartPos();
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

            if (Api?.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }

            contentCode = tree.GetString("contentCode");
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (contentConfigs == null) return;

            ContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
            ItemStack firstStack = inventory[0].Itemstack;

            if (config == null || firstStack == null) return;

            int fillLevel = firstStack.StackSize / config.QuantityPerFillLevel;

            dsc.AppendLine(Lang.Get("Portions: {0}", fillLevel));
        }
    }
}
