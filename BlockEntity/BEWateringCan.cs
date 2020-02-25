using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockEntityWateringCan : BlockEntity
    {
        public float SecondsWateringLeft;
        BlockWateringCan ownBlock;

        ICoreClientAPI capi;

        public virtual float MeshAngle { get; set; }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBlock = api.World.BlockAccessor.GetBlock(this.Pos) as BlockWateringCan;

            capi = api as ICoreClientAPI;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (byItemStack != null)
            {
                SecondsWateringLeft = (byItemStack.Block as BlockWateringCan).GetRemainingWateringSeconds(byItemStack);
            }
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);

            SecondsWateringLeft = tree.GetFloat("secondsWateringLeft");
            MeshAngle = tree.GetFloat("meshAngle", MeshAngle);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetFloat("secondsWateringLeft", SecondsWateringLeft);
            tree.SetFloat("meshAngle", MeshAngle);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(capi.TesselatorManager.GetDefaultBlockMesh(Block).Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, MeshAngle, 0));
            return true;
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            double perc = Math.Round(100 * SecondsWateringLeft / ownBlock.CapacitySeconds);
            if (perc < 1)
            {
                dsc.AppendLine(Lang.Get("Empty"));
            }
            else
            {
                dsc.AppendLine(Lang.Get("{0}% full", perc));
            }
        }

    }
}
