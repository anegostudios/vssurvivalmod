using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{

    public interface ITemperatureSensitive
    {
        bool IsHot { get; }
        void CoolNow(float amountRel);
    }

    /// <summary>
    /// For block entities that need to react to external influences that affect temperature or burn characteristics
    /// </summary>
    public class BEBehaviorTemperatureSensitive : BlockEntityBehavior
    {
        ITemperatureSensitive its;
        WeatherSystemBase wsys;
        Vec3d tmpPos = new Vec3d();

        public BEBehaviorTemperatureSensitive(BlockEntity blockentity) : base(blockentity) { }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            Blockentity.RegisterGameTickListener(onTick, 1900 + Api.World.Rand.Next(200), Api.World.Rand.Next(500));

            its = Blockentity.Block.GetInterface<ITemperatureSensitive>(api.World, Pos);

            if (its == null)
            {
                throw new InvalidOperationException("Applying BehaviorTemperatureSensitive to a block entity requires that block entity class to implement ITemperatureSensitive");
            }
            wsys = api.ModLoader.GetModSystem<WeatherSystemBase>();
        }

        float wateredSum = 0;
        public void OnWatered(float dt)
        {
            wateredSum += dt;
            if (wateredSum > 0.2f)
            {
                its.CoolNow(1f);
                wateredSum -= 0.2f;
            }
        }

        private void onTick(float dt)
        {
            if (!its.IsHot) return;

            wateredSum = Math.Max(0, wateredSum - dt / 2);

            // Check if under water
            var lblock = Api.World.BlockAccessor.GetBlock(Pos, BlockLayersAccess.Fluid);
            if (lblock.IsLiquid() && lblock.LiquidCode != "lava")
            {
                its.CoolNow(25f);
                return;
            }

            // Check if exposed to rain
            tmpPos.Set(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
            float rainLevel = 0;
            bool rainCheck =
                Api.Side == EnumAppSide.Server
                && Api.World.Rand.NextDouble() < 0.75
                && Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= Pos.Y
                && (rainLevel = wsys.GetPrecipitation(tmpPos)) > 0.04
            ;

            if (rainCheck && Api.World.Rand.NextDouble() < rainLevel * 5)
            {
                its.CoolNow(rainLevel);
            }
        }


    }
}
