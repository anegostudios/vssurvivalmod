using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityPeatPile : BlockEntityItemPile, ITexPositionSource
    {
        MeshData[] meshes
        {
            get
            {
                return ObjectCacheUtil.GetOrCreate(Api, "peatpile-meshes", () => GenMeshes());
            }
        }
        

        Block tmpBlock;
        ITexPositionSource tmpTextureSource;
        internal AssetLocation soundLocation = new AssetLocation("sounds/block/dirt");
        public override AssetLocation SoundLocation { get { return soundLocation; } }
        public override string BlockCode { get { return "peatpile"; } }
        public override int MaxStackSize { get { return 32; } }

        MeshData[] GenMeshes()
        {
            tmpBlock = Api.World.BlockAccessor.GetBlock(Pos);
            tmpTextureSource = ((ICoreClientAPI)Api).Tesselator.GetTextureSource(tmpBlock);
            Shape shape = API.Common.Shape.TryGet(Api, "shapes/block/peatpile.json");

            ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;
            MeshData[] meshes = new MeshData[33];

            for (int j = 0; j <= 32; j++)
            {
                mesher.TesselateShape("peatPile", shape, out meshes[j], this, null, 0, 0, 0, j);
            }

            tmpTextureSource = null;
            tmpBlock = null;

            return meshes;
        }

        public override bool TryPutItem(IPlayer player)
        {
            bool result = base.TryPutItem(player);
            return result;
        }

        

        public TextureAtlasPosition this[string textureCode]
        {
            get { return tmpTextureSource[textureCode]; }
        }


        public BlockEntityPeatPile() : base()
        {
            inventory = new InventoryGeneric(1, BlockCode, null, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            inventory.ResolveBlocksOrItems();
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
        }

        public override bool OnTesselation(ITerrainMeshPool meshdata, ITesselatorAPI tesselator)
        {
            lock (inventoryLock)
            {
                if (inventory[0].Itemstack == null) return true;

                meshdata.AddMeshData(meshes[inventory[0].StackSize]);
            }

            return true;
        }
    }
}
