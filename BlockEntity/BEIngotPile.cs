using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntityIngotPile : BlockEntityItemPile, ITexPositionSource
    {
        Dictionary<AssetLocation, MeshData[]> meshesByType
        {
            get
            {
                object value = null;
                api.ObjectCache.TryGetValue("ingotpile-meshes", out value);
                return (Dictionary<AssetLocation, MeshData[]>)value;
            }
            set { api.ObjectCache["ingotpile-meshes"] = value; }
        }
        

        Block tmpBlock;
        AssetLocation tmpMetal;
        ITexPositionSource tmpTextureSource;

        internal AssetLocation soundLocation = new AssetLocation("sounds/block/ingot");

        public override AssetLocation SoundLocation { get { return soundLocation; } }
        public override string BlockCode { get { return "ingotpile"; } }
        public override int MaxStackSize { get { return 64; } }

        internal void EnsureMeshExists()
        {
            if (meshesByType == null) meshesByType = new Dictionary<AssetLocation, MeshData[]>();
            if (MetalType == null || meshesByType.ContainsKey(new AssetLocation(MetalType))) return;
            if (api.Side != EnumAppSide.Client) return;

            tmpBlock = api.World.BlockAccessor.GetBlock(pos);
            tmpTextureSource = ((ICoreClientAPI)api).Tesselator.GetTexSource(tmpBlock);
            Shape shape = api.Assets.TryGet("shapes/block/metal/ingotpile.json").ToObject<Shape>();
            MetalProperty metals = api.Assets.TryGet("worldproperties/block/metal.json").ToObject<MetalProperty>();

            for (int i = 0; i < metals.Variants.Length; i++)
            {
                if (!metals.Variants[i].Code.Path.Equals(MetalType)) continue;

                ITesselatorAPI mesher = ((ICoreClientAPI)api).Tesselator;
                MeshData[] meshes = new MeshData[65];

                tmpMetal = metals.Variants[i].Code;

                for (int j = 0; j <= 64; j++)
                {
                    mesher.TesselateShape("ingotPile", shape, out meshes[j], this, null, 0, 0, j);
                }

                meshesByType[tmpMetal] = meshes;
            }

            tmpTextureSource = null;
            tmpMetal = null;
            tmpBlock = null;
        }

        public override bool TryPutItem(IPlayer player)
        {
            bool result = base.TryPutItem(player);
            EnsureMeshExists();
            return result;
        }

        

        public TextureAtlasPosition this[string textureCode]
        {
            get { return tmpTextureSource[tmpMetal.Path]; }
        }


        public BlockEntityIngotPile() : base()
        {
            inventory = new InventoryGeneric(1, BlockCode, null, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            inventory.ResolveBlocksOrItems();
            EnsureMeshExists();
        }

        public string MetalType
        {
            get { return inventory?.GetSlot(0)?.Itemstack?.Collectible?.LastCodePart(); }
        }

        public override bool OnTesselation(ITerrainMeshPool meshdata, ITesselatorAPI tesselator)
        {
            lock (inventoryLock)
            {
                if (inventory.GetSlot(0).Itemstack == null) return true;

                MeshData[] mesh = null;
                if (MetalType != null && meshesByType.TryGetValue(new AssetLocation(MetalType), out mesh))
                {
                    meshdata.AddMeshData(mesh[inventory.GetSlot(0).StackSize]);
                }
            }

            return true;
        }
    }
}
