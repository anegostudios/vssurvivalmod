using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPAngledGears : BEBehaviorMPBase
    {
        public override float AngleRad
        {
            get
            {
                float angle = base.AngleRad;

                if (inTurnDir.Facing == BlockFacing.DOWN || inTurnDir.Facing == BlockFacing.WEST) return /*lastKnownAngleRad = - why do i do this? it creates massive jitter*/ GameMath.TWOPI - angle;

                return angle;
            }
        }
        public BEBehaviorMPAngledGears(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            SetOrientations();

            if (api.Side == EnumAppSide.Client)
            {
                Blockentity.RegisterGameTickListener(onEverySecond, 1000);
            }
        }

        private void onEverySecond(float dt)
        {
            float speed = network == null ? 0 : network.Speed;

            if (Api.World.Rand.NextDouble() < speed / 3f)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/woodcreak"), Position.X + 0.5, Position.Y + 0.5, Position.Z + 0.5, null, 0.75f + speed);
            }
        }

        public override void SetOrientations()
        { 
            string orientations = Block.Variant["orientation"];

            switch (orientations)
            {
                case "n":
                case "s":
                    //AxisMapping = new int[6] { 2, 1, 0, 0, 1, 2 };
                    AxisSign = new int[6] { -1, -1, -1, -1, -1, -1 };
                    break;

                case "e":
                    AxisMapping = new int[6] { 2, 1, 0, 0, 1, 2 };
                    AxisSign = new int[6] { 1, 1, 1, 1, 1, 1 };
                    break;

                case "w":
                    AxisMapping = new int[6] { 2, 1, 0, 0, 1, 2 };
                    AxisSign = new int[6] { -1, -1, -1, -1, -1, -1 };
                    break;

                case "u":
                    AxisMapping = new int[6] { 1, 2, 0, 0, 2, 1 };
                    AxisSign = new int[6] { -1, -1, -1, -1, -1, -1 }; 
                    break;

                case "d":
                    AxisMapping = new int[6] { 1, 2, 0, 0, 2, 1 };
                    AxisSign = new int[6] { 1, 1, 1, 1, 1, 1 };
                    break;

                case "es":
                    AxisMapping = new int[6] { 2, 1, 0, 0, 1, 2 };
                    AxisSign = new int[6] { 1, 1, 1, -1, -1, -1 };
                    
                    break;


                case "ws":
                    AxisMapping = new int[6] { 0, 1, 2, 2, 1, 0 };
                    AxisSign = new int[6] { -1, -1, -1, -1, -1, -1 };
                    break;

                case "nw":
                    AxisMapping = new int[6] { 2, 1, 0, 0, 1, 2 };
                    //AxisSign = new int[6] { 1, -1, -1, -1, -1, -1 };
                    AxisSign = new int[6] { 1, 1, 1, -1, -1, -1 };
                    break;

                case "sd":
                    AxisMapping = new int[6] { 0, 1, 2, 1, 2, 0 };
                    AxisSign = new int[6] { -1, -1, -1, -1, -1, -1 };
                    break;

                case "ed":
                    AxisMapping = new int[6] { 0, 2, 1, 2, 1, 0 };
                    AxisSign = new int[6] { 1, 1, 1, 1, 1, 1 };
                    break;

                case "wd":
                    AxisMapping = new int[6] { 2, 1, 0, 0, 2, 1 };
                    AxisSign = new int[6] { -1, -1, -1, 1, 1, 1 };
                    break;

                case "nd":
                    AxisMapping = new int[6] { 0, 1, 2, 0, 2, 1 };
                    AxisSign = new int[6] { -1, -1, -1, 1, 1, 1 };
                    break;

                case "nu":
                    AxisMapping = new int[6] { 0, 2, 1, 0, 1, 2 };
                    AxisSign = new int[6] { -1, -1, -1, -1, -1, -1 };
                    break;

                case "eu":
                    AxisMapping = new int[6] { 0, 2, 1, 2, 1, 0 };
                    AxisSign = new int[6] { -1, -1, -1, 1, 1, 1 };
                    break;

                case "su":
                    AxisMapping = new int[6] { 1, 2, 0, 0, 1, 2 };
                    AxisSign = new int[6] { 1, 1, 1, -1, -1, -1 };
                    break;

                case "wu":
                    AxisMapping = new int[6] { 1, 2, 0, 2, 1, 0 };
                    AxisSign = new int[6] { -1, -1, -1, -1, -1, -1 };
                    break;

                case "en":
                    AxisMapping = new int[6] { 0, 1, 2, 2, 1, 0 };
                    AxisSign = new int[6] { 1, 1, 1, 1, 1, 1 };
                    break;

                default:
                    AxisMapping = new int[6] { 0, 1, 2, 2, 1, 0 };
                    AxisSign = new int[6] { 1, 1, 1, 1, 1, 1 };
                    break;
            }
        }


        /*public override void OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer)
        {
            if (world.Side == EnumAppSide.Client) MarkDirty(true);
        }*/

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            return base.OnTesselation(mesher, tesselator);
        }


        public override TurnDirection GetTurnDirection(BlockFacing forFacing)
        {
            string orientations = Block.Variant["orientation"];
            bool invert = false; // orientations.Contains("u");

            return forFacing == inTurnDir.Facing ?
                inTurnDir :
                new TurnDirection(forFacing, invert ? 1 - inTurnDir.Rot : inTurnDir.Rot)
            ;
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
                base.ToTreeAttributes(tree);
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);
        }



        public override float GetResistance()
        {
            return 0.0005f;
        }

        public override float GetTorque()
        {
            return 0;
        }

        protected override MechPowerPath[] GetMechPowerExits(TurnDirection fromExitTurnDir)
        {
            BlockFacing[] connectors = (Block as BlockAngledGears).Facings;
            connectors = connectors.Remove(fromExitTurnDir.Facing.GetOpposite());

            MechPowerPath[] paths = new MechPowerPath[connectors.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                paths[i] = new MechPowerPath(connectors[i], fromExitTurnDir.Rot);
            }

            return paths;
        }
    }
}
