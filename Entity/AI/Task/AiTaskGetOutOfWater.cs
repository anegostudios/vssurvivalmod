using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskGetOutOfWater : AiTaskBase
    {
        Vec3d target;
        bool done;
        float moveSpeed = 0.03f;

        public AiTaskGetOutOfWater(EntityAgent entity) : base(entity)
        {

        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.06f);
            }
        }

        public override bool ShouldExecute()
        {
            if (!entity.Swimming) return false;
            if (rand.NextDouble() > 0.04f) return false;
            

            int tries = 6;
            while (tries-- > 0)
            {
                target = entity.ServerPos.XYZ.Add(rand.Next(21) - 10, 0, rand.Next(21) - 10);
                pos.X = (int)target.X;
                pos.Z = (int)target.Z;
                pos.Y = entity.World.BlockAccessor.GetTerrainMapheightAt(pos);

                Block block = entity.World.BlockAccessor.GetBlock(pos);
                Block belowblock = entity.World.BlockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);

                bool canStep = block.CollisionBoxes == null || block.CollisionBoxes.Max((cuboid) => cuboid.Y2) <= 1f;
                bool canStand = belowblock.CollisionBoxes != null && belowblock.CollisionBoxes.Length > 0;

                if (canStand && canStep) return true;
            }

            return false;
        }

        BlockPos pos = new BlockPos();

        public override void StartExecute()
        {
            base.StartExecute();

            done = false;
            entity.PathTraverser.GoTo(target, moveSpeed, OnGoalReached, OnStuck);

            //entity.world.SpawnParticles(10, ColorUtil.WhiteArgb, target.AddCopy(new Vec3f(-0.1f, -0.1f, -0.1f)), target.AddCopy(new Vec3f(0.1f, 0.1f, 0.1f)), new Vec3f(), new Vec3f(), 1f, 1f);

        }

        public override bool ContinueExecute(float dt)
        {
            if (entity.Swimming)
            {
                Block aboveblock = entity.World.BlockAccessor.GetBlock((int)entity.ServerPos.X, (int)(entity.ServerPos.Y + entity.CollisionBox.Y2 * 0.25f), (int)entity.ServerPos.Z);

                if (aboveblock.IsLiquid()) entity.ServerPos.Motion.Y = Math.Min(entity.ServerPos.Motion.Y + 0.005f, 0.03f);
            }

            if (rand.NextDouble() < 0.1f)
            {
                Block block = entity.World.BlockAccessor.GetBlock((int)entity.ServerPos.X, (int)entity.ServerPos.Y, (int)entity.ServerPos.Z);
                if (block.CollisionBoxes != null && block.CollisionBoxes.Length > 0 && !entity.FeetInLiquid) return false;
            }

            return !done;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);

            entity.PathTraverser.Stop();
        }

        private void OnStuck()
        {
            done = true;
        }

        private void OnGoalReached()
        {
            done = true;
        }
    }
}
