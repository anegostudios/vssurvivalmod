using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityLayer : BlockEntity
    {
        protected static readonly int WEIGHTLIMIT = 75;
        protected readonly static Vec3d center = new Vec3d(0.5, 0.125, 0.5);

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            RegisterGameTickListener(OnEvery250Ms, 250);
        }

        private void OnEvery250Ms(float dt)
        {
            // Random checks for breaking this block if heavy entity above and unsupported below

            IWorldAccessor world = Api.World;
            Vec3d pos3d = center.AddCopy(Pos);
            BlockPos down = Pos.DownCopy();

            // If this block is unsupported, do an entity weight + block breaking check
            if (!CheckSupport(world.BlockAccessor, down))
            {
                Entity[] entities = world.GetEntitiesAround(pos3d, 1.0f, 1.5f, (e) => (e?.Properties.Weight > WEIGHTLIMIT));
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];
                    Cuboidd eBox = new Cuboidd();
                    EntityPos pos = entity.Pos;
                    eBox.Set(entity.SelectionBox).Translate(pos.X, pos.Y, pos.Z);

                    Cuboidf bBox = new Cuboidf();
                    bBox.Set(this.Block.CollisionBoxes[0]);
                    bBox.Translate(Pos.X, Pos.Y, Pos.Z);

                    // Check entity yPos actually intersects with this block (approximately)
                    if (eBox.MinY <= bBox.MaxY + 0.01 && eBox.MinY >= bBox.MinY - 0.01)
                    {
                        // Check whether supported enough on any surrounding side
                        bool checkSouth = eBox.MaxZ > bBox.Z2;
                        bool checkNorth = eBox.MinZ < bBox.Z1;
                        bool checkWest = eBox.MinX < bBox.X1;
                        bool checkEast = eBox.MinZ > bBox.X2;

                        bool supported = false;
                        IBlockAccessor access = world.BlockAccessor;
                        if (checkEast) supported |= CheckSupport(access, down.EastCopy());
                        if (checkEast && checkNorth) supported |= CheckSupport(access, down.EastCopy().North());
                        if (checkEast && checkSouth) supported |= CheckSupport(access, down.EastCopy().South());
                        if (checkWest) supported |= CheckSupport(access, down.WestCopy());
                        if (checkWest && checkNorth) supported |= CheckSupport(access, down.WestCopy().North());
                        if (checkWest && checkSouth) supported |= CheckSupport(access, down.WestCopy().South());
                        if (checkNorth) supported |= CheckSupport(access, down.NorthCopy());
                        if (checkSouth) supported |= CheckSupport(access, down.SouthCopy());

                        if (!supported)
                        {
                            // Break the block and the entity will fall :)

                            // ## TODO
                        }
                    }
                }
            }

            return;
        }

        protected bool CheckSupport(IBlockAccessor access, BlockPos pos)
        {
            // Simple test for support - any easily replaceable block (e.g. air, plants, snow layer) can't provide support
            return access.GetBlock(pos).Replaceable < 6000;
        }
    }
}
