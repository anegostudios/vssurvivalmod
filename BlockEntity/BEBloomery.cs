using System;
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
            smallMetalSparks.glowLevel = 128;
            smallMetalSparks.addPos.Set(1 / 16f, 0, 1 / 16f);
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
            breakSparks.glowLevel = 128;
            breakSparks.addPos.Set(4 / 16f, 4 / 16f, 4 / 16f);
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
                    Position = pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                    DisposeOnFinish = false,
                    Volume = 0.3f,
                    Range = 8
                });
                if (burning) ambientSound.Start();
            }

            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                capi.Event.RegisterRenderer(renderer = new BloomeryContentsRenderer(pos, capi), EnumRenderStage.Opaque);

                UpdateRenderer();
            }

            ownFacing = BlockFacing.FromCode(api.World.BlockAccessor.GetBlock(pos).LastCodePart());
        }

        private void UpdateRenderer()
        {
            renderer.SetFillLevel((int)((FuelSlot.StackSize + OreSlot.StackSize + OutSlot.StackSize*2) / 40f * 14));

            // Ease in in beginning and ease out on end
            double easinLevel = Math.Min(1, 24 * (api.World.Calendar.TotalDays - burningStartTotalDays));
            double easeoutLevel = Math.Min(1, 24 * (burningUntilTotalDays - api.World.Calendar.TotalDays));

            double glowLevel = Math.Max(0, Math.Min(easinLevel, easeoutLevel) * 128);

            renderer.glowLevel = burning ? (int)glowLevel : 0;
        }

        private void OnGameTick(float dt)
        {
            if (api.Side == EnumAppSide.Client)
            {
                UpdateRenderer();

                if (burning) EmitParticles();
            }

            if (!burning) return;


            if (api.Side == EnumAppSide.Server && burningUntilTotalDays < api.World.Calendar.TotalDays)
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
            if (api.World.Rand.Next(5) > 0)
            {
                smoke.minPos.Set(pos.X + 0.5 - 2 / 16.0, pos.Y + 1 + 10 / 16f, pos.Z + 0.5 - 2 / 16.0);
                smoke.addPos.Set(4 / 16.0, 0, 4 / 16.0);
                api.World.SpawnParticles(smoke, null);
            }

            if (renderer.glowLevel > 80 && api.World.Rand.Next(3) == 0)
            {
                Vec3f dir = ownFacing.Normalf;
                Vec3d particlePos = smallMetalSparks.minPos;
                particlePos.Set(pos.X + 0.5, pos.Y, pos.Z + 0.5);
                particlePos.Sub(dir.X * (6/16.0) + 2/16f, 0, dir.Z * (6 / 16.0) + 2/16f);
                
                smallMetalSparks.minPos = particlePos;
                smallMetalSparks.addPos.Set(4 / 16.0, 3 / 16.0, 4 / 16.0);
                smallMetalSparks.glowLevel = (byte)renderer.glowLevel;
                smallMetalSparks.model = EnumParticleModel.Cube;
                smallMetalSparks.lifeLength = 0.04f;
                smallMetalSparks.minVelocity = new Vec3f(-0.5f - dir.X, -0.3f, -0.5f - dir.Z);
                smallMetalSparks.addVelocity = new Vec3f(1f - dir.X, 0.6f, 1f - dir.Z);
                smallMetalSparks.minQuantity = 1;
                smallMetalSparks.addQuantity = 1;
                smallMetalSparks.minSize = 0.2f;
                smallMetalSparks.maxSize = 0.2f;
                smallMetalSparks.gravityEffect = 0f;
                
                smallMetalSparks.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.QUADRATIC, -0.5f);
                api.World.SpawnParticles(smallMetalSparks, null);
            }
        }

        public bool TryAdd(IItemSlot sourceSlot)
        {
            if (OutSlot.StackSize > 0) return false;
            if (sourceSlot.Itemstack == null) return false;

            CollectibleObject collectible = sourceSlot.Itemstack.Collectible;

            if (collectible.CombustibleProps?.SmeltedStack != null && collectible.CombustibleProps.MeltingPoint < 1500) 
            {
                int prevsize = sourceSlot.StackSize;
                if (OreSlot.StackSize >= 20) return false;

                sourceSlot.TryPutInto(api.World, OreSlot);
                MarkDirty();
                return prevsize != sourceSlot.StackSize;
            }

            if (collectible.CombustibleProps?.BurnTemperature >= 1200 && collectible.CombustibleProps.BurnDuration > 30)
            {
                int prevsize = sourceSlot.StackSize;
                if (FuelSlot.StackSize >= 20) return false;

                sourceSlot.TryPutInto(api.World, FuelSlot);
                MarkDirty();
                return prevsize != sourceSlot.StackSize;
            }

            return false;
        }


        public bool TryIgnite()
        {
            if (!CanBurn() || burning) return false;
            if (!api.World.BlockAccessor.GetBlock(pos.UpCopy()).Code.Path.Contains("bloomerychimney")) return false;

            burning = true;
            burningUntilTotalDays = api.World.Calendar.TotalDays + 10 / 24.0;
            burningStartTotalDays = api.World.Calendar.TotalDays;
            MarkDirty();
            ambientSound?.Start();
            return true;
        }


        public bool CanBurn()
        {
            return FuelSlot.StackSize > 0 && OreSlot.StackSize > 0 && FuelSlot.StackSize >= OreSlot.StackSize;
        }

        
        public override void OnBlockRemoved()
        {
            if (renderer != null)
            {
                renderer.Unregister();
            }

            base.OnBlockRemoved();
        }

        public override void OnBlockBroken()
        {
            if (burning)
            {
                Vec3d dpos = pos.ToVec3d().Add(0.5, 0.5, 0.5);
                bloomeryInv.DropSlots(dpos, new int[] { 0, 2 });


                breakSparks.minPos = pos.ToVec3d().AddCopy(dpos.X - 4 / 16f, dpos.Y - 4 / 16f, dpos.Z - 4 / 16f);
                api.World.SpawnParticles(breakSparks, null);
            }
            else
            {
                bloomeryInv.DropAll(pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Unregister();
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

        public override string GetBlockInfo(IPlayer forPlayer)
        {
            StringBuilder str = new StringBuilder();
            for (int i = 0; i < 3; i++)
            {
                ItemStack stack = bloomeryInv[i].Itemstack;
                if (stack != null) str.AppendLine("  " + stack.StackSize + "x " + stack.GetName());
            }

            if (str.Length > 0) return "Contents:\n" + str;


            return base.GetBlockInfo(forPlayer);
        }


        ItemSlot FuelSlot { get { return bloomeryInv[0]; } }
        ItemSlot OreSlot { get { return bloomeryInv[1]; } }
        ItemSlot OutSlot { get { return bloomeryInv[2]; } }

        ItemStack FuelStack { get { return bloomeryInv[0].Itemstack; } }
        ItemStack OreStack { get { return bloomeryInv[1].Itemstack; } }
        ItemStack OutStack { get { return bloomeryInv[2].Itemstack; } }
    }
}
