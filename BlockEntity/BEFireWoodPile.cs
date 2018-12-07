using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class BlockEntityFirewoodPile : BlockEntityItemPile
    {
        internal AssetLocation soundLocation = new AssetLocation("sounds/block/planks");

        public override AssetLocation SoundLocation { get { return soundLocation; } }

        public override string BlockCode
        {
            get { return "firewoodpile"; }
        }

        public override int TakeQuantity
        {
            get { return 2; }
        }

        public override int MaxStackSize { get { return 32; } }
        

        MeshData[] meshes
        {
            get {
                object value = null;
                api.ObjectCache.TryGetValue("firewoodpile-meshes", out value);
                return (MeshData[])value;
            }
            set { api.ObjectCache["firewoodpile-meshes"] = value; }
        }



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreClientAPI && meshes == null)
            {
                GenMeshes();
            }
        }

        internal void GenMeshes()
        {
            MeshData[] meshes = new MeshData[17];

            Block block = api.World.BlockAccessor.GetBlock(pos);
            
            Shape shape = api.Assets.TryGet("shapes/block/wood/firewoodpile.json").ToObject<Shape>();
        
            ITesselatorAPI mesher = ((ICoreClientAPI)api).Tesselator;
            
            for (int j = 0; j <= 16; j++)
            {
                mesher.TesselateShape(block, shape, out meshes[j], null, j);
            }

            this.meshes = meshes;
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
