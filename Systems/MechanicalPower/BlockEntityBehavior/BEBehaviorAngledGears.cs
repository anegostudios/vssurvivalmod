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
        public BlockFacing turnDir1 = null;
        public BlockFacing turnDir2 = null;
        public BlockFacing orientation = null;
        public bool newlyPlaced = false;

        public override float AngleRad
        {
            get
            {
                float angle = base.AngleRad;

                bool flip = propagationDir == BlockFacing.DOWN || propagationDir == BlockFacing.WEST;
                if (propagationDir == this.orientation && (this.orientation == BlockFacing.WEST || this.orientation == BlockFacing.EAST)) flip = !flip;
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
            //this.Api = api;
            //Shape = GetShape();

            //manager = Api.ModLoader.GetModSystem<MechanicalPowerMod>();

            //if (Api.World.Side == EnumAppSide.Client)
            //{
            //    lightRbs = Api.World.BlockAccessor.GetLightRGBs(Blockentity.Pos);
            //    if (NetworkId > 0)
            //    {
            //        network = manager.GetOrCreateNetwork(NetworkId);
            //        JoinNetwork(network);
            //    }
            //}

            //manager.AddDeviceForRender(this);

            //AxisSign = new int[3] { 0, 0, 1 };

            //SetOrientations();
            //if (api.Side == EnumAppSide.Server && OutFacingForNetworkDiscovery != null)
            //{
            //    CreateJoinAndDiscoverNetwork(OutFacingForNetworkDiscovery);
            //}

            if (api.Side == EnumAppSide.Client)
            {
                Blockentity.RegisterGameTickListener(onEverySecond, 1000);
            }

            //if (largeGear != null)
            //{
            //    this.CreateNetworkFromHere();
            //    if (largeGear.Api != null)   //if already initialised - otherwise it will initialise itself later if this was called during chunk loading (it's random whether the angled gear or the large gear gets initialised first)
            //    {
            //        if (largeGear.Network == null) largeGear.CreateJoinAndDiscoverNetwork(BlockFacing.DOWN);
            //        if (largeGear.Network == null) largeGear.CreateJoinAndDiscoverNetwork(BlockFacing.UP);
            //    }
            //}
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

            //This applies only when the BE is being updated when the gear orientations change after a neighbour block breaks
            if (this.turnDir1 != null)
            {
                if (propagationDir == turnDir1) propagationDir = turnDir2.Opposite;
                else if (propagationDir == turnDir2) propagationDir = turnDir1.Opposite;
                else if (propagationDir == turnDir2.Opposite) propagationDir = turnDir1;
                else if (propagationDir == turnDir1.Opposite) propagationDir = turnDir2;
                this.turnDir1 = null;
                this.turnDir2 = null;
            }

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
                    axis1 = BlockFacing.EAST;
                    axis2 = null;
                    this.turnDir1 = BlockFacing.EAST;
                    this.turnDir2 = BlockFacing.SOUTH;
                    break;

                case "ws":
                    AxisSign = new int[6] { 0, 0, -1, -1, 0, 0 };
                    axis1 = BlockFacing.WEST;
                    axis2 = null;
                    this.turnDir1 = BlockFacing.WEST;
                    this.turnDir2 = BlockFacing.SOUTH;
                    break;

                case "nw":
                    AxisSign = new int[6] { 1, 0, 0, 0, 0, -1 };
                    axis1 = null;
                    axis2 = BlockFacing.EAST;
                    this.turnDir1 = BlockFacing.NORTH;
                    this.turnDir2 = BlockFacing.WEST;
                    break;

                case "sd":  //OK
                    AxisSign = new int[6] { 0, 0, -1, 0, -1, 0 };
                    axis1 = null;
                    axis2 = null; // BlockFacing.NORTH;
                    this.turnDir1 = BlockFacing.SOUTH;
                    this.turnDir2 = BlockFacing.DOWN;
                    break;

                case "ed":  //1
                    AxisSign = new int[6] { 0, 1, 0, 1, 0, 0 };
                    axis1 = BlockFacing.EAST;
                    axis2 = BlockFacing.DOWN;
                    this.turnDir1 = BlockFacing.EAST;
                    this.turnDir2 = BlockFacing.DOWN;
                    break;

                case "wd":
                    AxisSign = new int[6] { -1, 0, 0, 0, 1, 0 };
                    axis1 = BlockFacing.DOWN;  //1
                    axis2 = BlockFacing.WEST;  //1
                    this.turnDir1 = BlockFacing.WEST;
                    this.turnDir2 = BlockFacing.DOWN;
                    break;

                case "nd":  //north checked
                    AxisSign = new int[6] { 0, 0, -1, 0, 1, 0 };
                    axis1 = BlockFacing.DOWN;   //1
                    axis2 = null;
                    this.turnDir1 = BlockFacing.NORTH;
                    this.turnDir2 = BlockFacing.DOWN;
                    break;

                case "nu": //1
                    AxisSign = new int[6] { 0, -1, 0, 0, 0, -1 };
                    axis1 = BlockFacing.UP;
                    axis2 = null;
                    this.turnDir1 = BlockFacing.NORTH;
                    this.turnDir2 = BlockFacing.UP;
                    break;

                case "eu": //1
                    AxisSign = new int[6] { 0, -1, 0, 1, 0, 0 };
                    axis1 = BlockFacing.UP;   //1
                    axis2 = BlockFacing.EAST;  //1
                    this.turnDir1 = BlockFacing.EAST;
                    this.turnDir2 = BlockFacing.UP;
                    break;

                case "su":   //south,up checked
                    AxisSign = new int[6] { 0, 1, 0, 0, 0, -1 };
                    axis1 = BlockFacing.DOWN;
                    axis2 = null;
                    this.turnDir1 = BlockFacing.SOUTH;
                    this.turnDir2 = BlockFacing.UP;
                    break;

                case "wu": //1
                    AxisSign = new int[6] { 0, -1, 0, -1, 0, 0 };
                    axis1 = BlockFacing.WEST;  //1
                    axis2 = BlockFacing.UP;   //1
                    this.turnDir1 = BlockFacing.WEST;
                    this.turnDir2 = BlockFacing.UP;
                    break;

                case "en":
                    AxisSign = new int[6] { 0, 0, 1, 1, 0, 0 };
                    axis1 = BlockFacing.EAST;
                    axis2 = BlockFacing.NORTH;
                    this.turnDir1 = BlockFacing.EAST;
                    this.turnDir2 = BlockFacing.NORTH;
                    break;

                default:
                    AxisSign = new int[6] { 0, 0, 1, 1, 0, 0 };
                    axis1 = null;
                    axis2 = null;
                    break;
            }

            this.CheckLargeGearJoin();
        }

        protected void CheckLargeGearJoin()
        {
            string orientations = (Block as BlockAngledGears).Orientation;
            if (orientations.Length == 2 && orientations[0] == orientations[1])
            {
                BlockPos largeGearPos = this.Position.AddCopy(this.orientation.Opposite);
                BlockEntity be = this.Api.World?.BlockAccessor.GetBlockEntity(largeGearPos);
                largeGear = be?.GetBehavior<BEBehaviorMPLargeGear3m>();
            }
        }

        public void AddToLargeGearNetwork(BEBehaviorMPLargeGear3m largeGear, BlockFacing outFacing)
        {
            this.JoinNetwork(largeGear.Network);
            this.SetPropagationDirection(new MechPowerPath(outFacing, largeGear.GearedRatio * largeGear.ratio, largeGear.GetPropagationDirection() == BlockFacing.DOWN));
        }

        public override bool isInvertedNetworkFor(BlockPos pos)
        {
            return this.orientation != null && this.orientation != propagationDir;
        }

        public float LargeGearAngleRad(float unchanged)
        {
            if (largeGear == null)
            {
                BlockPos largeGearPos = this.Position.AddCopy(this.orientation.Opposite);
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

        public override void SetPropagationDirection(MechPowerPath path)
        {
            BlockFacing turnDir = path.NetworkDir();
            if (this.turnDir1 != null)
            //rotate the input turn direction if it's an angled gear   (this helps later blocks to know which sense the network is turning)
            {
                if (turnDir == turnDir1) turnDir = turnDir2.Opposite;
                else if (turnDir == turnDir2) turnDir = turnDir1.Opposite;
                else if (turnDir == turnDir2.Opposite) turnDir = turnDir1;
                else if (turnDir == turnDir1.Opposite) turnDir = turnDir2;
                path = new MechPowerPath(turnDir, path.gearingRatio, false);
            }
            base.SetPropagationDirection(path);
        }

        public override BlockFacing GetPropagationDirectionInput()
        {
            if (this.turnDir1 != null)
            {
                if (propagationDir == turnDir1) return turnDir2.Opposite;
                if (propagationDir == turnDir2) return turnDir1.Opposite;
                if (propagationDir == turnDir1.Opposite) return turnDir2;
                if (propagationDir == turnDir2.Opposite) return turnDir1;
            }
            return propagationDir;
        }

        public override bool IsPropagationDirection(BlockFacing test)
        {
            if (this.turnDir1 != null)
            {
                if (propagationDir == turnDir1) return propagationDir == test || propagationDir == turnDir2.Opposite;
                if (propagationDir == turnDir2) return propagationDir == test || propagationDir == turnDir1.Opposite;
                if (propagationDir == turnDir1.Opposite) return propagationDir == test || propagationDir == turnDir2;
                if (propagationDir == turnDir2.Opposite) return propagationDir == test || propagationDir == turnDir1;
            }
            return propagationDir == test;
        }

        public override void WasPlaced(BlockFacing connectedOnFacing)
        {
            //Skip this if already called CreateJoinAndDiscoverNetwork in Initialize()
            if ((Api.Side == EnumAppSide.Client || OutFacingForNetworkDiscovery == null) && connectedOnFacing != null)
            {
                if (connectedOnFacing.IsAxisWE)
                {
                    if (!tryConnect(BlockFacing.NORTH) && !tryConnect(BlockFacing.SOUTH))
                    {
                        Api.Logger.Notification("AG was placed fail connect 2nd: " + connectedOnFacing + " at " + Position);
                    }
                    return;
                }
                if (connectedOnFacing.IsAxisNS)
                {
                    if (!tryConnect(BlockFacing.WEST) && !tryConnect(BlockFacing.EAST))
                    {
                        Api.Logger.Notification("AG was placed fail connect 2nd: " + connectedOnFacing + " at " + Position);
                    }
                    return;
                }
            }
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

        protected override MechPowerPath[] GetMechPowerExits(MechPowerPath fromExitTurnDir)
        {
            if (this.orientation == null) this.SetOrientations();  //this method could be called from another (earlier in the loading chunk) block's Initialise() method, i.e. before this itself is initialised.
            if (largeGear == null)
            {
                if (newlyPlaced) fromExitTurnDir.invert = !fromExitTurnDir.invert;
                BlockFacing[] connectors = (Block as BlockAngledGears).Facings;
                BlockFacing inputSide = fromExitTurnDir.invert ? fromExitTurnDir.OutFacing : fromExitTurnDir.OutFacing.Opposite;
                bool inputSideMatches = connectors.Contains(inputSide);
                if (!newlyPlaced)   //the code for removing the inputSide from the MechPowerExits is unwanted for a newly placed AngledGears block - it needs to seek networks on both faces if newly placed
                {
                    if (inputSideMatches) connectors = connectors.Remove(inputSide);
                    else connectors = connectors.Remove(fromExitTurnDir.OutFacing);
                }
                MechPowerPath[] paths = new MechPowerPath[connectors.Length];
                for (int i = 0; i < paths.Length; i++)
                {
                    BlockFacing pathFacing = connectors[i];
                    paths[i] = new MechPowerPath(pathFacing, this.GearedRatio, connectors.Length < 2 && fromExitTurnDir.OutFacing == pathFacing.Opposite || inputSideMatches && pathFacing != inputSide || !inputSideMatches && pathFacing != inputSide.Opposite ? fromExitTurnDir.invert : !fromExitTurnDir.invert);
                }
                return paths;
            }
            else
            {
                //alternative code for a small gear connected to a Large Gear - essentially pass through
                MechPowerPath[] paths = new MechPowerPath[2];
                paths[0] = new MechPowerPath(this.orientation.Opposite, this.GearedRatio, this.orientation == fromExitTurnDir.OutFacing ? !fromExitTurnDir.invert : fromExitTurnDir.invert);
                paths[1] = new MechPowerPath(this.orientation, this.GearedRatio, this.orientation == fromExitTurnDir.OutFacing ? fromExitTurnDir.invert : !fromExitTurnDir.invert);
                return paths;
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
            if (Api.World.EntityDebugMode)
            {
                string orientations = Block.Variant["orientation"];
                bool rev = propagationDir == axis1 || propagationDir == axis2;
                sb.AppendLine(string.Format(Lang.Get("Orientation: {0} {1} {2}", orientations, this.orientation, rev ? "-" : "")));
            }
        }

        public void ClearLargeGear()
        {
            this.largeGear = null;
        }
    }
}
