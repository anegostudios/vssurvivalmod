using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockEntityBloomery : BlockEntity
    {
        ILoadedSound ambientSound;
        BloomeryContentsRenderer renderer;

        static SimpleParticleProperties breakSparks;
        static SimpleParticleProperties smallMetalSparks;
        static SimpleParticleProperties smoke;

        BlockFacing ownFacing;

        static BlockEntityBloomery() {
            smallMetalSparks = new SimpleParticleProperties(
                2, 5,
                ColorUtil.ToRgba(255, 255, 233, 83),
                new Vec3d(), new Vec3d(),
                new Vec3f(-3f, 8f, -3f),
                new Vec3f(3f, 12f, 3f),
                0.1f,
                1f,
                0.25f, 0.25f,
                EnumParticleModel.Quad
            );
            smallMetalSparks.WithTerrainCollision = false;
            smallMetalSparks.VertexFlags = 128;
            smallMetalSparks.AddPos.Set(1 / 16f, 0, 1 / 16f);
            smallMetalSparks.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.05f);

            breakSparks = new SimpleParticleProperties(
                40, 80,
                ColorUtil.ToRgba(255, 255, 233, 83),
                new Vec3d(), new Vec3d(),
                new Vec3f(-1f, 0.5f, -1f),
                new Vec3f(2f, 1.5f, 2f),
                0.5f,
                1f,
                0.25f, 0.25f
            );
            breakSparks.VertexFlags = 128;
            breakSparks.AddPos.Set(4 / 16f, 4 / 16f, 4 / 16f);
            breakSparks.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.25f);

            smoke = new SimpleParticleProperties(
                1, 1, ColorUtil.ToRgba(128, 110, 110, 110), new Vec3d(), new Vec3d(),
                new Vec3f(-0.2f, 0.3f, -0.2f), new Vec3f(0.2f, 0.3f, 0.2f), 2, 0, 0.5f, 1f, EnumParticleModel.Quad
            );
            smoke.SelfPropelled = true;
            smoke.OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -255);
            smoke.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, 2);
        }

        // Slot 0: Fuel
        // Slot 1: Ore
        // Slot 2: Output
        internal InventoryGeneric bloomeryInv;

        bool burning;
        double burningUntilTotalDays;
        double burningStartTotalDays;

        public BlockEntityBloomery()
        {
            bloomeryInv = new InventoryGeneric(3, "bloomery-1", null, null);
        }

        public bool IsBurning
        {
            get { return burning; }
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            bloomeryInv.LateInitialize("bloomery-1", api);

            RegisterGameTickListener(OnGameTick, 100);

            if (ambientSound == null && api.Side == EnumAppSide.Client)
            {
                ambientSound = ((IClientWorldAccessor)api.World).LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/environment/fire.ogg"),
                    ShouldLoop = true,
                    Position = Pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                    DisposeOnFinish = false,
                    Volume = 0.3f,
                    Range = 8
                });
                if (burning) ambientSound.Start();
            }

            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                capi.Event.RegisterRenderer(renderer = new BloomeryContentsRenderer(Pos, capi), EnumRenderStage.Opaque);

                UpdateRenderer();
            }

            ownFacing = BlockFacing.FromCode(api.World.BlockAccessor.GetBlock(Pos).LastCodePart());
        }

        private void UpdateRenderer()
        {
            // max = 8 voxels
            float fillLevel = Math.Min(8, FuelSlot.StackSize / 5f + (float)4f * OreSlot.StackSize / OreCapacity + OutSlot.StackSize);
            renderer.SetFillLevel(fillLevel);

            // Ease in in beginning and ease out on end
            double easinLevel = Math.Min(1, 24 * (Api.World.Calendar.TotalDays - burningStartTotalDays));
            double easeoutLevel = Math.Min(1, 24 * (burningUntilTotalDays - Api.World.Calendar.TotalDays));

            double glowLevel = Math.Max(0, Math.Min(easinLevel, easeoutLevel) * 128);

            renderer.glowLevel = burning ? (int)glowLevel : 0;
        }

        private void OnGameTick(float dt)
        {
            if (Api.Side == EnumAppSide.Client)
            {
                UpdateRenderer();

                if (burning) EmitParticles();
            }

            if (!burning) return;


            if (Api.Side == EnumAppSide.Server && burningUntilTotalDays < Api.World.Calendar.TotalDays)
            {
                FuelSlot.Itemstack = null;
                int q = OreStack.StackSize / OreStack.Collectible.CombustibleProps.SmeltedRatio;

                OutSlot.Itemstack = OreStack.Collectible.CombustibleProps.SmeltedStack.ResolvedItemstack.Clone();
                OutSlot.Itemstack.StackSize *= q;

                OreSlot.Itemstack.StackSize -= q * OreStack.Collectible.CombustibleProps.SmeltedRatio;
                if (OreSlot.StackSize == 0) OreSlot.Itemstack = null;

                burning = false;
                burningUntilTotalDays = 0;

                MarkDirty();
            }
        }

        private void EmitParticles()
        {
            if (Api.World.Rand.Next(5) > 0)
            {
                smoke.MinPos.Set(Pos.X + 0.5 - 2 / 16.0, Pos.Y + 1 + 10 / 16f, Pos.Z + 0.5 - 2 / 16.0);
                smoke.AddPos.Set(4 / 16.0, 0, 4 / 16.0);
                Api.World.SpawnParticles(smoke, null);
            }

            if (renderer.glowLevel > 80 && Api.World.Rand.Next(3) == 0)
            {
                Vec3f dir = ownFacing.Normalf;
                Vec3d particlePos = smallMetalSparks.MinPos;
                particlePos.Set(Pos.X + 0.5, Pos.Y, Pos.Z + 0.5);
                particlePos.Sub(dir.X * (6/16.0) + 2/16f, 0, dir.Z * (6 / 16.0) + 2/16f);
                
                smallMetalSparks.MinPos = particlePos;
                smallMetalSparks.AddPos.Set(4 / 16.0, 3 / 16.0, 4 / 16.0);
                smallMetalSparks.VertexFlags = (byte)renderer.glowLevel;
                smallMetalSparks.ParticleModel = EnumParticleModel.Cube;
                smallMetalSparks.LifeLength = 0.04f;
                smallMetalSparks.MinVelocity = new Vec3f(-0.5f - dir.X, -0.3f, -0.5f - dir.Z);
                smallMetalSparks.AddVelocity = new Vec3f(1f - dir.X, 0.6f, 1f - dir.Z);
                smallMetalSparks.MinQuantity = 1;
                smallMetalSparks.AddQuantity = 1;
                smallMetalSparks.MinSize = 0.2f;
                smallMetalSparks.MaxSize = 0.2f;
                smallMetalSparks.GravityEffect = 0f;
                
                smallMetalSparks.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.QUADRATIC, -0.5f);
                Api.World.SpawnParticles(smallMetalSparks, null);
            }
        }

        public bool CanAdd(ItemStack stack, int quantity = 1)
        {
            if (IsBurning) return false;
            if (OutSlot.StackSize > 0) return false;
            if (stack == null) return false;

            CollectibleObject collectible = stack.Collectible;

            if (collectible.CombustibleProps?.SmeltedStack != null && collectible.CombustibleProps.MeltingPoint < 1500 && collectible.CombustibleProps.MeltingPoint >= 1000)
            {
                int prevsize = stack.StackSize;
                
                if (OreSlot.StackSize + quantity > OreCapacity) return false;
                if (!OreSlot.Empty && !OreSlot.Itemstack.Equals(Api.World, stack, GlobalConstants.IgnoredStackAttributes)) return false;
                return true;
            }

            if (collectible.CombustibleProps?.BurnTemperature >= 1200 && collectible.CombustibleProps.BurnDuration > 30)
            {
                int prevsize = stack.StackSize;
                if (FuelSlot.StackSize + quantity > 6) return false;
                if (!FuelSlot.Empty && !FuelSlot.Itemstack.Equals(Api.World, stack, GlobalConstants.IgnoredStackAttributes)) return false;

                return true;
            }
            
            return false;
        }


        public bool TryAdd(ItemSlot sourceSlot, int quantity = 1)
        {
            if (IsBurning) return false;
            if (OutSlot.StackSize > 0) return true;
            if (sourceSlot.Itemstack == null) return true;

            CollectibleObject collectible = sourceSlot.Itemstack.Collectible;

            if (collectible.CombustibleProps?.SmeltedStack != null && collectible.CombustibleProps.MeltingPoint < 1500 && collectible.CombustibleProps.MeltingPoint >= 1000) 
            {
                int prevsize = sourceSlot.StackSize;
                if (OreSlot.StackSize >= OreCapacity) return true;

                int moveableq = Math.Min(OreCapacity - OreSlot.StackSize, quantity);

                sourceSlot.TryPutInto(Api.World, OreSlot, moveableq);
                MarkDirty();
                return prevsize != sourceSlot.StackSize;
            }

            if (collectible.CombustibleProps?.BurnTemperature >= 1200 && collectible.CombustibleProps.BurnDuration > 30)
            {
                int prevsize = sourceSlot.StackSize;
                if (FuelSlot.StackSize + quantity > 20) return true;

                int moveableq = Math.Min(20 - FuelSlot.StackSize, quantity);

                sourceSlot.TryPutInto(Api.World, FuelSlot, moveableq);
                MarkDirty();
                return prevsize != sourceSlot.StackSize;
            }

            return true;
        }


        public bool TryIgnite()
        {
            if (!CanIgnite() || burning) return false;
            if (!Api.World.BlockAccessor.GetBlock(Pos.UpCopy()).Code.Path.Contains("bloomerychimney")) return false;

            burning = true;
            burningUntilTotalDays = Api.World.Calendar.TotalDays + 10 / 24.0;
            burningStartTotalDays = Api.World.Calendar.TotalDays;
            MarkDirty();
            ambientSound?.Start();
            return true;
        }


        public bool CanIgnite()
        {
            return FuelSlot.StackSize > 0 && OreSlot.StackSize > 0 && (float)FuelSlot.StackSize / OreSlot.StackSize >= Coal2OreRatio;
        }


        public override void OnBlockBroken()
        {
            if (burning)
            {
                Vec3d dpos = Pos.ToVec3d().Add(0.5, 0.5, 0.5);
                bloomeryInv.DropSlots(dpos, new int[] { 0, 2 });


                breakSparks.MinPos = Pos.ToVec3d().AddCopy(dpos.X - 4 / 16f, dpos.Y - 4 / 16f, dpos.Z - 4 / 16f);
                Api.World.SpawnParticles(breakSparks, null);
            }
            else
            {
                bloomeryInv.DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            renderer?.Unregister();
        }
    
        public override void OnBlockRemoved()
        {
            renderer?.Unregister();
            base.OnBlockRemoved();
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            bloomeryInv.FromTreeAttributes(tree);
            burning = tree.GetInt("burning") > 0;
            burningUntilTotalDays = tree.GetDouble("burningUntilTotalDays");
            burningStartTotalDays = tree.GetDouble("burningStartTotalDays");

            if (burning) ambientSound?.Start();
            else ambientSound?.Stop();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            bloomeryInv.ToTreeAttributes(tree);
            tree.SetInt("burning", burning ? 1 : 0);
            tree.SetDouble("burningUntilTotalDays", burningUntilTotalDays);
            tree.SetDouble("burningStartTotalDays", burningStartTotalDays);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            for (int i = 0; i < 3; i++)
            {
                ItemStack stack = bloomeryInv[i].Itemstack;
                if (stack != null)
                {
                    if (dsc.Length == 0) dsc.AppendLine("Contents:");
                    dsc.AppendLine("  " + stack.StackSize + "x " + stack.GetName());
                }
            }

            base.GetBlockInfo(forPlayer, dsc);
        }


        ItemSlot FuelSlot { get { return bloomeryInv[0]; } }
        ItemSlot OreSlot { get { return bloomeryInv[1]; } }
        ItemSlot OutSlot { get { return bloomeryInv[2]; } }

        ItemStack FuelStack { get { return bloomeryInv[0].Itemstack; } }
        ItemStack OreStack { get { return bloomeryInv[1].Itemstack; } }
        ItemStack OutStack { get { return bloomeryInv[2].Itemstack; } }

        int OreCapacity {
            get
            {
                if (OreSlot.Itemstack?.Collectible.CombustibleProps == null) return 8;
                return OreSlot.Itemstack.Collectible.CombustibleProps.SmeltedRatio * 6;
            }
        }

        float Coal2OreRatio
        {
            get
            {
                if (OreSlot.Itemstack?.Collectible.CombustibleProps == null || FuelSlot.Itemstack?.Collectible.CombustibleProps == null) return 8;
                return 6f / OreCapacity;
            }
        }
    }
}

