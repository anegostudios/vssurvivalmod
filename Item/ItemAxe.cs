using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;


namespace Vintagestory.GameContent
{
    public class ItemAxe : Item
    {
        static SimpleParticleProperties dustParticles = new SimpleParticleProperties()
        {
            MinPos = new Vec3d(),
            AddPos = new Vec3d(),
            MinQuantity = 0,
            AddQuantity = 3,
            Color = ColorUtil.ToRgba(100, 200, 200, 200),
            GravityEffect = 1f,
            WithTerrainCollision = true,
            ParticleModel = EnumParticleModel.Quad,
            LifeLength = 0.5f,
            MinVelocity = new Vec3f(-1, 2, -1),
            AddVelocity = new Vec3f(2, 0, 2),
            MinSize = 0.07f,
            MaxSize = 0.1f,
            WindAffected = true
        };

        static ItemAxe()
        {

            dustParticles.ParticleModel = EnumParticleModel.Quad;
            dustParticles.AddPos.Set(1, 1, 1);
            dustParticles.MinQuantity = 2;
            dustParticles.AddQuantity = 12;
            dustParticles.LifeLength = 4f;
            dustParticles.MinSize = 0.2f;
            dustParticles.MaxSize = 0.5f;
            dustParticles.MinVelocity.Set(-0.4f, -0.4f, -0.4f);
            dustParticles.AddVelocity.Set(0.8f, 1.2f, 0.8f);
            dustParticles.DieOnRainHeightmap = false;
            dustParticles.WindAffectednes = 0.5f;
        }

        public override float OnBlockBreaking(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            ITreeAttribute tempAttr = itemslot.Itemstack.TempAttributes;
            int posx = tempAttr.GetInt("lastposX", -1);
            int posy = tempAttr.GetInt("lastposY", -1);
            int posz = tempAttr.GetInt("lastposZ", -1);
            float treeResistance = tempAttr.GetFloat("treeResistance", 1);
            //int counter = tempAttr.GetInt("breakCounter", 0);

            BlockPos pos = blockSel.Position;

            if (pos.X != posx || pos.Y != posy || pos.Z != posz || counter % 30 == 0)
            {
                string bla;
                Stack<BlockPos> foundPositions = FindTree(player.Entity.World, pos, out bla);
                treeResistance = (float)Math.Max(1, Math.Sqrt(foundPositions.Count));

                tempAttr.SetFloat("treeResistance", treeResistance);
            }

            //tempAttr.SetInt("breakCounter", counter + 1);
            tempAttr.SetInt("lastposX", pos.X);
            tempAttr.SetInt("lastposY", pos.Y);
            tempAttr.SetInt("lastposZ", pos.Z);


            return base.OnBlockBreaking(player, blockSel, itemslot, remainingResistance, dt / treeResistance, counter);
        }

        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            ITreeAttribute tempAttr = itemslot.Itemstack.TempAttributes;
            //tempAttr.SetInt("breakCounter", 0);

            double windspeed = api.ModLoader.GetModSystem<WeatherSystemBase>()?.WeatherDataSlowAccess.GetWindSpeed(byEntity.SidedPos.XYZ) ?? 0;
            

            string treeType;
            Stack<BlockPos> foundPositions = FindTree(world, blockSel.Position, out treeType);
            
            Block leavesBranchyBlock = world.GetBlock(new AssetLocation("leavesbranchy-grown-" + treeType));
            Block leavesBlock = world.GetBlock(new AssetLocation("leaves-grown-" + treeType));
            

            if (foundPositions.Count == 0)
            {
                return base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);
            }

            bool damageable = DamagedBy != null && DamagedBy.Contains(EnumItemDamageSource.BlockBreaking);

            float leavesMul = 1;
            float leavesBranchyMul = 0.8f;
            int blocksbroken = 0;

            while (foundPositions.Count > 0) {
                BlockPos pos = foundPositions.Pop();
                blocksbroken++;

                Block block = world.BlockAccessor.GetBlock(pos);

                bool isLog = block.Code.Path.StartsWith("beehive-inlog-" + treeType) || block.Code.Path.StartsWith("log-resinharvested-" + treeType) || block.Code.Path.StartsWith("log-resin-" + treeType) || block.Code.Path.StartsWith("log-grown-"+treeType) || block.Code.Path.StartsWith("bamboo-grown-brown-segment") || block.Code.Path.StartsWith("bamboo-grown-green-segment");
                bool isBranchy = block == leavesBranchyBlock;
                bool isLeaves = block == leavesBlock || block.Code.Path == "bambooleaves-grown";

                world.BlockAccessor.BreakBlock(pos, byPlayer, isLeaves ? leavesMul : (isBranchy ? leavesBranchyMul : 1));

                if (world.Side == EnumAppSide.Client)
                {
                    dustParticles.Color = block.GetRandomColor(world.Api as ICoreClientAPI, pos, BlockFacing.UP);
                    dustParticles.Color |= 255 << 24;
                    dustParticles.MinPos.Set(pos.X, pos.Y, pos.Z);

                    if (block.BlockMaterial == EnumBlockMaterial.Leaves)
                    {
                        dustParticles.GravityEffect = (float)world.Rand.NextDouble() * 0.1f + 0.01f;
                        dustParticles.ParticleModel = EnumParticleModel.Quad;
                        dustParticles.MinVelocity.Set(-0.4f + 4 * (float)windspeed, -0.4f, -0.4f);
                        dustParticles.AddVelocity.Set(0.8f + 4 * (float)windspeed, 1.2f, 0.8f);

                    } else
                    {
                        dustParticles.GravityEffect = 0.8f;
                        dustParticles.ParticleModel = EnumParticleModel.Cube;
                        dustParticles.MinVelocity.Set(-0.4f + (float)windspeed, -0.4f, -0.4f);
                        dustParticles.AddVelocity.Set(0.8f + (float)windspeed, 1.2f, 0.8f);
                    }
                    

                    world.SpawnParticles(dustParticles);
                }


                if (damageable && isLog)
                {
                    DamageItem(world, byEntity, itemslot);
                }

                if (itemslot.Itemstack == null) return true;

                if (isLeaves && leavesMul > 0.03f) leavesMul *= 0.85f;
                if (isBranchy && leavesBranchyMul > 0.015f) leavesBranchyMul *= 0.6f;
            }

            if (blocksbroken > 35)
            {
                Vec3d pos = blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5);
                api.World.PlaySoundAt(new AssetLocation("sounds/effect/treefell"), pos.X, pos.Y, pos.Z, byPlayer, false, 32, GameMath.Clamp(blocksbroken / 100f, 0.25f, 1));
            }
            
            return true;
        }



        public Stack<BlockPos> FindTree(IWorldAccessor world, BlockPos startPos, out string treeType)
        {
            Queue<Vec4i> queue = new Queue<Vec4i>();
            HashSet<BlockPos> checkedPositions = new HashSet<BlockPos>();
            Stack<BlockPos> foundPositions = new Stack<BlockPos>();

            treeType = "";

            
            Block block = world.BlockAccessor.GetBlock(startPos);

            if (block.Code == null) return foundPositions;

            if (block.Code.Path.StartsWith("beehive-inlog-" + treeType) || block.Code.Path.StartsWith("log-resin") || block.Code.Path.StartsWith("log-grown") || block.Code.Path.StartsWith("bamboo-grown-brown-segment") || block.Code.Path.StartsWith("bamboo-grown-green-segment"))
            {
                treeType = block.FirstCodePart(2);

                queue.Enqueue(new Vec4i(startPos.X, startPos.Y, startPos.Z, 2));
                foundPositions.Push(startPos);
                checkedPositions.Add(startPos);
            }

            if (block is BlockFernTree)
            {
                treeType = "fern";
                queue.Enqueue(new Vec4i(startPos.X, startPos.Y, startPos.Z, 2));
                foundPositions.Push(startPos);
                checkedPositions.Add(startPos);
            }

            string logcode = "log-grown-" + treeType;
            string logcode2 = "log-resin-" + treeType;
            string logcode3 = "log-resinharvested-" + treeType;
            string leavescode = "leaves-grown-" + treeType;
            string leavesbranchycode = "leavesbranchy-grown-" + treeType;

            

            while (queue.Count > 0)
            {
                if (foundPositions.Count > 2000)
                {
                    break;
                }

                Vec4i pos = queue.Dequeue();

                for (int i = 0; i < Vec3i.DirectAndIndirectNeighbours.Length; i++)
                {
                    Vec3i facing = Vec3i.DirectAndIndirectNeighbours[i];
                    BlockPos neibPos = new BlockPos(pos.X + facing.X , pos.Y + facing.Y, pos.Z + facing.Z);
                    
                    float hordist = GameMath.Sqrt(neibPos.HorDistanceSqTo(startPos.X, startPos.Z));
                    float vertdist = (neibPos.Y - startPos.Y);

                    // "only breaks blocks inside an upside down square base pyramid"
                    if (hordist - 1 >= 2 * vertdist) continue;
                    if (checkedPositions.Contains(neibPos)) continue;

                    block = world.BlockAccessor.GetBlock(neibPos);
                    if (block.Code == null) continue;

                    if ((treeType == "fern" && block is BlockFernTree) || block.Code.Path.StartsWith(logcode) || block.Code.Path.StartsWith(logcode2) || block.Code.Path.StartsWith(logcode3) || block.Code.Path.StartsWith("bamboo-grown-brown-segment") || block.Code.Path.StartsWith("bamboo-grown-green-segment"))
                    {
                        if (pos.W < 2) continue;

                        foundPositions.Push(neibPos.Copy());
                        queue.Enqueue(new Vec4i(neibPos.X, neibPos.Y, neibPos.Z, 2));
                    } else if (block.Code.Path.StartsWith(leavesbranchycode))
                    {
                        if (pos.W < 1) continue;

                        foundPositions.Push(neibPos.Copy());
                        queue.Enqueue(new Vec4i(neibPos.X, neibPos.Y, neibPos.Z, 1));
                    } else if (block.Code.Path.StartsWith(leavescode) || block.Code.Path == "bambooleaves-grown")
                    {
                        foundPositions.Push(neibPos.Copy());
                        queue.Enqueue(new Vec4i(neibPos.X, neibPos.Y, neibPos.Z, 0));
                    }

                    checkedPositions.Add(neibPos);
                }
            }

            return foundPositions;
        }

    }
}
