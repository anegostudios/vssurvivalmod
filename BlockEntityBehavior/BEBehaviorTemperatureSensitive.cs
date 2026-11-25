using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public delegate void OnStackToCool(ItemSlot slot, Vec3d pos, float targettemp, bool sizzle);

    public interface ITemperatureSensitive
    {
        bool IsHot { get; }

        /// <summary>
        /// If you need to cool down an itemstack, make sure to use the "onStackToCoolCallback" callback so that quenchable 
        /// items are correctly cooled down.
        /// </summary>
        /// <param name="amountRel"></param>
        /// <param name="onStackToCoolCallback"></param>
        void CoolNow(float amountRel, OnStackToCool onStackToCoolCallback);

        public static void CoolStack(ICoreAPI api, ItemSlot slot, Vec3d pos, float amountRel, float targettemp, bool sizzle)
        {
            var stack = slot.Itemstack;
            if (stack == null) return;
            
            if (stack.Collectible.HasBehavior<CollectibleBehaviorQuenchable>())
            {
                float dt = amountRel / 10f;
                CollectibleBehaviorQuenchable.CoolToTemperature(api.World, slot, pos, dt, GlobalConstants.CollectibleDefaultTemperature, false);
            }
            else
            {
                float temp = stack.Collectible.GetTemperature(api.World, stack);
                stack.Collectible.SetTemperature(api.World, stack, Math.Min(1100, temp - amountRel * 20), false);
            }
        }
    }

    /// <summary>
    /// For block entities that need to react to external influences that affect temperature or burn characteristics/
    /// Defined with the "TemperatureSensitive" code, and has no properties.
    /// </summary>
    /// <example><code lang="json">
    ///"entityBehaviors": [
    ///	{ "name": "TemperatureSensitive" }
    ///],
    /// </code></example>
    [DocumentAsJson]
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

            its = Blockentity as ITemperatureSensitive;

            if (!(Blockentity is ITemperatureSensitive))
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
                its.CoolNow(1f, (slot, pos, temp, siz) => onStackToCool(slot, pos, temp, 1f, siz));
                wateredSum -= 0.2f;
            }
        }

        private void onStackToCool(ItemSlot slot, Vec3d pos, float targettemp, float amount, bool sizzle)
        {
            ITemperatureSensitive.CoolStack(Api, slot, pos, amount, targettemp, sizzle);
        }

        private void onTick(float dt)
        {
            if (!its.IsHot) return;

            wateredSum = Math.Max(0, wateredSum - dt / 2);

            // Check if under water
            var lblock = Api.World.BlockAccessor.GetBlock(Pos, BlockLayersAccess.Fluid);
            if (lblock.IsLiquid() && lblock.LiquidCode != "lava")
            {
                its.CoolNow(25f, (slot, pos, temp, siz) => onStackToCool(slot, pos, temp, 25f, siz));
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
                its.CoolNow(rainLevel, (slot, pos, temp, siz) => onStackToCool(slot, pos, temp, rainLevel, siz));
            }
        }


    }
}
