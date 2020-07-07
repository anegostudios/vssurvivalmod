using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPAngledGears : BEBehaviorMPBase
    {
        public BlockFacing axis1 = null;
        public BlockFacing axis2 = null;
        private BEBehaviorMPLargeGear3m largeGear;

        public override float AngleRad
        {
            get
            {
                float angle = base.AngleRad;

                bool flip = inTurnDir.Facing == BlockFacing.DOWN || inTurnDir.Facing == BlockFacing.WEST;
                if (inTurnDir.Facing == this.orientation && (this.orientation == BlockFacing.WEST || this.orientation == BlockFacing.EAST)) flip = !flip;
                //if (flip) return /*lastKnownAngleRad = - why do i do this? it creates massive jitter*/ GameMath.TWOPI - angle;

                return flip ? GameMath.TWOPI - angle : angle;
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
            string orientations = (Block as BlockAngledGears).Orientation;

            this.orientation = null;
            switch (orientations)
            {
                case "n":
                case "nn":   //"nn" "ss" "ee" "ww" variants are used for the single cage gear instead of the peg gear (e.g. on interaction with LargeGear3)
                    this.orientation = BlockFacing.NORTH;
                    AxisSign = new int[3] { 0, 0, -1 };   //for these "single direction" gears, the renderertype is generic, i.e. only one mesh, the peg gear, will be rendered
                    break;
                case "s":
                case "ss":
                    AxisSign = new int[3] { 0, 0, -1 };
                    this.orientation = BlockFacing.SOUTH;
                    break;

                case "e":
                case "ee":
                    AxisSign = new int[3] { 1, 0, 0 };
                    this.orientation = BlockFacing.EAST;
                    break;

                case "w":
                case "ww":
                    AxisSign = new int[3] { -1, 0, 0 };
                    this.orientation = BlockFacing.WEST;
                    break;

                case "u":
                    AxisSign = new int[3] { 0, -1, 0 }; 
                    break;

                case "d":
                    AxisSign = new int[3] { 0, 1, 0 };
                    break;

                case "es":
                    AxisSign = new int[6] { 1, 0, 0, 0, 0, -1 };  //for all these "2 directions" gears, the rendererByType is "angledgears" ie. AngledGearBlockRenderer and two meshes will be rendered, the renderer looks for a special 6 member AxisSign array
                    axis1 = null;
                    axis2 = null;
                    break;

                case "ws":
                    AxisSign = new int[6] { 0, 0, -1, -1, 0, 0 };
                    axis1 = null;
                    axis2 = null;
                    break;

                case "nw":
                    AxisSign = new int[6] { 1, 0, 0, 0, 0, -1 };
                    axis1 = BlockFacing.EAST;    //tested    Rotation reverse for these two inTurnDir axes
                    axis2 = BlockFacing.NORTH;    //tested
                    break;

                case "sd":
                    AxisSign = new int[6] { 0, 0, -1, 0, -1, 0 };
                    axis1 = BlockFacing.SOUTH;  //tested
                    axis2 = BlockFacing.UP;  //tested
                    break;

                case "ed":
                    AxisSign = new int[6] { 0, 1, 0, 1, 0, 0 };
                    axis1 = BlockFacing.EAST;  //tested
                    axis2 = BlockFacing.DOWN;  //tested
                    break;

                case "wd":
                    AxisSign = new int[6] { -1, 0, 0, 0, 1, 0 };
                    axis1 = null;
                    axis2 = null;
                    break;

                case "nd":
                    AxisSign = new int[6] { 0, 0, -1, 0, 1, 0 };
                    axis1 = BlockFacing.DOWN;
                    axis2 = null;
                    break;

                case "nu":
                    AxisSign = new int[6] { 0, -1, 0, 0, 0, -1 };
                    axis1 = null;
                    axis2 = null;
                    break;

                case "eu":
                    AxisSign = new int[6] { 0, -1, 0, 1, 0, 0 };
                    axis1 = null;
                    axis2 = null;
                    break;

                case "su":
                    AxisSign = new int[6] { 0, 1, 0, 0, 0, -1 };
                    axis1 = BlockFacing.DOWN;   //tested    Rotation reverse for these two inTurnDir axes
                    axis2 = BlockFacing.SOUTH;
                    break;

                case "wu":
                    AxisSign = new int[6] { 0, -1, 0, -1, 0, 0 };
                    axis1 = null;
                    axis2 = null;
                    break;

                case "en":
                    AxisSign = new int[6] { 0, 0, 1, 1, 0, 0 };
                    axis1 = BlockFacing.SOUTH;   //tested    Rotation reverse for these two inTurnDir axes
                    axis2 = BlockFacing.EAST;   //tested
                    break;

                default:
                    AxisSign = new int[6] { 0, 0, 1, 1, 0, 0 };
                    axis1 = null;
                    axis2 = null;
                    break;
            }

            if (orientations.Length == 2 && orientations[0] == orientations[1])
            {
                BlockPos largeGearPos = this.Position.AddCopy(this.orientation.GetOpposite());
                BlockEntity be = this.Api.World?.BlockAccessor.GetBlockEntity(largeGearPos);
                largeGear = be?.GetBehavior<BEBehaviorMPLargeGear3m>();
            }
        }

        internal float LargeGearAngleRad(float unchanged)
        {
            if (largeGear == null)
            {
                BlockPos largeGearPos = this.Position.AddCopy(this.orientation.GetOpposite());
                BlockEntity be = this.Api.World?.BlockAccessor.GetBlockEntity(largeGearPos);
                largeGear = be?.GetBehavior<BEBehaviorMPLargeGear3m>();
                if (largeGear == null) return unchanged;
            }
            int dir = this.orientation == BlockFacing.SOUTH ? -1 : 1;
            return dir * largeGear.GetSmallgearAngleRad() % GameMath.TWOPI;
        }

        internal void CreateNetworkFromHere()
        {
            OutFacingForNetworkDiscovery = this.orientation;
            if (Api.Side == EnumAppSide.Server && OutFacingForNetworkDiscovery != null)
            {
                CreateJoinAndDiscoverNetwork(OutFacingForNetworkDiscovery);
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            return base.OnTesselation(mesher, tesselator);
        }

        public override TurnDirection GetTurnDirection(BlockFacing forFacing)
        {
            string orientations = Block.Variant["orientation"];
            bool invert = false;

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
            if (largeGear == null) return 0.0005f;
            //If meshed with a large gear, this variable resistance is the key to getting the two networks quickly in sync with (reasonably) realistic physics; and to slow this network down in sync with the large gear
            float dSpeed = 1;
            if (largeGear.Network != null)
            {
                dSpeed = (this.Network.Speed - largeGear.Network.Speed * largeGear.ratio) * 10f;
            }
            return dSpeed > 0 ? dSpeed * dSpeed * 2 : 0.0005f;
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

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
            if (Api.World.EntityDebugMode)
            {
                string orientations = Block.Variant["orientation"];
                sb.AppendLine(string.Format(Lang.Get("Orientation: {0}", orientations)));
                bool rev = inTurnDir.Facing == axis1 || inTurnDir.Facing == axis2;
                sb.AppendLine(string.Format(Lang.Get("Rotation: {0} - {1}", inTurnDir.Rot, (rev ? "-" : "") + inTurnDir.Facing)));
            }
        }

    }
}
