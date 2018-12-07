using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    class BlockEntityPlatePile : BlockEntityItemPile, ITexPositionSource
    {
        Dictionary<AssetLocation, MeshData[]> meshesByType
        {
            get
            {
                object value = null;
                api.ObjectCache.TryGetValue("platepile-meshes", out value);
                return (Dictionary<AssetLocation, MeshData[]>)value;
            }
            set { api.ObjectCache["platepile-meshes"] = value; }
        }


        Block tmpBlock;
        AssetLocation tmpMetal;
        ITexPositionSource tmpTextureSource;

        internal AssetLocation soundLocation = new AssetLocation("sounds/block/ingot");

        public override AssetLocation SoundLocation { get { return soundLocation; } }
        public override string BlockCode { get { return "platepile"; } }
        public override int MaxStackSize { get { return 16; } }

        internal void GenMeshes()
        {
            meshesByType = new Dictionary<AssetLocation, MeshData[]>();

            tmpBlock = api.World.BlockAccessor.GetBlock(pos);
            tmpTextureSource = ((ICoreClientAPI)api).Tesselator.GetTexSource(tmpBlock);
            Shape shape = api.Assets.TryGet("shapes/block/metal/platepile.json").ToObject<Shape>();
            MetalProperty metals = api.Assets.TryGet("worldproperties/block/metal.json").ToObject<MetalProperty>();

            for (int i = 0; i < metals.Variants.Length; i++)
            {
                ITesselatorAPI mesher = ((ICoreClientAPI)api).Tesselator;
                MeshData[] meshes = new MeshData[17];

                tmpMetal = metals.Variants[i].Code;

                for (int j = 0; j <= 16; j++)
                {
                    mesher.TesselateShape("platePile", shape, out meshes[j], this, null, 0, 0, j);
                }

                meshesByType[tmpMetal] = meshes;
            }

            tmpTextureSource = null;
            tmpMetal = null;
            tmpBlock = null;
        }


        public TextureAtlasPosition this[string textureCode]
        {
            get { return tmpTextureSource[tmpMetal.Path]; }
        }


        public BlockEntityPlatePile() : base()
        {
            inventory = new InventoryGeneric(1, BlockCode, null, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreClientAPI && meshesByType == null)
            {
                GenMeshes();
            }

            inventory.ResolveBlocksOrItems();
        }

        public override bool OnTesselation(ITerrainMeshPool meshdata, ITesselatorAPI tesselator)
        {
            lock (inventoryLock)
            {
                if (inventory[0].Itemstack == null) return true;
                string metal = inventory[0].Itemstack.Collectible.LastCodePart();
                int index = Math.Min(16, inventory[0].StackSize);

                meshdata.AddMeshData(meshesByType[new AssetLocation(metal)][index]);
            }

            return true;
        }
    }
}

