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

        string startedByPlayerUid;


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);


            if (api.Side == EnumAppSide.Client)
            {
                RegisterGameTickListener(OnClientTick, 150);
            } else
            {
                RegisterGameTickListener(OnServerTick, 3000);
            }

            startingAfterTotalHours = api.World.Calendar.TotalHours + 0.5f;

            // To popuplate the smokeLocations
            FindHoleInPit();
        }

        private void OnClientTick(float dt)
        {
            BlockPos pos = new BlockPos();
            foreach (var val in smokeLocations)
            {
                if (Api.World.Rand.NextDouble() < 0.2f && Block.ParticleProperties.Length > 0)
                {
                    pos.Set(val.Key.X, val.Value + 1, val.Key.Z);

                    Block upblock = Api.World.BlockAccessor.GetBlock(pos);
                    AdvancedParticleProperties particles = Block.ParticleProperties[0];
                    particles.basePos = BlockEntityFire.RandomBlockPos(Api.World.BlockAccessor, pos, upblock, BlockFacing.UP);

                    particles.Quantity.avg = 1;
                    Api.World.SpawnParticles(particles);
                    particles.Quantity.avg = 0;
                }
            }
        }

        private void OnServerTick(float dt)
        {
            if (startingAfterTotalHours <= Api.World.Calendar.TotalHours && state == 0)
            {
                finishedAfterTotalHours = Api.World.Calendar.TotalHours + BurnHours;
                state = 1;
                MarkDirty(false);
            }

            if (state == 0) return;


            BlockPos holePos = FindHoleInPit();

            if (holePos != null)
            {
                finishedAfterTotalHours = Api.World.Calendar.TotalHours + BurnHours;

                BlockPos tmpPos = new BlockPos();
                BlockFacing firefacing = BlockFacing.UP;

                Block block = Api.World.BlockAccessor.GetBlock(holePos);
                if (block.BlockId != 0 && block.BlockId != Block.BlockId)
                {
                    foreach (BlockFacing facing in BlockFacing.ALLFACES)
                    {
                        tmpPos.Set(holePos).Add(facing);
                        if (Api.World.BlockAccessor.GetBlock(tmpPos).BlockId == 0)
                        {
                            holePos.Set(tmpPos);
                            firefacing = facing;
                            break;
                        }
                    }
                }

                Block fireblock = Api.World.GetBlock(new AssetLocation("fire"));
                Api.World.BlockAccessor.SetBlock(fireblock.BlockId, holePos);

                BlockEntityFire befire = Api.World.BlockAccessor.GetBlockEntity(holePos) as BlockEntityFire;
                befire?.Init(firefacing, startedByPlayerUid);

                return;
            }

            if (finishedAfterTotalHours <= Api.World.Calendar.TotalHours)
            {
                ConvertPit();
            }
        }


        void ConvertPit()
        {
            Dictionary<BlockPos, Vec2i> quantityPerColumn = new Dictionary<BlockPos, Vec2i>();

            HashSet<BlockPos> visitedPositions = new HashSet<BlockPos>();
            Queue<BlockPos> bfsQueue = new Queue<BlockPos>();
            bfsQueue.Enqueue(Pos);

            int maxHalfSize = 6;
            int firewoodBlockId = Api.World.GetBlock(new AssetLocation("firewoodpile")).BlockId;

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

                BlockEntityFirewoodPile be = Api.World.BlockAccessor.GetBlockEntity(bpos) as BlockEntityFirewoodPile;
                if (be != null)
                {
                    curQuantityAndYPos.X += be.OwnStackSize;
                }
                Api.World.BlockAccessor.SetBlock(0, bpos);

                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    BlockPos npos = bpos.AddCopy(facing);
                    Block nBlock = Api.World.BlockAccessor.GetBlock(npos);

                    // Only traverse inside the firewood pile
                    if (nBlock.BlockId != firewoodBlockId) continue;

                    // Only traverse within a 12x12x12 block cube
                    bool inCube = Math.Abs(npos.X - Pos.X) <= maxHalfSize && Math.Abs(npos.Y - Pos.Y) <= maxHalfSize && Math.Abs(npos.Z - Pos.Z) <= maxHalfSize;

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
                int charCoalQuantity = (int)(logQuantity * (0.125f + (float)Api.World.Rand.NextDouble() / 8));

                while (charCoalQuantity > 0)
                {
                    Block charcoalBlock = Api.World.GetBlock(new AssetLocation("charcoalpile-" + GameMath.Clamp(charCoalQuantity, 1, 8)));
                    Api.World.BlockAccessor.SetBlock(charcoalBlock.BlockId, lpos);
                    charCoalQuantity -= 8;
                    lpos.Up();
                }
            }

            Api.World.BlockAccessor.SetBlock(0, Pos);
        }

        internal void Init(IPlayer player)
        {
            startedByPlayerUid = player?.PlayerUID;
        }


        // Returns the block pos that is adjacent to a hole
        BlockPos FindHoleInPit()
        {
            smokeLocations.Clear();

            HashSet<BlockPos> visitedPositions = new HashSet<BlockPos>();
            Queue<BlockPos> bfsQueue = new Queue<BlockPos>();
            bfsQueue.Enqueue(Pos);

            int firewoodBlockId = Api.World.GetBlock(new AssetLocation("firewoodpile")).BlockId;
            int charcoalPitBlockId = Api.World.GetBlock(new AssetLocation("charcoalpit")).BlockId;

            int maxHalfSize = 6;
            

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
                    IWorldChunk chunk = Api.World.BlockAccessor.GetChunkAtBlockPos(npos);
                    if (chunk == null) continue; // Maybe at the endge of the loaded chunk

                    Block nBlock = chunk.GetLocalBlockAtBlockPos(Api.World, npos);

                    if (!nBlock.SideSolid[facing.GetOpposite().Index] && nBlock.BlockId != firewoodBlockId && nBlock.BlockId != charcoalPitBlockId)
                    {
                        return bpos;
                    }

                    // Only traverse inside the firewood pile
                    if (nBlock.BlockId != firewoodBlockId) continue;

                    // Only traverse within a 12x12x12 block cube
                    bool inCube = Math.Abs(npos.X - Pos.X) <= maxHalfSize && Math.Abs(npos.Y - Pos.Y) <= maxHalfSize && Math.Abs(npos.Z - Pos.Z) <= maxHalfSize;
                    
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
            int beforeState = state;
            base.FromTreeAtributes(tree, worldForResolving);

            finishedAfterTotalHours = tree.GetDouble("finishedAfterTotalHours");
            state = tree.GetInt("state");

            if (beforeState != state && Api?.Side == EnumAppSide.Client) FindHoleInPit();

            startedByPlayerUid = tree.GetString("startedByPlayerUid");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("finishedAfterTotalHours", finishedAfterTotalHours);
            tree.SetInt("state", state);

            if (startedByPlayerUid != null)
            {
                tree.SetString("startedByPlayerUid", startedByPlayerUid);
            }
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            double minutesLeft = 60 * (startingAfterTotalHours - Api.World.Calendar.TotalHours);
            if (minutesLeft <= 0) return;

            dsc.AppendLine(Lang.Get("{0} ingame minutes before the pile ignites,\nmake sure it's not exposed to air!", (int)minutesLeft)); 
        }

    }
}
