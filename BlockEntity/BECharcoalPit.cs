using System;
using System.Collections.Generic;
using System.Text;
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

        int maxSize = 11;

        public int MaxPileSize { get { return maxSize; } set { maxSize = value; } }


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
                FindHolesInPit();
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

            List<BlockPos> holes = FindHolesInPit();

            if (holes?.Count > 0)
            {
                Block fireblock = Api.World.GetBlock(new AssetLocation("fire"));
                finishedAfterTotalHours = Api.World.Calendar.TotalHours + BurnHours;

                foreach (BlockPos holePos in holes)
                {
                    BlockPos firePos = holePos.Copy();

                    Block block = Api.World.BlockAccessor.GetBlock(holePos);
                    if (block.BlockId != 0 && block.BlockId != Block.BlockId)
                    {
                        foreach (BlockFacing facing in BlockFacing.ALLFACES)
                        {
                            facing.IterateThruFacingOffsets(firePos);  // This must be the first command in the loop, to ensure all facings will be properly looped through regardless of any 'continue;' statements
                            if (Api.World.BlockAccessor.GetBlock(firePos).BlockId == 0 && Api.World.Rand.NextDouble() > 0.9f)
                            {
                                Api.World.BlockAccessor.SetBlock(fireblock.BlockId, firePos);
                                BlockEntity befire = Api.World.BlockAccessor.GetBlockEntity(firePos);
                                befire?.GetBehavior<BEBehaviorBurning>()?.OnFirePlaced(facing, startedByPlayerUid);
                            }
                        }
                    }
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
            FindHolesInPit();
        }

        void ConvertPit()
        {
            Dictionary<BlockPos, Vec3i> quantityPerColumn = new Dictionary<BlockPos, Vec3i>();

            HashSet<BlockPos> visitedPositions = new HashSet<BlockPos>();
            Queue<BlockPos> bfsQueue = new Queue<BlockPos>();
            bfsQueue.Enqueue(Pos);

            BlockPos minPos = Pos.Copy(), maxPos = Pos.Copy();
            Vec3i curQuantityAndYMinMax;

            while (bfsQueue.Count > 0)
            {
                BlockPos bpos = bfsQueue.Dequeue();
                BlockPos npos = bpos.Copy();

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
                    facing.IterateThruFacingOffsets(npos);  // This must be the first command in the loop, to ensure all facings will be properly looped through regardless of any 'continue;' statements

                    // Only traverse inside the firewood pile
                    if (!BlockFirepit.IsFirewoodPile(Api.World, npos))
                    {
                        IWorldChunk chunk = Api.World.BlockAccessor.GetChunkAtBlockPos(npos);
                        if (chunk == null) return; // Maybe at the endge of the loaded chunk, in which case return before changing any blocks and it can be converted next tick instead
                        continue;
                    }

                    if (InCube(npos, ref minPos, ref maxPos) && !visitedPositions.Contains(npos))
                    {
                        bfsQueue.Enqueue(npos.Copy());
                        visitedPositions.Add(npos.Copy());
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
                    if (BlockFirepit.IsFirewoodPile(Api.World, lpos) || lpos == Pos)  // test for the possibility someone had contiguous firewood both above and below a soil block for example
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
        }

        internal void Init(IPlayer player)
        {
            startedByPlayerUid = player?.PlayerUID;
        }


        // Returns the block pos that is adjacent to a hole
        List<BlockPos> FindHolesInPit()
        {
            smokeLocations.Clear();

            List<BlockPos> holes = new List<BlockPos>();
            HashSet<BlockPos> visitedPositions = new HashSet<BlockPos>();
            Queue<BlockPos> bfsQueue = new Queue<BlockPos>();
            bfsQueue.Enqueue(Pos);

            int charcoalPitBlockId = Api.World.GetBlock(new AssetLocation("charcoalpit")).BlockId;

            BlockPos minPos = Pos.Copy(), maxPos = Pos.Copy();

            while (bfsQueue.Count > 0)
            {
                BlockPos bpos = bfsQueue.Dequeue();
                BlockPos npos = bpos.Copy();
                BlockPos bposGround = bpos.Copy();
                bposGround.Y = 0;

                int yMax;
                smokeLocations.TryGetValue(bposGround, out yMax);
                smokeLocations[bposGround] = Math.Max(yMax, bpos.Y);

                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    facing.IterateThruFacingOffsets(npos);  // This must be the first command in the loop, to ensure all facings will be properly looped through regardless of any 'continue;' statements
                    IWorldChunk chunk = Api.World.BlockAccessor.GetChunkAtBlockPos(npos);
                    if (chunk == null) return null;

                    Block nBlock = chunk.GetLocalBlockAtBlockPos(Api.World, npos);

                    bool solid = nBlock.GetLiquidBarrierHeightOnSide(facing.Opposite, npos) == 1 || nBlock.GetLiquidBarrierHeightOnSide(facing, bpos) == 1;
                    bool isFirewoodpile = BlockFirepit.IsFirewoodPile(Api.World, npos);

                    if (!isFirewoodpile && nBlock.BlockId != charcoalPitBlockId)
                    {
                        if (IsCombustible(npos)) holes.Add(npos.Copy());
                        else if (!solid) holes.Add(bpos.Copy());
                    }

                    // Only traverse inside the firewood pile
                    if (!isFirewoodpile) continue;

                    if (InCube(npos, ref minPos, ref maxPos) && !visitedPositions.Contains(npos))
                    {
                        bfsQueue.Enqueue(npos.Copy());
                        visitedPositions.Add(npos.Copy());
                    }
                }
            }

            return holes;
        }

        private bool InCube(BlockPos npos, ref BlockPos minPos, ref BlockPos maxPos)
        {
            BlockPos nmin = minPos.Copy(), nmax = maxPos.Copy();

            if (npos.X < minPos.X) nmin.X = npos.X;
            else if (npos.X > maxPos.X) nmax.X = npos.X;

            if (npos.Y < minPos.Y) nmin.Y = npos.Y;
            else if (npos.Y > maxPos.Y) nmax.Y = npos.Y;

            if (npos.Z < minPos.Z) nmin.Z = npos.Z;
            else if (npos.Z > maxPos.Z) nmax.Z = npos.Z;

            // Only traverse within maxSize range
            if (nmax.X - nmin.X + 1 <= maxSize && nmax.Y - nmin.Y + 1 <= maxSize && nmax.Z - nmin.Z + 1 <= maxSize)
            {
                minPos = nmin.Copy();
                maxPos = nmax.Copy();
                return true;
            }

            return false;
        }

        private bool IsCombustible(BlockPos pos)
        {
            Block block = Api.World.BlockAccessor.GetBlock(pos);
            if (block.CombustibleProps != null) return block.CombustibleProps.BurnDuration > 0;

            if (block.GetInterface<ICombustible>(be.Api.World, pos) is ICombustible bic)
            {
                return bic.GetBurnDuration(Api.World, pos) > 0;
            }

            return false;
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
                FindHolesInPit();
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
