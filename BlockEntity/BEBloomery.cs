using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityBloomery : BlockEntity, IHeatSource
    {
        ILoadedSound ambientSound;
        BloomeryContentsRenderer renderer;

        static SimpleParticleProperties breakSparks;
        static SimpleParticleProperties smallMetalSparks;
        static SimpleParticleProperties smoke;

        BlockFacing ownFacing;

        public AssetLocation FuelSoundLocation => new AssetLocation("sounds/block/charcoal");
        public AssetLocation OreSoundLocation => new AssetLocation("sounds/block/loosestone");

        public const int MinTemp = 1000;
        public const int MaxTemp = 1500;

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
            smallMetalSparks.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.QUADRATIC, -0.5f);
            smallMetalSparks.AddPos.Set(4 / 16.0, 3 / 16.0, 4 / 16.0);
            smallMetalSparks.ParticleModel = EnumParticleModel.Cube;
            smallMetalSparks.LifeLength = 0.04f;
            smallMetalSparks.MinQuantity = 1;
            smallMetalSparks.AddQuantity = 1;
            smallMetalSparks.MinSize = 0.2f;
            smallMetalSparks.MaxSize = 0.2f;
            smallMetalSparks.GravityEffect = 0f;


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

        public bool IsBurning => burning;


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            bloomeryInv.LateInitialize("bloomery-1", api);

            RegisterGameTickListener(OnGameTick, 100);

            updateSoundState();

            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                capi.Event.RegisterRenderer(renderer = new BloomeryContentsRenderer(Pos, capi), EnumRenderStage.Opaque, "bloomery");

                UpdateRenderer();
            }

            ownFacing = BlockFacing.FromCode(api.World.BlockAccessor.GetBlock(Pos).LastCodePart());
        }

        private void UpdateRenderer()
        {
            // max = 8 voxels
            float fillLevel = Math.Min(14, FuelSlot.StackSize + (float)8f * OreSlot.StackSize / OreCapacity + OutSlot.StackSize);
            renderer.SetFillLevel(fillLevel);

            // Ease in in beginning and ease out on end
            double easinLevel = Math.Min(1, 24 * (Api.World.Calendar.TotalDays - burningStartTotalDays));
            double easeoutLevel = Math.Min(1, 24 * (burningUntilTotalDays - Api.World.Calendar.TotalDays));

            double glowLevel = Math.Max(0, Math.Min(easinLevel, easeoutLevel) * 250);

            renderer.glowLevel = burning ? (int)glowLevel : 0;
        }

        public void updateSoundState()
        {
            if (burning) startSound();
            else stopSound();
        }

        public void startSound()
        {
            if (ambientSound == null && Api?.Side == EnumAppSide.Client)
            {
                ambientSound = (Api as ICoreClientAPI).World.LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/environment/fire.ogg"),
                    ShouldLoop = true,
                    Position = Pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                    DisposeOnFinish = false,
                    Volume = 0.3f,
                    Range = 8
                });

                ambientSound.Start();
            }
        }

        public void stopSound()
        {
            if (ambientSound != null)
            {
                ambientSound.Stop();
                ambientSound.Dispose();
                ambientSound = null;
            }
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
                DoSmelt();
            }
        }


        private void DoSmelt()
        {
            if (OreStack.Collectible.CombustibleProps is not CombustibleProperties combustProps) return;

            int q = OreStack.StackSize / combustProps.SmeltedRatio;

            if (OreStack.ItemAttributes?.IsTrue("mergeUnitsInBloomery") == true)
            {
                OutSlot.Itemstack = combustProps.SmeltedStack.ResolvedItemstack.Clone();
                OutStack.StackSize = 1;

                float qf = (float)OreStack.StackSize / combustProps.SmeltedRatio;
                OutStack.Attributes.SetFloat("units", qf*100);

            } else
            {
                OutSlot.Itemstack = combustProps.SmeltedStack.ResolvedItemstack.Clone();
                OutStack.StackSize *= q;
            }

            OutStack.Collectible.SetTemperature(Api.World, OutSlot.Itemstack, 900, true);

            FuelSlot.Itemstack = null;
            
            OreStack.StackSize -= q * combustProps.SmeltedRatio;
            if (OreSlot.StackSize == 0) OreSlot.Itemstack = null;

            burning = false;
            burningUntilTotalDays = 0;

            MarkDirty();
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
                smallMetalSparks.VertexFlags = (byte)renderer.glowLevel;
                smallMetalSparks.MinVelocity = new Vec3f(-0.5f - dir.X, -0.3f, -0.5f - dir.Z);
                smallMetalSparks.AddVelocity = new Vec3f(1f - dir.X, 0.6f, 1f - dir.Z);                
                
                Api.World.SpawnParticles(smallMetalSparks, null);
            }
        }

        public bool CanAdd(ItemStack stack, int quantity = 1)
        {
            if (IsBurning) return false;
            if (OutSlot.StackSize > 0) return false;
            if (stack?.Collectible.CombustibleProps is not CombustibleProperties combustProps) return false;

            if (combustProps.SmeltedStack != null && combustProps.MeltingPoint < MaxTemp && combustProps.MeltingPoint >= MinTemp)
            {
                if (OreSlot.StackSize + quantity > OreCapacity) return false;
                if (!OreSlot.Empty && !OreStack.Equals(Api.World, stack, GlobalConstants.IgnoredStackAttributes)) return false;
                return true;
            }

            if (combustProps.BurnTemperature >= 1200 && combustProps.BurnDuration > 30)
            {
                if (FuelSlot.StackSize + quantity > FuelCapacity) return false;
                if (!FuelSlot.Empty && !FuelStack.Equals(Api.World, stack, GlobalConstants.IgnoredStackAttributes)) return false;

                return true;
            }
            
            return false;
        }


        public bool TryAdd(IPlayer byPlayer , int quantity = 1)
        {
            ItemSlot sourceSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (IsBurning) return false;
            if (OutSlot.StackSize > 0) return false;
            if (sourceSlot.Itemstack == null) return false;

            if (sourceSlot.Itemstack.Collectible.CombustibleProps is not CombustibleProperties combustProps) return true;

            if (combustProps.SmeltedStack != null && combustProps.MeltingPoint < MaxTemp && combustProps.MeltingPoint >= MinTemp) 
            {
                if (sourceSlot.TryPutInto(Api.World, OreSlot, Math.Min(OreCapacity - OreSlot.StackSize, quantity)) > 0)
                {
                    MarkDirty();
                    Api.World.PlaySoundAt(OreSoundLocation, Pos, 0, byPlayer);
                    return true;
                }

                return false;
            }

            if (combustProps.BurnTemperature >= 1200 && combustProps.BurnDuration > 30)
            {
                int maxRequired = (int)Math.Ceiling((float)OreSlot.StackSize / Ore2FuelRatio); 

                if (sourceSlot.TryPutInto(Api.World, FuelSlot, Math.Min(maxRequired - FuelSlot.StackSize, quantity)) > 0)
                {
                    MarkDirty();
                    Api.World.PlaySoundAt(FuelSoundLocation, Pos, 0, byPlayer);
                    return true;
                }

                return false;
            }

            return false;
        }


        public bool TryIgnite()
        {
            if (!CanIgnite() || burning) return false;
            if (!Api.World.BlockAccessor.GetBlock(Pos.UpCopy()).Code.Path.Contains("bloomerychimney")) return false;

            burning = true;
            burningUntilTotalDays = Api.World.Calendar.TotalDays + 10 / 24.0;
            burningStartTotalDays = Api.World.Calendar.TotalDays;
            MarkDirty();
            updateSoundState();
            return true;
        }


        public bool CanIgnite()
        {
            return !burning && FuelSlot.StackSize > 0 && OreSlot.StackSize > 0 && (float)OreSlot.StackSize / FuelSlot.StackSize <= Ore2FuelRatio;
        }


        public override void OnBlockBroken(IPlayer byPlayer = null)
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
            renderer?.Dispose();
            ambientSound?.Dispose();
        }
    
        public override void OnBlockRemoved()
        {
            renderer?.Dispose();
            ambientSound?.Dispose();
            base.OnBlockRemoved();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            bloomeryInv.FromTreeAttributes(tree);
            burning = tree.GetInt("burning") > 0;
            burningUntilTotalDays = tree.GetDouble("burningUntilTotalDays");
            burningStartTotalDays = tree.GetDouble("burningStartTotalDays");

            updateSoundState();
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
            if (Api.World.EntityDebugMode && forPlayer?.WorldData?.CurrentGameMode == EnumGameMode.Creative)
            {
                dsc.AppendLine(string.Format("Burning: {3}, Current total days: {0}, BurningStart total days: {1}, BurningUntil total days: {2}", Api.World.Calendar.TotalDays, burningStartTotalDays, burningUntilTotalDays, burning));
            }

            for (int i = 0; i < 3; i++)
            {
                ItemStack stack = bloomeryInv[i].Itemstack;
                if (stack != null)
                {
                    if (dsc.Length == 0) dsc.AppendLine(Lang.Get("Contents:"));
                    dsc.AppendLine("  " + stack.StackSize + "x " + stack.GetName());
                }
            }

            base.GetBlockInfo(forPlayer, dsc);
        }

        public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
        {
            return IsBurning ? 7 : 0;
        }

        ItemSlot FuelSlot => bloomeryInv[0];
        ItemSlot OreSlot => bloomeryInv[1];
        ItemSlot OutSlot => bloomeryInv[2];

        ItemStack FuelStack => FuelSlot.Itemstack;
        ItemStack OreStack => OreSlot.Itemstack;
        ItemStack OutStack => OutSlot.Itemstack;

        const int FuelCapacity = 6;
        int OreCapacity => Ore2FuelRatio * FuelCapacity;
        int Ore2FuelRatio
        {
            get
            {
                int ratio = OreStack?.Collectible.CombustibleProps?.SmeltedRatio ?? 1;
                return OreStack?.ItemAttributes?["bloomeryFuelRatio"].AsInt(ratio) ?? ratio;
            }
        }

    }
}

