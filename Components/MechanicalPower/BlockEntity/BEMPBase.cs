using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace Vintagestory.GameContent.Mechanics
{
    public abstract class BEMPBase : BlockEntity, IBlockShapeSupplier, IMechanicalPowerNode
    { 
        protected MechanicalPowerMod manager;
        protected MechanicalNetwork network;
        
        public Vec4f lightRbs = new Vec4f();

        public virtual BlockPos Position { get { return pos; } }
        public virtual Vec4f LightRgba { get { return lightRbs; } }
        
        public virtual Block Block { get; protected set; }

        public virtual int[] AxisMapping { get; protected set; }
        public virtual int[] AxisSign { get; protected set; }

        public long NetworkId { get; set; }

        public MechanicalNetwork Network => network;

        public BlockFacing orientation;
        protected EnumTurnDirection turnDir;
        protected BlockFacing turnDirFromFacing = BlockFacing.NORTH;
        protected float lastKnownAngle = 0;

        public float Angle
        {
            get
            {
                if (network == null) return lastKnownAngle;

                if (network.TurnDirection != turnDir)
                {
                    return (lastKnownAngle = 360 - network.Angle);
                }

                return (lastKnownAngle = network.Angle);
            }
        }

        

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            Block = api.World.BlockAccessor.GetBlock(pos);

            manager = api.ModLoader.GetModSystem<MechanicalPowerMod>();

            if (api.World.Side == EnumAppSide.Client)
            {
                lightRbs = api.World.BlockAccessor.GetLightRGBs(pos);
            }

            manager.AddDeviceForRender(this);
            if (NetworkId > 0)
            {
                network = manager.GetOrCreateNetwork(NetworkId);
            }

            AxisMapping = new int[3] { 0, 1, 2 };
            AxisSign = new int[3] { 1, 1, 1 };
        }

        
        public void JoinNetwork(MechanicalNetwork network)
        {
            this.network = network;
            network.Join(this);
        
            if (network == null) NetworkId = 0;
            else
            {
                NetworkId = network.networkId;
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            network.Leave(this);
            manager.RemoveDeviceForRender(this);
        }

        public virtual void DidConnectTo(BlockPos pos, BlockFacing facing)
        {
            BEMPBase nbe = api.World.BlockAccessor.GetBlockEntity(pos) as BEMPBase;
            if (nbe != null)
            {
                NetworkId = nbe.NetworkId;
            }
        }

        public virtual bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            lightRbs = api.World.BlockAccessor.GetLightRGBs(pos);

            return true;
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);

            NetworkId = tree.GetLong("networkid");
            if (manager != null)
            {
                network = manager.GetOrCreateNetwork(NetworkId);
            }

            turnDirFromFacing = BlockFacing.ALLFACES[tree.GetInt("turnDirFromFacing")];
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetLong("networkid", NetworkId);
            tree.SetInt("turnDirFromFacing", turnDirFromFacing.Index);
        }

        public override void OnBlockBroken()
        {
            base.OnBlockBroken();

            //network?.OnBlockBroken(pos);
        }

        public virtual void OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer)
        {

        }

        public override string GetBlockInfo(IPlayer forPlayer)
        {
            //if (api.World.EntityDebugMode)
            {
                return "networkid: " + NetworkId + ", angle: " + Angle + "\nturnDir: " + turnDir + "\nnetwork turn dir: " + network?.TurnDirection;
            }

            //return base.GetBlockInfo(forPlayer);
        }

        
        public virtual EnumTurnDirection GetBaseTurnDirection()
        {
            return turnDir;
        }

        public virtual void SetBaseTurnDirection(EnumTurnDirection turnDir, BlockFacing fromFacing)
        {
            this.turnDir = turnDir;
        }

        public abstract float GetTorque();
        public abstract float GetResistance();
        public abstract EnumTurnDirection GetTurnDirection(BlockFacing forFacing);
    }
}
