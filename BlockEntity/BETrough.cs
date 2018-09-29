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

    public class BlockEntityTrough : BlockEntityContainer, IBlockShapeSupplier, ITexPositionSource, IAnimalFoodSource
    {
        internal InventoryGeneric inventory;
        public override InventoryBase Inventory => inventory;
        public override string InventoryClassName => "trough";

        ITexPositionSource blockTexPosSource;
        public int AtlasSize => (api as ICoreClientAPI).BlockTextureAtlas.Size;

        public Vec3d Position => pos.ToVec3d().Add(0.5, 0.5, 0.5);
        public string Type => "food";
        
        BlockFacing facing;
        Block ownBlock;
        MeshData currentMesh;

        ContentConfig[] contentConfigs;

        string contentCode = "";


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
            inventory = new InventoryGeneric(4, null, null);
        }


        public bool IsSuitableFor(Entity entity)
        {
            ContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
            if (config == null) return false;

            for (int i = 0; i < config.Foodfor.Length; i++)
            {
                if (RegistryObject.WildCardMatch(config.Foodfor[i], entity.Code)) return true;
            }

            return false; 
        }



        public float ConsumeOnePortion()
        {
            ContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
            if (config == null) return 0f;

            inventory.GetSlot(0).TakeOut(config.QuantityPerFillLevel);

            if (inventory.GetSlot(0).Empty)
            {
                contentCode = "";
            }

            
            MarkDirty(true);
            return 1f;
        }



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBlock = api.World.BlockAccessor.GetBlock(pos) as Block;            

            facing = BlockFacing.FromCode(ownBlock.LastCodePart());

            if (contentConfigs == null)
            {
                contentConfigs = ownBlock.Attributes["contentConfig"].AsObject<ContentConfig[]>();
            }

            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                if (currentMesh == null)
                {
                    currentMesh = GenMesh();
                }
            } else
            {
                api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);

            }

        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }


        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (api.Side == EnumAppSide.Server)
            {
                api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (api.Side == EnumAppSide.Server)
            {
                api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }



        internal MeshData GenMesh()
        {
            if (ownBlock == null || contentCode == "") return null;
            ContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
            if (config == null) return null;

            ICoreClientAPI capi = api as ICoreClientAPI;

            ItemStack firstStack = inventory.GetSlot(0).Itemstack;
            if (firstStack == null) return null;

            int fillLevel = Math.Max(0, firstStack.StackSize / config.QuantityPerFillLevel - 1);
            string shapeLoc = config.ShapesPerFillLevel[Math.Min(config.ShapesPerFillLevel.Length - 1, fillLevel)];

            Vec3f rotation = new Vec3f(ownBlock.Shape.rotateX, ownBlock.Shape.rotateY, ownBlock.Shape.rotateZ);
            MeshData meshbase;
            MeshData meshadd;

            blockTexPosSource = capi.Tesselator.GetTexSource(ownBlock);
            capi.Tesselator.TesselateShape("betrough", api.Assets.TryGet("shapes/" + shapeLoc + ".json").ToObject<Shape>(), out meshbase, this, rotation);

            BlockTroughDoubleBlock doubleblock = ownBlock as BlockTroughDoubleBlock;

            if (doubleblock != null)
            {
                capi.Tesselator.TesselateShape("betroughcontents", api.Assets.TryGet("shapes/" + shapeLoc + ".json").ToObject<Shape>(), out meshadd, this, rotation.Add(0, 180, 0), 0, 0, null, new string[] { "Origin point/contents/*" });
                BlockFacing facing = doubleblock.OtherPartPos();
                meshadd.Translate(facing.Normalf);
                meshbase.AddMeshData(meshadd);
            }

            return meshbase;
        }


        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(currentMesh);
            return currentMesh != null;
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot handSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (handSlot.Empty) return false;

            ItemStack[] stacks = GetContentStacks();
            bool canAdd;

            // Add new
            if (stacks.Length == 0)
            {
                for (int i = 0; i < contentConfigs.Length; i++)
                {
                    contentConfigs[i].Content.Resolve(api.World, "troughcontentconfig");

                    canAdd =
                        handSlot.Itemstack.Equals(api.World, contentConfigs[i].Content.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes) &&
                        handSlot.StackSize >= contentConfigs[i].QuantityPerFillLevel
                    ;

                    if (canAdd)
                    {
                        contentCode = contentConfigs[i].Code;
                        inventory.GetSlot(0).Itemstack = handSlot.TakeOut(contentConfigs[i].QuantityPerFillLevel);

                        if (api.Side == EnumAppSide.Client) currentMesh = GenMesh();
                        MarkDirty(true);
                        return true;
                    }
                }
            }

            // Or merge
            ContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
            if (config == null) return false;


            canAdd =
                handSlot.Itemstack.Equals(api.World, stacks[0], GlobalConstants.IgnoredStackAttributes) &&
                handSlot.StackSize >= config.QuantityPerFillLevel &&
                stacks[0].StackSize < config.QuantityPerFillLevel * config.MaxFillLevels
            ;

            if (canAdd)
            {
                handSlot.TakeOut(config.QuantityPerFillLevel);
                inventory.GetSlot(0).Itemstack.StackSize += config.QuantityPerFillLevel;

                if (api.Side == EnumAppSide.Client) currentMesh = GenMesh();
                MarkDirty(true);
                return true;
            }
            
            return false;
        }




        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetString("contentCode", contentCode);
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            if (api?.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }

            contentCode = tree.GetString("contentCode");
        }

        public override string GetBlockInfo(IPlayer forPlayer)
        {
            ContentConfig config = contentConfigs.FirstOrDefault(c => c.Code == contentCode);
            ItemStack firstStack = inventory.GetSlot(0).Itemstack;

            if (config == null || firstStack == null) return null;

            int fillLevel = firstStack.StackSize / config.QuantityPerFillLevel;

            return Lang.Get("Portions: {0}", fillLevel);
        }
    }
}
