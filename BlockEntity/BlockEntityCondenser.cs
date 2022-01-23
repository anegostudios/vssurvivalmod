using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityCondenser : BlockEntityLiquidContainer, ITerrainMeshPool
    {
        // inv[0] - water
        // inv[1] - bucket
        public override string InventoryClassName => "condenser";

        MeshData currentMesh;
        BlockCondenser ownBlock;

        MeshData bucketMesh;
        MeshData bucketMeshTmp;

        long lastReceivedDistillateTotalMs = -99999;
        ItemStack lastReceivedDistillate;

        Vec3f spoutPos;
        Vec3d steamposmin;
        Vec3d steamposmax;

        public BlockEntityCondenser()
        {
            inventory = new InventoryGeneric(2, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBlock = Block as BlockCondenser;
            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
                RegisterGameTickListener(clientTick, 200, Api.World.Rand.Next(50));

                if (!inventory[1].Empty && bucketMesh == null) genBucketMesh();
            }

            Matrixf mat = new Matrixf();
            mat
                .Translate(0.5f, 0, 0.5f)
                .RotateYDeg(Block.Shape.rotateY - 90)
                .Translate(-0.5f, 0, -0.5f)
            ;

            spoutPos = mat.TransformVector(new Vec4f(8f / 16f, 7.5f / 16f, 3.5f / 16f, 1)).XYZ;

            var steamposoffmin = mat.TransformVector(new Vec4f(6 / 16f, 13 / 16f, 9 / 16f, 1)).XYZ;
            var steamposoffmax = mat.TransformVector(new Vec4f(10 / 16f, 13 / 16f, 13 / 16f, 1)).XYZ;

            steamposmin = Pos.ToVec3d().Add(steamposoffmin);
            steamposmax = Pos.ToVec3d().Add(steamposoffmax);
        }

        private void clientTick(float dt)
        {
            long msPassed = Api.World.ElapsedMilliseconds - lastReceivedDistillateTotalMs;
            if (msPassed < 10000)
            {
                int color = lastReceivedDistillate.Collectible.GetRandomColor(Api as ICoreClientAPI, lastReceivedDistillate);
                var droppos = Pos.ToVec3d().Add(spoutPos);
                
                if (!inventory[0].Empty)
                {
                    Api.World.SpawnParticles(0.5f, ColorUtil.ToRgba(64, 255, 255, 255), steamposmin, steamposmax, new Vec3f(-0.1f, 0.2f, -0.1f), new Vec3f(0.1f, 0.3f, 0.1f), 1.5f, 0, 0.25f, EnumParticleModel.Quad);
                    Api.World.SpawnParticles(1f, color, droppos, droppos, new Vec3f(), new Vec3f(), 0.08f, 1, 0.15f, EnumParticleModel.Quad);
                } else
                {
                    Api.World.SpawnParticles(0.33f, color, droppos, droppos, new Vec3f(), new Vec3f(), 0.08f, 1, 0.15f, EnumParticleModel.Quad);
                }
            }
        }

        #region ITerrainMeshPool imp to get bucket mesh
        public void AddMeshData(MeshData data, int lodlevel = 1)
        {
            if (data == null) return;
            bucketMeshTmp.AddMeshData(data);
        }

        public void AddMeshData(MeshData data, ColorMapData colormapdata, int lodlevel = 1)
        {
            if (data == null) return;
            bucketMeshTmp.AddMeshData(data);
        }
        #endregion


        public bool OnBlockInteractStart(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot handslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ItemStack handStack = handslot.Itemstack;

            if (blockSel.SelectionBoxIndex < 2)
            {
                if (handslot.Empty && !inventory[1].Empty)
                {
                    AssetLocation sound = inventory[1].Itemstack?.Block?.Sounds?.Place;
                    Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);

                    if (!byPlayer.InventoryManager.TryGiveItemstack(inventory[1].Itemstack, true))
                    {
                        Api.World.SpawnItemEntity(inventory[1].Itemstack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                    inventory[1].Itemstack = null;
                    MarkDirty(true);
                    bucketMesh?.Clear();
                    return true;
                }

                else if (handStack != null && handStack.Collectible is BlockLiquidContainerTopOpened blockLiqCont && blockLiqCont.CapacityLitres >= 1 && blockLiqCont.CapacityLitres < 20 && inventory[1].Empty)
                {
                    bool moved = handslot.TryPutInto(Api.World, inventory[1], 1) > 0;
                    if (moved)
                    {
                        AssetLocation sound = inventory[1].Itemstack?.Block?.Sounds?.Place;
                        Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                        handslot.MarkDirty();
                        MarkDirty(true);
                        genBucketMesh();
                    }
                    return true;
                }
            }

            

            return false;
        }


        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
        }

        private void genBucketMesh()
        {
            if (inventory.Count < 2 || inventory[1].Empty) return;

            // Haxy, but works ¯\_(ツ)_/¯
            if (inventory[1].Itemstack.Block?.EntityClass != null && Api.Side == EnumAppSide.Client)
            {
                if (bucketMeshTmp == null)
                {
                    bucketMeshTmp = new MeshData(4, 3, false, true, true, true);

                    // Liquid mesh
                    bucketMeshTmp.CustomInts = new CustomMeshDataPartInt(bucketMeshTmp.FlagsCount);
                    bucketMeshTmp.CustomInts.Count = bucketMeshTmp.FlagsCount;
                    bucketMeshTmp.CustomInts.Values.Fill(0x4000000); // light foam only

                    bucketMeshTmp.CustomFloats = new CustomMeshDataPartFloat(bucketMeshTmp.FlagsCount * 2);
                    bucketMeshTmp.CustomFloats.Count = bucketMeshTmp.FlagsCount * 2;
                }
                bucketMeshTmp.Clear();
                var be = Api.ClassRegistry.CreateBlockEntity(inventory[1].Itemstack.Block.EntityClass);
                be.Pos = new BlockPos(0, 0, 0);
                be.Block = inventory[1].Itemstack.Block;
                be.Initialize(Api);
                be.OnBlockPlaced(inventory[1].Itemstack);
                be.OnTesselation(this, (Api as ICoreClientAPI).Tesselator);
                be.OnBlockRemoved();

                bucketMesh = bucketMeshTmp
                    .Clone()
                    .Translate(0, 0, 6 / 16f)
                    .Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, GameMath.PIHALF + Block.Shape.rotateY * GameMath.DEG2RAD, 0)
                ;
            }
        }

        float partialStackAccum;
        public bool ReceiveDistillate(ItemSlot sourceSlot, DistillationProps props)
        {
            if (sourceSlot.Empty)
            {
                lastReceivedDistillateTotalMs = -99999;
                return true;
            }
            if (inventory[1].Empty)
            {
                lastReceivedDistillateTotalMs = Api.World.ElapsedMilliseconds;
                lastReceivedDistillate = props.DistilledStack.ResolvedItemstack.Clone();
                return false;
            }

            ItemStack distilledStack = props.DistilledStack.ResolvedItemstack.Clone();

            lastReceivedDistillate = distilledStack.Clone();

            ItemStack bucketStack = inventory[1].Itemstack;
            BlockLiquidContainerTopOpened bucketBlock = bucketStack.Collectible as BlockLiquidContainerTopOpened;

            if (bucketBlock.IsEmpty(bucketStack))
            {
                if (Api.Side == EnumAppSide.Server)
                {
                    distilledStack.StackSize = 1;
                    bucketBlock.SetContent(bucketStack, distilledStack);
                }
            }
            else
            {
                ItemStack currentLiquidStack = bucketBlock.GetContent(bucketStack);
                if (!currentLiquidStack.Equals(Api.World, distilledStack, GlobalConstants.IgnoredStackAttributes))
                {
                    lastReceivedDistillateTotalMs = -99999;
                    return false;
                }

                if (Api.Side == EnumAppSide.Server)
                {
                    if (!inventory[0].Empty || Api.World.Rand.NextDouble() > 0.5f) // Missing coolant reduces distillation efficeny by 50%
                    {
                        currentLiquidStack.StackSize++;
                        bucketBlock.SetContent(bucketStack, currentLiquidStack);
                    }

                    if (!inventory[0].Empty && Api.World.Rand.NextDouble() < 0.5f)
                    {
                        inventory[0].TakeOut(1);
                    }
                }
            }


            float itemsToRemove = 1 / props.Ratio - partialStackAccum;
            int stackSize = (int)Math.Ceiling(itemsToRemove);

            partialStackAccum = itemsToRemove - stackSize;

            if (stackSize > 0)
            {
                sourceSlot.TakeOut(stackSize);
            }

            MarkDirty(true);
            lastReceivedDistillateTotalMs = Api.World.ElapsedMilliseconds;

            return true;   
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }


        internal MeshData GenMesh()
        {
            if (ownBlock == null) return null;

            MeshData mesh = ownBlock.GenMesh(Api as ICoreClientAPI, GetContent(), Pos);

            if (mesh.CustomInts != null)
            {
                for (int i = 0; i < mesh.CustomInts.Count; i++)
                {
                    mesh.CustomInts.Values[i] |= 1 << 27; // Disable water wavy
                    mesh.CustomInts.Values[i] |= 1 << 26; // Enabled weak foam
                }
            }

            return mesh;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(currentMesh);
            mesher.AddMeshData(bucketMesh);
            return true;
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (Api?.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }

            if (worldForResolving.Side == EnumAppSide.Client)
            {
                genBucketMesh();
            }

            partialStackAccum = tree.GetFloat("partialStackAccum");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetFloat("partialStackAccum", partialStackAccum);
        }


    }
}
