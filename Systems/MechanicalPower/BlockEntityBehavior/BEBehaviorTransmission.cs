using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPTransmission : BEBehaviorMPAxle
    {
        protected bool engaged;

        public BEBehaviorMPTransmission(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            Shape = new CompositeShape() { Base = new AssetLocation("shapes/block/wood/mechanics/axle.json") };
        }

        protected override bool AddStands => false;

        protected override MechPowerPath[] GetMechPowerExits(TurnDirection fromExitTurnDir)
        {
            if (!engaged) return new MechPowerPath[0];

            return base.GetMechPowerExits(fromExitTurnDir);
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
