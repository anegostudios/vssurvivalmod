using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityTorch : BlockEntityTransient, ITemperatureSensitive
    {
        public bool IsHot => true;

        public override void Initialize(ICoreAPI api)
        {
            CheckIntervalMs = 1000;
            base.Initialize(api);
        }

        public override void CheckTransition(float dt)
        {
            if (Api.World.Rand.NextDouble() < 0.3)
            {
                base.CheckTransition(dt);
            }
        }

        public void CoolNow(float amountRel)
        {
            if (Api.World.Rand.NextDouble() < amountRel * 5)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), Pos, 0.25, null, false, 16);
                if (Api.World.Rand.NextDouble() < 0.2 + amountRel / 2f)
                {
                    Block block = Api.World.BlockAccessor.GetBlock(Pos);
                    if (block.Attributes == null) return;
                    var toCode = block.CodeWithVariant("state", "extinct").ToShortString();
                    tryTransition(toCode);
                }
            }
        }
    }
}
