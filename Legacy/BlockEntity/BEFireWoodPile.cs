using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityFirewoodPile : BlockEntityItemPile, IBlockEntityItemPile
    {
        internal AssetLocation soundLocation = new AssetLocation("sounds/block/planks");

        public override AssetLocation SoundLocation { get { return soundLocation; } }

        public override string BlockCode
        {
            get { return "firewoodpile"; }
        }

        public override int DefaultTakeQuantity
        {
            get { return 2; }
        }

        public override int BulkTakeQuantity
        {
            get { return 8; }
        }

        public override int MaxStackSize { get { return 32; } }
        

        MeshData[] meshes
        {
            get
            {
                return ObjectCacheUtil.GetOrCreate(Api, "firewoodpile-meshes", () =>
                {
                    MeshData[] meshes = new MeshData[17];

                    Block block = Api.World.BlockAccessor.GetBlock(Pos);

                    Shape shape = Shape.TryGet(Api, "shapes/block/wood/firewoodpile.json");

                    ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;

                    for (int j = 0; j <= 16; j++)
                    {
                        mesher.TesselateShape(block, shape, out meshes[j], null, j);
                    }

                    return meshes;
                });
            }
        }



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            RandomizeSoundPitch = true;
        }


        public override bool OnTesselation(ITerrainMeshPool meshdata, ITesselatorAPI tesselator)
        {
            lock(inventoryLock)
            {
                int index = Math.Min(16, (int)Math.Ceiling(inventory[0].StackSize / 2.0));

                meshdata.AddMeshData(meshes[index]);
            }

            return true;
        }        
    }
}
