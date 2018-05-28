using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockEntityAngledGears : BlockEntity, IBlockShapeSupplier, IMechanicalPowerDeviceVS
    {
        MechanicalPowerMod manager;
        public Vec4f lightRbs = new Vec4f();

        public BlockPos Position { get { return pos; } }
        public Vec4f LightRgba { get { return lightRbs; } }
        public MechDeviceType Type { get { return MechDeviceType.Gearbox; } }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            manager = api.ObjectCache["mechPowerMod"] as MechanicalPowerMod;
            manager.AddDevice(this);
        }


        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            manager.RemoveDevice(this);
        }

        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            lightRbs = api.World.BlockAccessor.GetLightRGBs(pos);

            return true;
        }
    }
}
