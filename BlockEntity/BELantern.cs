using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BELantern : BlockEntity, IBlockShapeSupplier
    {
        public string material = "copper";
        public string lining = "plain";
        MeshData currentMesh;

        byte[] origlightHsv = new byte[] { 7, 4, 18 };
        byte[] lightHsv = new byte[] { 7, 4, 18 };

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            Block block = api.World.BlockAccessor.GetBlock(pos);
            origlightHsv = block.LightHsv;
        }

        public void DidPlace(string material, string lining)
        {
            this.lining = lining;
            this.material = material;
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            material = tree.GetString("material", "copper");
            lining = tree.GetString("lining", "plain");

            if (api != null && api.Side == EnumAppSide.Client)
            {
                currentMesh = null;
                MarkDirty(true);
            }
        }

        internal byte[] GetLightHsv()
        {
            lightHsv[2] = lining != "plain" ? (byte)(origlightHsv[2] + 3) : origlightHsv[2];
            return lightHsv;
        }

        private MeshData getMesh(ITesselatorAPI tesselator)
        {
            Dictionary<string, MeshData> lanternMeshes = null;

            object obj;
            if (api.ObjectCache.TryGetValue("blockLanternBlockMeshes", out obj))
            {
                lanternMeshes = obj as Dictionary<string, MeshData>;
            }
            else
            {
                api.ObjectCache["blockLanternBlockMeshes"] = lanternMeshes = new Dictionary<string, MeshData>();
            }

            MeshData mesh = null;
            BlockLantern block = api.World.BlockAccessor.GetBlock(pos) as BlockLantern;
            if (block == null) return null;

            string orient = block.LastCodePart();

            if (lanternMeshes.TryGetValue(material + "-" + lining + "-" + orient, out mesh))
            {
                return mesh;
            }

            return lanternMeshes[material + "-" + lining + "-" + orient] = block.GenMesh(api as ICoreClientAPI, material, lining, null, tesselator);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetString("material", material);
            tree.SetString("lining", lining);
        }


        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (currentMesh == null)
            {
                currentMesh = getMesh(tesselator);
                if (currentMesh == null) return false;
            }

            mesher.AddMeshData(currentMesh);
            return true;
        }

        public override string GetBlockInfo(IPlayer forPlayer)
        {
            return Lang.Get("{0} with {1} lining", material.UcFirst(), lining.UcFirst());
        }
    }
}
