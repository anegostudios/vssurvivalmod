using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPBrake : BEBehaviorMPAxle
    {
        protected bool engaged;

        public BEBehaviorMPBrake(BlockEntity blockentity) : base(blockentity)
        {
            
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            Shape = new CompositeShape() { Base = new AssetLocation("shapes/block/wood/mechanics/axle.json") };
        }

        protected override bool AddStands => false;


        public override float GetResistance()
        {
            if (engaged) return 10;

            return base.GetResistance();
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);

            engaged = tree.GetBool("engaged");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("engaged", engaged);
        }
    }
}
