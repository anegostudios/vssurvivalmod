using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockEntityWildCrop : BlockEntityTransient
    {

        WeatherSystemBase wsys;
        Vec3d tmpPos = new Vec3d();

        public override void Initialize(ICoreAPI api)
        {
            CheckIntervalMs = 1000;
            wsys = api.ModLoader.GetModSystem<WeatherSystemBase>();

            base.Initialize(api);

        }

        public override void CheckTransition(float dt)
        {
            tmpPos.Set(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
            double rainLevel = 0;
            bool rainCheck =
                Api.Side == EnumAppSide.Server
                && Api.World.Rand.NextDouble() < 0.15
                && Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= Pos.Y
                && (rainLevel = wsys.GetPrecipitation(tmpPos)) > 0.04
            ;
            
            if (rainCheck && Api.World.Rand.NextDouble() < rainLevel * 5)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), Pos.X + 0.5, Pos.Y + 0.75, Pos.Z + 0.5, null, false, 16);
                if (Api.World.Rand.NextDouble() < 0.2 + rainLevel/2f)
                {
                    Block block = Api.World.BlockAccessor.GetBlock(Pos);
                    if (block.Attributes == null) return;
                    var toCode = block.CodeWithVariant("state", "extinct").ToShortString();
                    tryTransition(toCode);
                }
            }

            if (Api.World.Rand.NextDouble() < 0.3)
            {
                base.CheckTransition(dt);
            }
        }
    }
}
