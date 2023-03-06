using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class MushroomProps
    {
        public float DieWhenTempBelow = 0;
        public bool DieAfterFruiting = true;
    }
    public class MyceliumSystem : ModSystem
    {
        ICoreAPI api;

        public static LCGRandom lcgrnd;
        public static NormalRandom rndn;
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            api.RegisterCommand("mycdebug", "", "", onCmd, Privilege.controlserver);
            api.Event.SaveGameLoaded += Event_SaveGameLoaded;

            this.api = api;
        }

        private void Event_SaveGameLoaded()
        {
            lcgrnd = new LCGRandom(api.World.Seed);
            rndn = new NormalRandom(api.World.Seed);
        }

        private void onCmd(IServerPlayer player, int groupId, CmdArgs args)
        {
            string cmd = args.PopWord();

            BlockPos pos = player.Entity.Pos.XYZ.AsBlockPos;

            switch (cmd)
            {
                case "regrow":
                    BlockEntityMycelium bemc = api.World.BlockAccessor.GetBlockEntity(pos.DownCopy()) as BlockEntityMycelium;
                    if (bemc == null)
                    {
                        player.SendMessage(groupId, "No mycelium below you", EnumChatType.Notification);
                        return;
                    }

                    bemc.Regrow();

                    break;
            }
        }
    }

    public class BlockEntityMycelium : BlockEntity
    {
        Vec3i[] grownMushroomOffsets = new Vec3i[0];
        
        double mushroomsGrownTotalDays = 0;
        double mushroomsDiedTotalDays = -999999;
        double mushroomsGrowingDays = 0;
        double lastUpdateTotalDays = 0;

        AssetLocation mushroomBlockCode;

        MushroomProps props;
        Block mushroomBlock;

        double fruitingDays = 20;
        double growingDays = 20;
        int growRange = 7;



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                int interval = 10000;
                RegisterGameTickListener(onServerTick, interval, -api.World.Rand.Next(interval));
                
                if (mushroomBlockCode != null && !setMushroomBlock(Api.World.GetBlock(mushroomBlockCode)))
                {
                    api.Logger.Error("Invalid mycelium mushroom type '{0}' at {1}. Will delete block entity.", mushroomBlockCode, Pos);
                    Api.Event.EnqueueMainThreadTask(() => Api.World.BlockAccessor.RemoveBlockEntity(Pos), "deletemyceliumBE");
                }
            }
        }

        private void onServerTick(float dt)
        {
            bool isFruiting = grownMushroomOffsets.Length > 0;
            if (isFruiting && props.DieWhenTempBelow > -99)
            {
                var conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, Api.World.Calendar.TotalDays);
                if (conds == null) return;
                if (props.DieWhenTempBelow > conds.Temperature)
                {
                    DestroyGrownMushrooms();
                    return;
                }
            }

            if (props.DieAfterFruiting && isFruiting && mushroomsGrownTotalDays + fruitingDays < Api.World.Calendar.TotalDays)
            {
                DestroyGrownMushrooms();
                return;
            }

            if (!isFruiting)
            {
                lastUpdateTotalDays = Math.Max(lastUpdateTotalDays, Api.World.Calendar.TotalDays - 50); // Don't check more than 50 days into the past

                while (Api.World.Calendar.TotalDays - lastUpdateTotalDays > 1)
                {
                    var conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, lastUpdateTotalDays + 0.5);
                    if (conds == null) return;

                    if (conds.Temperature > 5)
                    {
                        mushroomsGrowingDays += Api.World.Calendar.TotalDays - lastUpdateTotalDays;
                    }

                    lastUpdateTotalDays++;
                }

                if (mushroomsGrowingDays > growingDays)
                {
                    growMushrooms(Api.World.BlockAccessor, MyceliumSystem.rndn);
                    mushroomsGrowingDays = 0;
                }
            } else
            {
                if (Api.World.Calendar.TotalDays - lastUpdateTotalDays > 0.1)
                {
                    lastUpdateTotalDays = Api.World.Calendar.TotalDays;

                    for (int i = 0; i < grownMushroomOffsets.Length; i++)
                    {
                        var offset = grownMushroomOffsets[i];
                        var pos = Pos.AddCopy(offset);
                        var chunk = Api.World.BlockAccessor.GetChunkAtBlockPos(pos);
                        if (chunk == null) return;

                        if (!Api.World.BlockAccessor.GetBlock(pos).Code.Equals(mushroomBlockCode))
                        {
                            grownMushroomOffsets = grownMushroomOffsets.RemoveEntry(i);
                            i--;
                        }
                    }
                }
            }
        }

        public void Regrow()
        {
            DestroyGrownMushrooms();
            growMushrooms(Api.World.BlockAccessor, MyceliumSystem.rndn);
        }
        private void DestroyGrownMushrooms()
        {
            mushroomsDiedTotalDays = Api.World.Calendar.TotalDays;
            foreach (var offset in grownMushroomOffsets)
            {
                BlockPos pos = Pos.AddCopy(offset);
                var block = Api.World.BlockAccessor.GetBlock(pos);
                if (block.Variant["mushroom"] == mushroomBlock.Variant["mushroom"])
                {
                    Api.World.BlockAccessor.SetBlock(0, pos);
                }
            }

            grownMushroomOffsets = new Vec3i[0];
        }

        bool setMushroomBlock(Block block)
        {
            this.mushroomBlock = block;
            this.mushroomBlockCode = block.Code;

            if (Api != null)
            {
                if (block?.Attributes?["mushroomProps"].Exists != true) return false;

                if (block != null) props = block.Attributes["mushroomProps"].AsObject<MushroomProps>();
                MyceliumSystem.lcgrnd.InitPositionSeed(mushroomBlockCode.GetHashCode(), (int)Api.World.Calendar.GetHemisphere(Pos) + 5);

                fruitingDays = 20 + MyceliumSystem.lcgrnd.NextDouble() * 20;
                growingDays = 10 + MyceliumSystem.lcgrnd.NextDouble() * 10;
            }

            return true;
        }

        public void OnGenerated(IBlockAccessor blockAccessor, LCGRandom rnd, BlockMushroom block)
        {
            setMushroomBlock(block);

            MyceliumSystem.lcgrnd.InitPositionSeed(mushroomBlockCode.GetHashCode(), (int)(mushroomBlock as BlockMushroom).Api.World.Calendar.GetHemisphere(Pos));
            if (MyceliumSystem.lcgrnd.NextDouble() < 0.33)
            {
                mushroomsGrowingDays = MyceliumSystem.lcgrnd.NextDouble() * 10;
                return;
            }
            growMushrooms(blockAccessor, rnd);
        }

        private void growMushrooms(IBlockAccessor blockAccessor, IRandom rnd)
        {
            bool sidegrowing = mushroomBlock.Variant.ContainsKey("side");

            if (sidegrowing)
            {
                generateSideGrowingMushrooms(blockAccessor, rnd);
            }
            else
            {
                generateUpGrowingMushrooms(blockAccessor, rnd);
            }

            mushroomsGrownTotalDays = (mushroomBlock as BlockMushroom).Api.World.Calendar.TotalDays - rnd.NextDouble() * fruitingDays;
        }

        private void generateUpGrowingMushrooms(IBlockAccessor blockAccessor, IRandom rnd)
        {
            if (mushroomBlock == null) return;

            int cnt = 2 + rnd.NextInt(11);
            BlockPos pos = new BlockPos();
            int chunkSize = blockAccessor.ChunkSize;
            List<Vec3i> offsets = new List<Vec3i>();

            if (!isChunkAreaLoaded(blockAccessor, growRange)) return;

            while (cnt-- > 0)
            {
                int dx = growRange - rnd.NextInt(2* growRange + 1);
                int dz = growRange - rnd.NextInt(2* growRange + 1);

                pos.Set(Pos.X + dx, 0, Pos.Z + dz);

                var mapChunk = blockAccessor.GetMapChunkAtBlockPos(pos);
                if (mapChunk == null) continue;
                int lx = GameMath.Mod(pos.X, chunkSize);
                int lz = GameMath.Mod(pos.Z, chunkSize);

                pos.Y = mapChunk.WorldGenTerrainHeightMap[lz * blockAccessor.ChunkSize + lx] + 1;

                Block hereBlock = blockAccessor.GetBlock(pos);
                Block belowBlock = blockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);

                if (belowBlock.Fertility < 10 || hereBlock.LiquidCode != null) continue;

                if ((mushroomsGrownTotalDays == 0 && hereBlock.Replaceable >= 6000) || hereBlock.Id == 0)
                {
                    blockAccessor.SetBlock(mushroomBlock.Id, pos);
                    offsets.Add(new Vec3i(dx, pos.Y - Pos.Y, dz));
                }
            }

            this.grownMushroomOffsets = offsets.ToArray();
        }

        private bool isChunkAreaLoaded(IBlockAccessor blockAccessor, int growRange)
        {
            int chunksize = blockAccessor.ChunkSize;
            int mincx = (Pos.X - growRange) / chunksize;
            int maxcx = (Pos.X + growRange) / chunksize;

            int mincz = (Pos.Z - growRange) / chunksize;
            int maxcz = (Pos.Z + growRange) / chunksize;

            for (int cx = mincx; cx <= maxcx; cx++)
            {
                for (int cz = mincz; cz <= maxcz; cz++)
                {
                    if (blockAccessor.GetChunk(cx, Pos.Y / chunksize, cz) == null) return false;
                }
            }

            return true;
        }

        private void generateSideGrowingMushrooms(IBlockAccessor blockAccessor, IRandom rnd)
        {
            int cnt = 1 + rnd.NextInt(5);
            BlockPos mpos = new BlockPos();
            List<Vec3i> offsets = new List<Vec3i>();

            while (cnt-- > 0)
            {
                int dx = 0;
                int dy = rnd.NextInt(5) - 2;
                int dz = 0;

                mpos.Set(Pos.X + dx, Pos.Y + dy, Pos.Z + dz);

                var block = blockAccessor.GetBlock(mpos);
                if (!(block is BlockLog) || block.Variant["type"] == "resin") continue;

                BlockFacing facing = null;

                int rndside = rnd.NextInt(4);

                for (int j = 0; j < 4; j++)
                {
                    var f = BlockFacing.HORIZONTALS[(j + rndside) % 4];
                    mpos.Set(Pos.X + dx, Pos.Y + dy, Pos.Z + dz).Add(f);
                    var nblock = blockAccessor.GetBlock(mpos);
                    if (nblock.Id != 0) continue;
                    
                    facing = f.Opposite;
                    break;
                    
                }

                if (facing == null) continue;

                var mblock = blockAccessor.GetBlock(mushroomBlock.CodeWithVariant("side", facing.Code));
                blockAccessor.SetBlock(mblock.Id, mpos);
                offsets.Add(new Vec3i(mpos.X - Pos.X, mpos.Y - Pos.Y, mpos.Z - Pos.Z));
            }

            this.grownMushroomOffsets = offsets.ToArray();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            mushroomBlockCode = new AssetLocation(tree.GetString("mushroomBlockCode"));
            grownMushroomOffsets = tree.GetVec3is("grownMushroomOffsets");

            mushroomsGrownTotalDays = tree.GetDouble("mushromsGrownTotalDays");
            mushroomsDiedTotalDays = tree.GetDouble("mushroomsDiedTotalDays");
            lastUpdateTotalDays = tree.GetDouble("lastUpdateTotalDays");
            mushroomsGrowingDays = tree.GetDouble("mushroomsGrowingDays");

            setMushroomBlock(worldAccessForResolve.GetBlock(mushroomBlockCode));
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetString("mushroomBlockCode", mushroomBlockCode.ToShortString());
            tree.SetVec3is("grownMushroomOffsets", grownMushroomOffsets);
            tree.SetDouble("mushromsGrownTotalDays", mushroomsGrownTotalDays);
            tree.SetDouble("mushroomsDiedTotalDays", mushroomsDiedTotalDays);

            tree.SetDouble("lastUpdateTotalDays", lastUpdateTotalDays);
            tree.SetDouble("mushroomsGrowingDays", mushroomsGrowingDays);
        }

        
    }
}
