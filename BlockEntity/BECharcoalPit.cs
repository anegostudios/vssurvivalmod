using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockEntityCharcoalPit : BlockEntity
    {
        static float BurnHours = 18;

        // Key = horizontal location
        // Value = highest Y Position
        Dictionary<BlockPos, int> smokeLocations = new Dictionary<BlockPos, int>();

        double finishedAfterTotalHours;
        double startingAfterTotalHours;

        // 0 = warmup
        // 1 = burning
        int state;

        Block charcoalPitBlock;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            RegisterGameTickListener(OnTick, 3000);
            if (api.Side == EnumAppSide.Client)
            {
                RegisterGameTickListener(OnClientTick, 150);
            }

            startingAfterTotalHours = api.World.Calendar.TotalHours + 0.5f;

            charcoalPitBlock = api.World.BlockAccessor.GetBlock(pos);

            // To popuplate the smokeLocations
            FindHoleInPit();
        }

        private void OnClientTick(float dt)
        {
            BlockPos pos = new BlockPos();
            foreach (var val in smokeLocations)
            {
                if (api.World.Rand.NextDouble() < 0.2f && charcoalPitBlock.ParticleProperties.Length > 0)
                {
                    pos.Set(val.Key.X, val.Value + 1, val.Key.Z);

                    Block upblock = api.World.BlockAccessor.GetBlock(pos);
                    AdvancedParticleProperties particles = charcoalPitBlock.ParticleProperties[0];
                    particles.basePos = BlockEntityFire.RandomBlockPos(api.World.BlockAccessor, pos, upblock, BlockFacing.UP);

                    particles.Quantity.avg = 1;
                    api.World.SpawnParticles(particles);
                    particles.Quantity.avg = 0;
                }
            }
        }

        private void OnTick(float dt)
        {
            if (startingAfterTotalHours <= api.World.Calendar.TotalHours && state == 0)
            {
                finishedAfterTotalHours = api.World.Calendar.TotalHours + BurnHours;
                state = 1;
            }

            if (state == 0) return;


            BlockPos holePos = FindHoleInPit();

            if (holePos != null)
            {
                finishedAfterTotalHours = api.World.Calendar.TotalHours + BurnHours;

                BlockPos tmpPos = new BlockPos();
                BlockFacing firefacing = BlockFacing.UP;

                Block block = api.World.BlockAccessor.GetBlock(holePos);
                if (block.BlockId != 0 && block.BlockId != charcoalPitBlock.BlockId)
                {
                    foreach (BlockFacing facing in BlockFacing.ALLFACES)
                    {
                        tmpPos.Set(holePos).Add(facing);
                        if (api.World.BlockAccessor.GetBlock(tmpPos).BlockId == 0)
                        {
                            holePos.Set(tmpPos);
                            firefacing = facing;
                            break;
                        }
                    }
                }

                Block fireblock = api.World.GetBlock(new AssetLocation("fire"));
                api.World.BlockAccessor.SetBlock(fireblock.BlockId, holePos);

                BlockEntityFire befire = api.World.BlockAccessor.GetBlockEntity(holePos) as BlockEntityFire;
                befire?.Init(firefacing);

                return;
            }

            if (finishedAfterTotalHours <= api.World.Calendar.TotalHours)
            {
                ConvertPit();
            }
        }


        void ConvertPit()
        {
            Dictionary<BlockPos, Vec2i> quantityPerColumn = new Dictionary<BlockPos, Vec2i>();

            HashSet<BlockPos> visitedPositions = new HashSet<BlockPos>();
            Queue<BlockPos> bfsQueue = new Queue<BlockPos>();
            bfsQueue.Enqueue(pos);

            int maxHalfSize = 4;
            ushort firewoodBlockId = api.World.GetBlock(new AssetLocation("firewoodpile")).BlockId;

            Vec2i curQuantityAndYPos = new Vec2i();

            while (bfsQueue.Count > 0)
            {
                BlockPos bpos = bfsQueue.Dequeue();

                BlockPos bposGround = bpos.Copy();
                bposGround.Y = 0;

                if (quantityPerColumn.TryGetValue(bposGround, out curQuantityAndYPos))
                {
                    curQuantityAndYPos.Y = Math.Min(curQuantityAndYPos.Y, bpos.Y);
                } else
                {
                    curQuantityAndYPos = quantityPerColumn[bposGround] = new Vec2i(0, bpos.Y);
                }

                BlockEntityFirewoodPile be = api.World.BlockAccessor.GetBlockEntity(bpos) as BlockEntityFirewoodPile;
                if (be != null)
                {
                    curQuantityAndYPos.X += be.OwnStackSize;
                }
                api.World.BlockAccessor.SetBlock(0, bpos);

                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    BlockPos npos = bpos.AddCopy(facing);
                    Block nBlock = api.World.BlockAccessor.GetBlock(npos);

                    // Only traverse inside the firewood pile
                    if (nBlock.BlockId != firewoodBlockId) continue;

                    // Only traverse within a 8x8x8 block cube
                    bool inCube = Math.Abs(npos.X - pos.X) <= maxHalfSize && Math.Abs(npos.Y - pos.Y) <= maxHalfSize && Math.Abs(npos.Z - pos.Z) <= maxHalfSize;

                    if (inCube && !visitedPositions.Contains(npos))
                    {
                        bfsQueue.Enqueue(npos);
                        visitedPositions.Add(npos);
                    }
                }
            }


            BlockPos lpos = new BlockPos();
            foreach (var val in quantityPerColumn)
            {
                lpos.Set(val.Key.X, val.Value.Y, val.Key.Z);
                int logQuantity = val.Value.X;
                int charCoalQuantity = (int)(logQuantity * (0.125f + (float)api.World.Rand.NextDouble() / 8));

                while (charCoalQuantity > 0)
                {
                    Block charcoalBlock = api.World.GetBlock(new AssetLocation("charcoalpile-" + GameMath.Clamp(charCoalQuantity, 1, 8)));
                    api.World.BlockAccessor.SetBlock(charcoalBlock.BlockId, lpos);
                    charCoalQuantity -= 8;
                    lpos.Up();
                }
            }

            api.World.BlockAccessor.SetBlock(0, pos);
        }
        

        // Returns the block pos that is adjacent to a hole
        BlockPos FindHoleInPit()
        {
            smokeLocations.Clear();

            HashSet<BlockPos> visitedPositions = new HashSet<BlockPos>();
            Queue<BlockPos> bfsQueue = new Queue<BlockPos>();
            bfsQueue.Enqueue(pos);

            ushort firewoodBlockId = api.World.GetBlock(new AssetLocation("firewoodpile")).BlockId;
            ushort charcoalPitBlockId = api.World.GetBlock(new AssetLocation("charcoalpit")).BlockId;

            int maxHalfSize = 4;
            

            while (bfsQueue.Count > 0)
            {
                BlockPos bpos = bfsQueue.Dequeue();
                BlockPos bposGround = bpos.Copy();
                bposGround.Y = 0;

                int yMax = 0;
                smokeLocations.TryGetValue(bposGround, out yMax);
                smokeLocations[bposGround] = Math.Max(yMax, bpos.Y);


                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    BlockPos npos = bpos.AddCopy(facing);
                    Block nBlock = api.World.BlockAccessor.GetBlock(npos);

                    if (!nBlock.SideSolid[facing.GetOpposite().Index] && nBlock.BlockId != firewoodBlockId && nBlock.BlockId != charcoalPitBlockId)
                    {
                        return bpos;
                    }

                    // Only traverse inside the firewood pile
                    if (nBlock.BlockId != firewoodBlockId) continue;

                    // Only traverse within a 8x8x8 block cube
                    bool inCube = Math.Abs(npos.X - pos.X) <= maxHalfSize && Math.Abs(npos.Y - pos.Y) <= maxHalfSize && Math.Abs(npos.Z - pos.Z) <= maxHalfSize;

                    if (inCube && !visitedPositions.Contains(npos))
                    {
                        bfsQueue.Enqueue(npos);
                        visitedPositions.Add(npos);
                    }
                }
            }

            return null;
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);
            finishedAfterTotalHours = tree.GetDouble("finishedAfterTotalHours");
            state = tree.GetInt("state");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("finishedAfterTotalHours", finishedAfterTotalHours);
            tree.SetInt("state", state);
        }


        public override string GetBlockInfo(IPlayer forPlayer)
        {
            double minutesLeft = 60 * (startingAfterTotalHours - api.World.Calendar.TotalHours);
            if (minutesLeft <= 0) return null;

            return Lang.Get("{0} ingame minutes before the pile ignites,\nmake sure it's not exposed to air!", (int)minutesLeft); 
        }

    }
}
