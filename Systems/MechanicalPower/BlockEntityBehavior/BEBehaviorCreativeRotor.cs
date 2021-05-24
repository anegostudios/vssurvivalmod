using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPCreativeRotor : BEBehaviorMPRotor
    {
        private int powerSetting;

        protected override AssetLocation Sound => null;

        protected override float Resistance => 0.3f;
        protected override double AccelerationFactor => 1d;
        protected override float TargetSpeed => 0.1f * powerSetting;
        protected override float TorqueFactor => 0.07f * powerSetting;

        public BEBehaviorMPCreativeRotor(BlockEntity blockentity) : base(blockentity)
        {
            this.powerSetting = 3;
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
        }

        protected override CompositeShape GetShape()
        {
            CompositeShape shape = Block.Shape.Clone();
            shape.Base = new AssetLocation("shapes/block/metal/mechanics/creativerotor-spinbar.json");
            return shape;
        }

        internal bool OnInteract(IPlayer byPlayer)
        {
            if (++this.powerSetting > 10) this.powerSetting = 1;
            Blockentity.MarkDirty(true);
            return true;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            base.OnTesselation(mesher, tesselator);

            ICoreClientAPI capi = Api as ICoreClientAPI;
            Shape shape = capi.Assets.TryGet("shapes/block/metal/mechanics/creativerotor-frame.json").ToObject<Shape>();
            float rotateY = 0f;
            switch (BlockFacing.FromCode(Block.Variant["side"]).Index)
            {
                case 0:
                    AxisSign = new int[] { 0, 0, 1 };
                    rotateY = 180;
                    break;
                case 1:
                    AxisSign = new int[] { -1, 0, 0 };
                    rotateY = 90;
                    break;
                case 3:
                    AxisSign = new int[] { 1, 0, 0 };
                    rotateY = 270;
                    break;
                default:
                    break;
            }
            MeshData mesh;
            capi.Tesselator.TesselateShape(Block, shape, out mesh, new Vec3f(0, rotateY, 0));
            mesher.AddMeshData(mesh);
            return true;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world)
        {
            powerSetting = tree.GetInt("p");
            if (powerSetting > 10 || powerSetting < 1) powerSetting = 3;
            base.FromTreeAttributes(tree, world);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetInt("p", powerSetting);
            base.ToTreeAttributes(tree);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
            sb.AppendLine(string.Format(Lang.Get("Power: {0}%", (int)(10 * powerSetting))));
        }

    }
}
