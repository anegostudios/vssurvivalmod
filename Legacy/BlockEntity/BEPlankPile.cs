using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    class BlockEntityPlankPile : BlockEntityItemPile, ITexPositionSource
    {
        Dictionary<AssetLocation, MeshData[]> meshesByType
        {
            get
            {
                return ObjectCacheUtil.GetOrCreate(Api, "plankpile-meshes", () => GenMeshes());
            }
        }


        Block tmpBlock;
        AssetLocation tmpWood;
        ITexPositionSource tmpTextureSource;

        internal AssetLocation soundLocation = new AssetLocation("sounds/block/planks");

        public override AssetLocation SoundLocation { get { return soundLocation; } }
        public override string BlockCode { get { return "plankpile"; } }
        public override int MaxStackSize { get { return 48; } }

        Dictionary<AssetLocation, MeshData[]> GenMeshes()
        {
            Dictionary<AssetLocation, MeshData[]> meshesByType = new Dictionary<AssetLocation, MeshData[]>();

            tmpBlock = Api.World.BlockAccessor.GetBlock(Pos);
            tmpTextureSource = ((ICoreClientAPI)Api).Tesselator.GetTextureSource(tmpBlock);
            Shape shape = API.Common.Shape.TryGet(Api, "shapes/block/wood/plankpile.json");
            MetalProperty woodtpyes = Api.Assets.TryGet("worldproperties/block/wood.json").ToObject<MetalProperty>();

            woodtpyes.Variants = woodtpyes.Variants.Append(new MetalPropertyVariant() { Code = new AssetLocation("aged") });

            for (int i = 0; i < woodtpyes.Variants.Length; i++)
            {
                ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;
                MeshData[] meshes = new MeshData[49];

                tmpWood = woodtpyes.Variants[i].Code;

                for (int j = 0; j <= 48; j++)
                {
                    mesher.TesselateShape("PlankPile", shape, out meshes[j], this, null, 0, 0, 0, j);
                }

                meshesByType[tmpWood] = meshes;
            }

            tmpTextureSource = null;
            tmpWood = null;
            tmpBlock = null;

            return meshesByType;
        }


        public TextureAtlasPosition this[string textureCode]
        {
            get { return tmpTextureSource[tmpWood.Path]; }
        }


        public BlockEntityPlankPile() : base()
        {
            inventory = new InventoryGeneric(1, BlockCode, null, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            inventory.ResolveBlocksOrItems();
        }

        public override bool OnTesselation(ITerrainMeshPool meshdata, ITesselatorAPI tesselator)
        {
            lock (inventoryLock)
            {
                if (inventory[0].Itemstack == null) return true;
                string woodtype = inventory[0].Itemstack.Collectible.LastCodePart();
                int index = Math.Min(48, inventory[0].StackSize);

                meshdata.AddMeshData(meshesByType[new AssetLocation(woodtype)][index]);
            }

            return true;
        }
    }
}

