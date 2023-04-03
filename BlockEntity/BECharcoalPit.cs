using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

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

        bool lit;

        public bool Lit => lit;


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

            if (Lit)
            {
                FindHoleInPit();
            }
        }

        private void OnClientTick(float dt)
        {
            if (!lit || Block?.ParticleProperties == null) return;

            BlockPos pos = new BlockPos();
            foreach (var val in smokeLocations)
            {
                if (Api.World.Rand.NextDouble() < 0.2f && Block.ParticleProperties.Length > 0)
                {
                    pos.Set(val.Key.X, val.Value + 1, val.Key.Z);

                    Block upblock = Api.World.BlockAccessor.GetBlock(pos);
                    AdvancedParticleProperties particles = Block.ParticleProperties[0];
                    particles.basePos = BEBehaviorBurning.RandomBlockPos(Api.World.BlockAccessor, pos, upblock, BlockFacing.UP);

                    particles.Quantity.avg = 1;
                    Api.World.SpawnParticles(particles);
                    particles.Quantity.avg = 0;
                }
            }
        }

        private void OnServerTick(float dt)
        {
            if (!lit) return;

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
                BlockFacing firefacing = null;

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

                if (firefacing != null)
                {
                    Block fireblock = Api.World.GetBlock(new AssetLocation("fire"));
                    Api.World.BlockAccessor.SetBlock(fireblock.BlockId, holePos);
                    BlockEntity befire = Api.World.BlockAccessor.GetBlockEntity(holePos);
                    befire?.GetBehavior<BEBehaviorBurning>()?.OnFirePlaced(firefacing, startedByPlayerUid);
                }

                return;
            }

            if (finishedAfterTotalHours <= Api.World.Calendar.TotalHours)
            {
                ConvertPit();
            }
        }

        public void IgniteNow()
        {
            if (lit) return;

            lit = true;

            startingAfterTotalHours = this.Api.World.Calendar.TotalHours + 0.5f;
            MarkDirty(true);

            // To popuplate the smokeLocations
            FindHoleInPit();
        }

        void ConvertPit()
        {
            Dictionary<BlockPos, Vec3i> quantityPerColumn = new Dictionary<BlockPos, Vec3i>();

            HashSet<BlockPos> visitedPositions = new HashSet<BlockPos>();
            Queue<BlockPos> bfsQueue = new Queue<BlockPos>();
            bfsQueue.Enqueue(Pos);

            int maxHalfSize = 6;
            Vec3i curQuantityAndYMinMax;

            while (bfsQueue.Count > 0)
            {
                BlockPos bpos = bfsQueue.Dequeue();

                BlockPos bposGround = bpos.Copy();
                bposGround.Y = 0;

                if (quantityPerColumn.TryGetValue(bposGround, out curQuantityAndYMinMax))
                {
                    curQuantityAndYMinMax.Y = Math.Min(curQuantityAndYMinMax.Y, bpos.Y);
                    curQuantityAndYMinMax.Z = Math.Max(curQuantityAndYMinMax.Z, bpos.Y);
                }
                else
                {
                    curQuantityAndYMinMax = quantityPerColumn[bposGround] = new Vec3i(0, bpos.Y, bpos.Y);
                }

                curQuantityAndYMinMax.X += BlockFirepit.GetFireWoodQuanity(Api.World, bpos);

                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    BlockPos npos = bpos.AddCopy(facing);

                    // Only traverse inside the firewood pile
                    if (!BlockFirepit.IsFirewoodPile(Api.World, npos))
                    {
                        IWorldChunk chunk = Api.World.BlockAccessor.GetChunkAtBlockPos(npos);
                        if (chunk == null) return; // Maybe at the endge of the loaded chunk, in which case return before changing any blocks and it can be converted next tick instead
                        continue;
                    }

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

                int maxY = val.Value.Z;
                while (lpos.Y <= maxY)
                {
                    if (BlockFirepit.IsFirewoodPile(Api.World, lpos))  // test for the possibility someone had contiguous firewood both above and below a soil block for example
                    {
                        if (charCoalQuantity > 0)
                        {
                            Block charcoalBlock = Api.World.GetBlock(new AssetLocation("charcoalpile-" + GameMath.Clamp(charCoalQuantity, 1, 8)));
                            Api.World.BlockAccessor.SetBlock(charcoalBlock.BlockId, lpos);
                            charCoalQuantity -= 8;
                        }
                        else
                        {
                            //Set any free blocks still in this column (y <= maxY) to air
                            Api.World.BlockAccessor.SetBlock(0, lpos);
                        }
                    }
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

            int charcoalPitBlockId = Api.World.GetBlock(new AssetLocation("charcoalpit")).BlockId;

            int maxHalfSize = 6;

            while (bfsQueue.Count > 0)
            {
                BlockPos bpos = bfsQueue.Dequeue();
                BlockPos bposGround = bpos.Copy();
                bposGround.Y = 0;

                int yMax;
                smokeLocations.TryGetValue(bposGround, out yMax);
                smokeLocations[bposGround] = Math.Max(yMax, bpos.Y);

                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    BlockPos npos = bpos.AddCopy(facing);
                    IWorldChunk chunk = Api.World.BlockAccessor.GetChunkAtBlockPos(npos);
                    if (chunk == null) return null;

                    Block nBlock = chunk.GetLocalBlockAtBlockPos(Api.World, npos);

                    bool solid = nBlock.SideSolid[facing.Opposite.Index] || (nBlock is BlockMicroBlock && (chunk.GetLocalBlockEntityAtBlockPos(npos) as BlockEntityMicroBlock).sideAlmostSolid[facing.Opposite.Index]);
                    bool isFirewoodpile = BlockFirepit.IsFirewoodPile(Api.World, npos);

                    if (!solid && !isFirewoodpile && nBlock.BlockId != charcoalPitBlockId)
                    {
                        return bpos;
                    }

                    // Only traverse inside the firewood pile
                    if (!isFirewoodpile) continue;

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


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            int beforeState = state;
            bool beforeLit = lit;
            base.FromTreeAttributes(tree, worldForResolving);

            finishedAfterTotalHours = tree.GetDouble("finishedAfterTotalHours");
            startingAfterTotalHours = tree.GetDouble("startingAfterTotalHours");

            state = tree.GetInt("state");

            startedByPlayerUid = tree.GetString("startedByPlayerUid");
            lit = tree.GetBool("lit", true);

            if ((beforeState != state || beforeLit != lit) && Api?.Side == EnumAppSide.Client)
            {
                FindHoleInPit();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("finishedAfterTotalHours", finishedAfterTotalHours);
            tree.SetDouble("startingAfterTotalHours", startingAfterTotalHours);
            tree.SetInt("state", state);
            tree.SetBool("lit", lit);

            if (startedByPlayerUid != null)
            {
                tree.SetString("startedByPlayerUid", startedByPlayerUid);
            }
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            double minutesLeft = 60 * (startingAfterTotalHours - Api.World.Calendar.TotalHours);

            if (lit)
            {
                if (minutesLeft <= 0)
                {
                    dsc.AppendLine(Lang.Get("Lit.")); 
                } else
                {
                    dsc.AppendLine(Lang.Get("lit-starting", (int)minutesLeft));
                }
            } else
            {
                dsc.AppendLine(Lang.Get("Unlit."));
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (!lit)
            {
                MeshData litCharcoalMesh = ObjectCacheUtil.GetOrCreate(Api, "litCharcoalMesh", () =>
                {
                    MeshData mesh;

                    ITesselatorAPI tess = ((ICoreClientAPI)Api).Tesselator;
                    tess.TesselateShape(Block, API.Common.Shape.TryGet(Api, "shapes/block/wood/firepit/cold-normal.json"), out mesh);

                    return mesh;
                });

                mesher.AddMeshData(litCharcoalMesh);
                return true;
            }

            return false;
        }

    }
}
