using System;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntitySmeltedContainer : BlockEntity
    {
        public ItemStack contents;
        public int units = 0;

        public float Temperature
        {
            get { return contents.Collectible.GetTemperature(api.World, contents); }
        }

        public BlockEntitySmeltedContainer() : base()
        {
            
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreClientAPI)
            {
                RegisterGameTickListener(OnClientTick, 500);
            }
        }

        private void OnClientTick(float dt)
        {
            if (contents?.Collectible?.CombustibleProps == null) return;
            int meltingPoint = contents.Collectible.CombustibleProps.MeltingPoint;
            if (meltingPoint * 0.9 > Temperature) return;

            if (api.World.Rand.NextDouble() > 0.5)
            {
                Vec3d pos = this.pos.ToVec3d().Add(0.45, 0.44, 0.45);
                BlockSmeltedContainer.smokeHeld.minPos = pos;
                api.World.SpawnParticles(BlockSmeltedContainer.smokeHeld);
            }
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolve)
        {
            base.FromTreeAtributes(tree, worldForResolve);

            contents = tree.GetItemstack("contents");
            units = tree.GetInt("units");

            if (api?.World != null) contents.ResolveBlockOrItem(api.World);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetItemstack("contents", contents);
            tree.SetInt("units", units);
        }
    }
}
