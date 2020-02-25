using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPTransmission : BEBehaviorMPBase
    {
        protected bool engaged;

        BlockFacing[] orients = new BlockFacing[2];
        ICoreClientAPI capi;
        string orientations;

        public override CompositeShape Shape
        {
            get
            {
                string side = Block.Variant["orientation"];

                CompositeShape shape = new CompositeShape() { 
                    Base = new AssetLocation("shapes/block/wood/mechanics/transmission-leftgear.json"), Overlays = new CompositeShape[] { 
                           new CompositeShape() { Base = new AssetLocation("shapes/block/wood/mechanics/transmission-rightgear.json") } } 
                };

                if (side == "ns")
                {
                    shape.rotateY = 90;
                    shape.Overlays[0].rotateY = 90;
                }

                return shape;
            }
            set
            {

            }
        }

        public BEBehaviorMPTransmission(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            orientations = Block.Variant["orientation"];
            switch (orientations)
            {
                case "ns":
                    AxisSign = new int[] { -1, -1, -1 };
                    orients[0] = BlockFacing.NORTH;
                    orients[1] = BlockFacing.SOUTH;
                    break;

                case "we":
                    AxisMapping = new int[] { 2, 1, 0 };
                    AxisSign = new int[] { -1, -1, -1 };

                    orients[0] = BlockFacing.WEST;
                    orients[1] = BlockFacing.EAST;
                    break;
            }
        }


        protected override MechPowerPath[] GetMechPowerExits(TurnDirection fromExitTurnDir)
        {
            if (!engaged) return new MechPowerPath[0];

            // Axles just forward mechanical power in the same direction with the same turn direction
            return new MechPowerPath[] { new MechPowerPath(fromExitTurnDir.Facing, fromExitTurnDir.Rot) };
        }


        public override TurnDirection GetTurnDirection(BlockFacing forFacing)
        {
            return GetInTurnDirection();
        }

        public override float GetResistance()
        {
            return 0.0005f;
        }

        public override float GetTorque()
        {
            return 0;
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

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            base.OnTesselation(mesher, tesselator);
            return false;
        }
    }
}
