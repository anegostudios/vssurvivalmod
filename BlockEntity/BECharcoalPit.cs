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
    public enum EnumCharcoalPitState
    {
        Warmup,
        Sealed,
        Unsealed
    }

    public class BlockEntityCharcoalPit : BlockEntity
    {
        protected ActionBoolReturn<BlockPos, BlockPos, BlockFacing, IWorldChunk> defaultCheckAction = null!;

        static float BurnHours = 18;

        // Key = horizontal location
        // Value = highest Y Position
        protected Dictionary<BlockPos, int> smokeLocations = new Dictionary<BlockPos, int>();

        protected double finishedAfterTotalHours;
        protected double startingAfterTotalHours;

        protected EnumCharcoalPitState state;

        protected string startedByPlayerUid = string.Empty;

        public bool Lit { get; protected set; }
        public virtual int MaxSize { get; set; } = 11;
        protected virtual float PitEfficiency { get; set; } = 1;

        public int charcoalPitId, fireBlockId;
        public int[] charcoalPileId = new int[8];


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            MaxSize = Block.Attributes?["maxSize"].AsInt(11) ?? 11;

            charcoalPitId = Block.BlockId;
            fireBlockId = Api.World.GetBlock(new AssetLocation("fire"))!.BlockId;
            for (int i = 0; i < 8; i++)
            {
                Block? block = Api.World.GetBlock(new AssetLocation("charcoalpile-" + (i + 1).ToString()));
                ArgumentNullException.ThrowIfNull(block);
                charcoalPileId[i] = block.BlockId;
            }

            defaultCheckAction = (bpos, npos, facing, chunk) =>
            {
                // Only traverse inside the firewood pile
                if (!BlockFirepit.IsFirewoodPile(Api.World, npos)) return false;

                return true;
            };

            if (api.Side == EnumAppSide.Client)
            {
                if (Lit) UpdateSmokeLocations();
                RegisterGameTickListener(OnClientTick, 150);
            }
            else
            {
                RegisterGameTickListener(OnServerTick, 3000);
            }
        }

        protected virtual void OnClientTick(float dt)
        {
            if (state != EnumCharcoalPitState.Sealed || (Block?.ParticleProperties is not AdvancedParticleProperties[] partProps)) return;

            BlockPos pos = new BlockPos(Pos.dimension);
            foreach (var val in smokeLocations)
            {
                if (Api.World.Rand.NextDouble() < 0.2f && partProps.Length > 0)
                {
                    pos.Set(val.Key.X, val.Value + 1, val.Key.Z);

                    Block upblock = Api.World.BlockAccessor.GetBlock(pos);
                    AdvancedParticleProperties particles = partProps[0];
                    particles.basePos = BEBehaviorBurning.RandomBlockPos(Api.World.BlockAccessor, pos, upblock, BlockFacing.UP);

                    particles.Quantity.avg = 1;
                    Api.World.SpawnParticles(particles);
                    particles.Quantity.avg = 0;
                }
            }
        }

        protected virtual void OnServerTick(float dt)
        {
            if (!Lit) return;

            if (startingAfterTotalHours <= Api.World.Calendar.TotalHours && state == EnumCharcoalPitState.Warmup)
            {
                finishedAfterTotalHours = Api.World.Calendar.TotalHours + BurnHours;
                state = EnumCharcoalPitState.Sealed;
                MarkDirty(false);
            }

            if (state == EnumCharcoalPitState.Warmup) return;

            HashSet<BlockPos>? holes = FindHolesInPit();

            if (holes == null) return;

            var beforeState = state;
            if (holes.Count > 0)
            {
                state = EnumCharcoalPitState.Unsealed;
                finishedAfterTotalHours = Api.World.Calendar.TotalHours + BurnHours;
                float burnChance = Math.Clamp(1f - (0.1f * (holes.Count - 1)), 0.5f, 1f);

                foreach (BlockPos holePos in holes)
                {
                    BlockPos firePos = holePos.Copy();

                    Block block = Api.World.BlockAccessor.GetBlock(holePos);

                    var byEntity = Api.World.PlayerByUid(startedByPlayerUid)?.Entity ?? Api.World.NearestPlayer(Pos.X, Pos.InternalY, Pos.Z)?.Entity;
                    IIgnitable ign = block.GetInterface<IIgnitable>(Api.World, holePos);

                    if (byEntity != null && ign?.OnTryIgniteBlock(byEntity, holePos, 10) is EnumIgniteState.Ignitable or EnumIgniteState.IgniteNow)
                    {
                        if (Api.World.Rand.NextDouble() < burnChance)
                        {
                            EnumHandling useless = EnumHandling.PassThrough;
                            ign.OnTryIgniteBlockOver(byEntity, holePos, 10, ref useless);
                        }
                    }
                    else if (block.BlockId != 0 && block.BlockId != charcoalPitId)
                    {
                        foreach (BlockFacing facing in BlockFacing.ALLFACES)
                        {
                            facing.IterateThruFacingOffsets(firePos);  // This must be the first command in the loop, to ensure all facings will be properly looped through regardless of any 'continue;' statements
                            if (Api.World.BlockAccessor.GetBlock(firePos).BlockId == 0 && Api.World.Rand.NextDouble() < burnChance)
                            {
                                Api.World.BlockAccessor.SetBlock(fireBlockId, firePos);
                                BlockEntity befire = Api.World.BlockAccessor.GetBlockEntity(firePos);
                                befire?.GetBehavior<BEBehaviorBurning>()?.OnFirePlaced(facing, startedByPlayerUid);
                            }
                        }
                    }
                }

                MarkDirty();
                return;
            }
            else state = EnumCharcoalPitState.Sealed;

            if (beforeState != state) MarkDirty();

            if (finishedAfterTotalHours <= Api.World.Calendar.TotalHours)
            {
                ConvertPit();
            }
        }

        public void IgniteNow()
        {
            if (Lit) return;

            Lit = true;

            startingAfterTotalHours = this.Api.World.Calendar.TotalHours + 0.5f;
            MarkDirty(true);

            if (Api.Side == EnumAppSide.Client) UpdateSmokeLocations();
        }

        protected bool WalkPit(Action<BlockPos>? bAction, ActionBoolReturn<BlockPos, BlockPos, BlockFacing, IWorldChunk>? nAction)
        {
            HashSet<BlockPos> visitedPositions = new HashSet<BlockPos>();
            Queue<BlockPos> bfsQueue = new Queue<BlockPos>();
            bfsQueue.Enqueue(Pos.Copy());
            visitedPositions.Add(Pos.Copy());

            BlockPos minPos = Pos.Copy();
            BlockPos maxPos = Pos.Copy();

            IWorldChunk? chunk = null;
            BlockPos bpos = Pos;
            BlockPos npos = Pos;
            while (bfsQueue.Count > 0)
            {
                bpos = bfsQueue.Dequeue();
                npos = bpos.Copy();

                bAction?.Invoke(bpos); // Perform the first custom action before iterating

                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    facing.IterateThruFacingOffsets(npos);  // This must be the first command in the loop, to ensure all facings will be properly looped through regardless of any 'continue;' statements
                    chunk = Api.World.BlockAccessor.GetChunkAtBlockPos(npos);
                    if (chunk == null) return false;

                    if (nAction?.Invoke(bpos, npos, facing, chunk) == false) continue; // Second custom action and continue check

                    if (InCube(npos, ref minPos, ref maxPos) && !visitedPositions.Contains(npos))
                    {
                        bfsQueue.Enqueue(npos.Copy());
                        visitedPositions.Add(npos.Copy());
                    }
                }
            }

            return true;
        }

        protected virtual void ConvertPit()
        {
            Dictionary<BlockPos, Vec3i> quantityPerColumn = new Dictionary<BlockPos, Vec3i>();
            NatFloat firewoodEfficiency = NatFloat.createUniform(0.75f, 0.25f);
            BlockPos bposGround = Pos;
            float totalEfficiency = 0;
            int firewoodQuantity = 0;
            int bposY = 0;

            if (!WalkPit((bpos) =>
            {
                bposY = bpos.Y;
                bposGround = bpos.DownCopy(bposY); // Floor Y to 0

                if (quantityPerColumn.TryGetValue(bposGround, out Vec3i? curQuantityAndYMinMax))
                {
                    curQuantityAndYMinMax.Y = Math.Min(curQuantityAndYMinMax.Y, bposY);
                    curQuantityAndYMinMax.Z = Math.Max(curQuantityAndYMinMax.Z, bposY);
                }
                else
                {
                    curQuantityAndYMinMax = quantityPerColumn[bposGround] = new Vec3i(0, bposY, bposY);
                }

                firewoodQuantity = (Block as BlockCharcoalPit)?.GetFirewoodQuantity(Api.World, bpos, ref firewoodEfficiency) ?? 0;
                totalEfficiency = firewoodEfficiency.nextFloat(PitEfficiency);

                curQuantityAndYMinMax.X += Math.Clamp(GameMath.RoundRandom(Api.World.Rand, firewoodQuantity / 4f * totalEfficiency), 0, 8);
            }, defaultCheckAction)) return;

            BlockPos lpos = new BlockPos(Pos.dimension);
            int charcoalQuantity = 0;
            int numPileBlocks = charcoalPileId.Length;
            foreach (var val in quantityPerColumn)
            {
                lpos.Set(val.Key.X, val.Value.Y, val.Key.Z);
                charcoalQuantity = val.Value.X;

                while (lpos.Y <= val.Value.Z)
                {
                    if (BlockFirepit.IsFirewoodPile(Api.World, lpos) || lpos == Pos)  // test for the possibility someone had contiguous firewood both above and below a soil block for example
                    {
                        if (charcoalQuantity > 0)
                        {
                            Api.World.BlockAccessor.SetBlock(charcoalPileId[GameMath.Clamp(charcoalQuantity, 0, numPileBlocks) - 1], lpos);
                            charcoalQuantity -= 8;
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

        public void Init(IPlayer? player)
        {
            startedByPlayerUid = player?.PlayerUID ?? string.Empty;
        }


        // Returns the block pos that is adjacent to a hole
        protected virtual HashSet<BlockPos>? FindHolesInPit()
        {
            HashSet<BlockPos> holes = new HashSet<BlockPos>();
            Block nBlock = Block;
            bool containsNew = false;
            bool isFirewood = false;

            if (!WalkPit(null,
            (bpos, npos, facing, chunk) =>
            {
                isFirewood = BlockFirepit.IsFirewoodPile(Api.World, npos);

                containsNew = holes.Contains(npos);
                if (!holes.Contains(bpos) || !containsNew)
                {
                    nBlock = chunk.GetLocalBlockAtBlockPos(Api.World, npos);

                    if (!isFirewood && nBlock.BlockId != charcoalPitId)
                    {
                        if (IsCombustible(npos))
                        {
                            holes.Add(npos.Copy());
                            holes.Add(bpos.Copy());
                        }
                        else if (nBlock.GetLiquidBarrierHeightOnSide(facing.Opposite, npos) != 1) holes.Add(bpos.Copy());
                    }
                    else if (containsNew && nBlock.BlockId == charcoalPitId) holes.Add(bpos);
                }

                if (!isFirewood) return false; // Only traverse inside the firewood pile
                return true;
            })) return null;

            return holes;
        }

        protected virtual void UpdateSmokeLocations()
        {
            smokeLocations.Clear();

            if (state is not EnumCharcoalPitState.Sealed) return;

            WalkPit((bpos) =>
            {
                BlockPos bposGround = bpos.DownCopy(bpos.Y); // Floor Y to 0

                smokeLocations.TryGetValue(bposGround, out int yMax);
                smokeLocations[bposGround] = Math.Max(yMax, bpos.Y);
            }, defaultCheckAction);
        }

        protected bool InCube(BlockPos npos, ref BlockPos minPos, ref BlockPos maxPos)
        {
            BlockPos nmin = minPos.Copy(), nmax = maxPos.Copy();
            
            if (npos.X < nmin.X) nmin.X = npos.X;
            else if (npos.X > nmax.X) nmax.X = npos.X;

            if (npos.Y < nmin.Y) nmin.Y = npos.Y;
            else if (npos.Y > nmax.Y) nmax.Y = npos.Y;

            if (npos.Z < nmin.Z) nmin.Z = npos.Z;
            else if (npos.Z > nmax.Z) nmax.Z = npos.Z;

            // Only traverse within maxSize range
            if (nmax.X - nmin.X + 1 <= MaxSize && nmax.Y - nmin.Y + 1 <= MaxSize && nmax.Z - nmin.Z + 1 <= MaxSize)
            {
                minPos = nmin.Copy();
                maxPos = nmax.Copy();
                return true;
            }

            return false;
        }

        protected bool IsCombustible(BlockPos pos)
        {
            Block block = Api.World.BlockAccessor.GetBlock(pos);

            var combustibleProps = block.GetCombustibleProperties(Api.World, null, pos);
            if (combustibleProps != null) return combustibleProps.BurnDuration > 0;

            if (block.GetInterface<ICombustible>(Api.World, pos) is ICombustible bic) return bic.GetBurnDuration(Api.World, pos) > 0;

            return false;
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            EnumCharcoalPitState beforeState = state;
            bool beforeLit = Lit;
            base.FromTreeAttributes(tree, worldForResolving);

            finishedAfterTotalHours = tree.GetDouble("finishedAfterTotalHours");
            startingAfterTotalHours = tree.GetDouble("startingAfterTotalHours");

            state = (EnumCharcoalPitState)tree.GetInt("state");

            startedByPlayerUid = tree.GetString("startedByPlayerUid");
            Lit = tree.GetBool("lit", true);

            if ((beforeState != state || beforeLit != Lit) && Api?.Side == EnumAppSide.Client)
            {
                UpdateSmokeLocations();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("finishedAfterTotalHours", finishedAfterTotalHours);
            tree.SetDouble("startingAfterTotalHours", startingAfterTotalHours);
            tree.SetInt("state", (int)state);
            tree.SetBool("lit", Lit);

            if (startedByPlayerUid != null)
            {
                tree.SetString("startedByPlayerUid", startedByPlayerUid);
            }
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            double minutesLeft = 60 * (startingAfterTotalHours - Api.World.Calendar.TotalHours);

            if (Lit)
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
            if (!Lit)
            {
                MeshData litCharcoalMesh = ObjectCacheUtil.GetOrCreate(Api, "litCharcoalMesh", () =>
                {

                    ITesselatorAPI tess = ((ICoreClientAPI)Api).Tesselator;
                    tess.TesselateShape(Block, API.Common.Shape.TryGet(Api, "shapes/block/wood/firepit/cold-normal.json"), out MeshData mesh);

                    return mesh;
                });

                mesher.AddMeshData(litCharcoalMesh);
                return true;
            }

            return false;
        }

    }
}
