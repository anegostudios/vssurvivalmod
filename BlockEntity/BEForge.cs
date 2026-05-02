using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable enable

namespace Vintagestory.GameContent
{

    public class BlockEntityForge : BlockEntityContainer, IHeatSource, ITemperatureSensitive, IBellowsAirReceiver
    {
        protected static SimpleParticleProperties? smallMetalSparks;
        protected static SimpleParticleProperties? smokeQuads;

        protected InventoryGeneric inv;
        protected ForgeContentsRenderer? renderer;
        protected float partialFuelConsumed;
        protected bool burning;
        protected double lastTickTotalHours;
        protected ILoadedSound? ambientSound;
        protected Vec3f blockRotRad = new Vec3f();
        protected bool clientSidePrevBurning;
        public ItemSlot FuelSlot => inv[1];
        public ItemSlot WorkItemSlot => inv[0];
        public ItemStack? WorkItemStack => inv[0].Itemstack;
        public float FuelLevel => FuelSlot.StackSize - partialFuelConsumed;
        public bool IsBurning => burning;
        public bool CanIgnite => !burning && FuelLevel > 0;
        public bool IsHot => (inv[1].Itemstack?.Collectible.GetTemperature(Api.World, inv[1].Itemstack) ?? 0) > 20;

        /// <summary>
        /// The number of coal items burnt per hour
        /// </summary>
        public virtual float BurnRate
        {
            get
            {
                return 0.5f * 1 / (FuelSlot.Itemstack?.ItemAttributes?["inForge"]["durationMul"].AsFloat() ?? 1f);
            }
        }


        public int MaxTemperature
        {
            get
            {
                return 700 + (FuelSlot.Itemstack?.ItemAttributes?["inForge"]["tempGainDeg"].AsInt() ?? 1);
            }
        }

        public float MaxExtraHeatRate = 1150 / 700f - 1;
        public float extraOxygenRate;
        public float extraOxygenRateRender;
        public float extraOxygenRateParticles;
        public BlockFacing blowDirection = BlockFacing.NORTH;

        public virtual float MeshAngleRad
        {
            get { return blockRotRad.Y; }
            set { blockRotRad.Y = value; }
        }


        public BlockEntityForge()
        {
            inv = new InventoryGeneric(2, null, null);
        }

        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "forge";

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            inv.LateInitialize("forge-" + Pos, api);

            if (api is ICoreClientAPI capi)
            {
                capi.Event.RegisterRenderer(renderer = new ForgeContentsRenderer(Block, Pos, capi, blockRotRad), EnumRenderStage.Opaque, "forge");

                renderer.SetContents(WorkItemStack, FuelLevel, burning, true, extraOxygenRateRender);
                RegisterGameTickListener(OnClientTick, 50);

                // Regen mesh on transform change
                api.Event.RegisterEventBusListener(onEventBusEvent, filterByEventName: "genjsontransform");
            }

            RegisterGameTickListener(OnCommonTick200ms, 200);


            if (smallMetalSparks == null)
            {
                smallMetalSparks = BlockEntityCoalPile.smallMetalSparks.Clone(api.World);
                smokeQuads = BlockEntityCoalPile.smokeParticles.Clone(api.World);
                smokeQuads.LifeLength = 0.5f;
                smokeQuads.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -16f);
            }
        }

        private void onEventBusEvent(string eventName, ref EnumHandling handling, IAttribute data)
        {
            renderer?.RegenMesh();
        }

        public void ToggleAmbientSounds(bool on)
        {
            if (Api.Side != EnumAppSide.Client) return;

            if (on)
            {
                if (!ambientSound?.IsPlaying ?? false)
                {
                    ambientSound = ((IClientWorldAccessor)Api.World).LoadSound(new SoundParams()
                    {
                        Location = new AssetLocation("sounds/effect/embers.ogg"),
                        ShouldLoop = true,
                        Position = Pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                        DisposeOnFinish = false,
                        Volume = 1
                    });

                    ambientSound.Start();
                }
            }
            else
            {
                if (ambientSound != null)
                {
                    ambientSound.Stop();
                    ambientSound.Dispose();
                    ambientSound = null;
                }
            }
        }


        public void BlowAirInto(IWorldAccessor world, BlockPos pos, float amount, BlockFacing direction)
        {
            BlowAirInto(amount, direction);
        }

        public void BlowAirInto(float amount, BlockFacing direction)
        {
            if (!burning) return;

            extraOxygenRate = Math.Min(MaxExtraHeatRate, extraOxygenRate + amount);
            extraOxygenRateRender = Math.Min(1, extraOxygenRateRender + amount);
            extraOxygenRateParticles = Math.Max(extraOxygenRateParticles, amount);

            blowDirection = direction;
        }


        protected void OnClientTick(float dt)
        {
            extraOxygenRateParticles = Math.Max(0, extraOxygenRateParticles - dt * 0.5f);
            if (extraOxygenRateRender > 0.15)
            {
                var dir = blowDirection.Normalf;

                smallMetalSparks!.MinPos.Set(Pos.X + 4 / 16f, Pos.Y + 14 / 16f, Pos.Z + 4 / 16f);
                smallMetalSparks.AddPos.Set(8 / 16f, 0.1f, 8 / 16f);
                smallMetalSparks.MinVelocity.Set(dir.X * 0.5f, 1f, dir.Z * 0.5f);
                smallMetalSparks.AddVelocity.Set(dir.X * 0.5f, 3f, dir.Z * 0.5f);

                smokeQuads!.MinPos.Set(Pos.X + 4 / 16f, Pos.Y + 14 / 16f, Pos.Z + 4 / 16f);
                smokeQuads.AddPos.Set(8 / 16f, 0.1f, 8 / 16f);
                smokeQuads.MinVelocity.Set(-0.125f, 0.25f, -0.125f);
                smokeQuads.AddVelocity.Set(0.25f, 0.5f, 0.25f);

                smallMetalSparks.MinQuantity = (float)Math.Pow(extraOxygenRateRender, 3) * 250;
                smallMetalSparks.AddQuantity = 0;
                Api.World.SpawnParticles(smallMetalSparks);
                smokeQuads.MinQuantity = extraOxygenRateRender;
                Api.World.SpawnParticles(smokeQuads);
            }


            if (Api?.Side == EnumAppSide.Client && clientSidePrevBurning != burning)
            {
                ToggleAmbientSounds(IsBurning);
                clientSidePrevBurning = IsBurning;
            }

            if (burning && Api?.World.Rand.NextDouble() < 0.13)
            {
                BlockEntityCoalPile.SpawnBurningCoalParticles(Api, Pos.ToVec3d().Add(4 / 16f, 14 / 16f, 4 / 16f), 8 / 16f, 8 / 16f);
            }

            renderer?.SetContents(WorkItemSlot.Itemstack, FuelLevel, burning, false, extraOxygenRateRender);
            extraOxygenRateRender *= 0.97f;
        }


        protected void OnCommonTick200ms(float dt)
        {
            if (burning)
            {
                double hoursPassed = Api.World.Calendar.TotalHours - lastTickTotalHours;
                if (hoursPassed < 0)
                {
                    lastTickTotalHours = Api.World.Calendar.TotalHours;
                    hoursPassed = 0;
                }

                var oxygenBurnMul = 1 + extraOxygenRate;

                partialFuelConsumed += (float)(BurnRate * hoursPassed * oxygenBurnMul);
                updateFuelLevel();

                if (FuelLevel <= 0)
                {
                    burning = false;
                    partialFuelConsumed = 0;
                }

                if (WorkItemStack != null && Api.Side == EnumAppSide.Server)
                {
                    float temp = WorkItemStack.Collectible.GetTemperature(Api.World, WorkItemStack);

                    if (temp < MaxTemperature * oxygenBurnMul)
                    {
                        float tempGain = (float)(hoursPassed * 1500 * oxygenBurnMul);
                        WorkItemStack.Collectible.SetTemperature(Api.World, WorkItemStack, Math.Min(MaxTemperature * oxygenBurnMul, temp + tempGain));
                        MarkDirty(false);
                    }
                }

                // after 0.1h all extra oxygen is gone
                extraOxygenRate *= Math.Max(0, 1 - ((float)hoursPassed * 30));
            }
            else
            {
                partialFuelConsumed = 0;

                if (Api.Side == EnumAppSide.Server && Api.World.Rand.NextDouble() < 0.05 && WorkItemStack != null)
                {
                    float temp = WorkItemStack.Collectible.GetTemperature(Api.World, WorkItemStack);
                    if (temp > 900) TryIgnite();
                }

                extraOxygenRate = 0;
            }

            lastTickTotalHours = Api.World.Calendar.TotalHours;
        }

        public void TryIgnite()
        {
            if (burning) return;
            burning = true;
            renderer?.SetContents(WorkItemStack, FuelLevel, burning, false, extraOxygenRate);
            lastTickTotalHours = Api.World.Calendar.TotalHours;
            MarkDirty();
        }

        public bool OnPlayerInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!byPlayer.Entity.Controls.ShiftKey)
            {
                if (WorkItemStack == null) return false;
                ItemStack splitStack = WorkItemStack.Clone();
                splitStack.StackSize = 1;
                WorkItemStack.StackSize--;

                if (WorkItemStack.StackSize == 0) WorkItemSlot.Itemstack = null;

                if (byPlayer.InventoryManager.TryGiveItemstack(splitStack))
                {
                    Api.ModLoader.GetModSystem<ModSystemSubTongsDurability>()?.OnItemPickedUp(byPlayer.Entity, splitStack);
                }
                else
                {
                    world.SpawnItemEntity(splitStack, Pos);
                }

                Api.World.Logger.Audit("{0} Took 1x{1} from Forge at {2}.",
                    byPlayer.PlayerName,
                    splitStack.Collectible.Code,
                    blockSel.Position
                );

                renderer?.SetContents(WorkItemStack, FuelLevel, burning, true, extraOxygenRateRender);
                MarkDirty();
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos, 0.4375, byPlayer, true);

                return true;

            }

            if (heldSlot.Itemstack is not ItemStack heldStack) return false;

            // Add fuel
            if (heldStack.Collectible.GetCombustibleProperties(world, heldStack, null) is { BurnTemperature: > 1000 })
            {
                if (FuelLevel > 4.5f) return false;
                bool isFuel = heldStack.Collectible is ItemCoal || heldStack.Collectible is ItemOre;
                if (heldSlot.TryPutInto(Api.World, FuelSlot) == 0) return false;

                if (isFuel) Api.World.PlaySoundAt(new AssetLocation("sounds/block/charcoal"), byPlayer, byPlayer, true, 16);
                (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                renderer?.SetContents(WorkItemStack, FuelLevel, burning, false, extraOxygenRateRender);
                MarkDirty();

                return true;
            }


            string firstCodePart = heldSlot.Itemstack.Collectible.FirstCodePart();
            bool forgableGeneric = heldSlot.Itemstack.Collectible.Attributes?.IsTrue("forgable") == true;

            // Add heatable item
            if (WorkItemStack == null && (firstCodePart == "ingot" || firstCodePart == "metalplate" || firstCodePart == "workitem" || forgableGeneric))
            {
                WorkItemSlot.Itemstack = heldSlot.TakeOut(1);

                heldSlot.MarkDirty();
                Api.World.Logger.Audit("{0} Put 1x{1} into Forge at {2}.",
                    byPlayer.PlayerName,
                    WorkItemStack!.Collectible.Code,
                    blockSel.Position
                );

                renderer?.SetContents(WorkItemStack, FuelLevel, burning, true, extraOxygenRateRender);
                MarkDirty();
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos, 0.4375, byPlayer, true);

                return true;
            }

            // Merge heatable item
            if (!forgableGeneric && WorkItemStack != null && WorkItemStack.Equals(Api.World, heldSlot.Itemstack, GlobalConstants.IgnoredStackAttributes) && WorkItemStack.StackSize < 4 && WorkItemStack.StackSize < WorkItemStack.Collectible.MaxStackSize)
            {
                float myTemp = WorkItemStack.Collectible.GetTemperature(Api.World, WorkItemStack);
                float histemp = heldSlot.Itemstack.Collectible.GetTemperature(Api.World, heldSlot.Itemstack);

                WorkItemStack.Collectible.SetTemperature(world, WorkItemStack, (myTemp * WorkItemStack.StackSize + histemp * 1) / (WorkItemStack.StackSize + 1));
                WorkItemStack.StackSize++;

                heldSlot.TakeOut(1);
                heldSlot.MarkDirty();
                Api.World.Logger.Audit("{0} Put 1x{1} into Forge at {2}.",
                    byPlayer.PlayerName,
                    WorkItemStack.Collectible.Code,
                    blockSel.Position
                );

                renderer?.SetContents(WorkItemStack, FuelLevel, burning, true, extraOxygenRateRender);
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos, 0.4375, byPlayer, true);

                MarkDirty();
                return true;
            }

            return false;
        }


        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            renderer?.Dispose();
            renderer = null;
            ambientSound?.Dispose();
            (Api as ICoreClientAPI)?.Event.UnregisterEventBusListener(onEventBusEvent);
        }
        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            renderer?.Dispose();
            (Api as ICoreClientAPI)?.Event.UnregisterEventBusListener(onEventBusEvent);
        }

        public override void OnBlockBroken(IPlayer? byPlayer = null)
        {
            if (!burning && !FuelSlot.Empty)
            {
                Api.World.SpawnItemEntity(FuelSlot.Itemstack, Pos);
            }

            if (WorkItemStack != null)
            {
                Api.World.SpawnItemEntity(WorkItemStack, Pos);
            }

            Inventory.Clear();
            base.OnBlockBroken(byPlayer);

            ambientSound?.Dispose();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            ItemStack? prevStack = WorkItemStack?.Clone();

            base.FromTreeAttributes(tree, worldForResolving);

            ItemStack contents = tree.GetItemstack("contents");
            // Pre 1.22 forges
            if (contents != null && WorkItemStack == null)
            {
                WorkItemSlot.Itemstack = contents;
                if (Api != null)
                {
                    contents?.ResolveBlockOrItem(Api.World);
                }
            }

            partialFuelConsumed = tree.GetFloat("partialFuelConsumed");
            burning = tree.GetInt("burning") > 0;
            lastTickTotalHours = tree.GetDouble("lastTickTotalHours");
            MeshAngleRad = tree.GetFloat("meshAngle", MeshAngleRad);

            bool remesh =
                ((prevStack == null) ^ (WorkItemStack == null)) // stack became null or non-null
                || (prevStack?.StackSize != WorkItemStack?.StackSize) // stack size changed
                || (prevStack != null && !prevStack.Equals(Api?.World, WorkItemStack, GlobalConstants.IgnoredStackAttributes)); // full equality check

            renderer?.SetContents(WorkItemStack, FuelLevel, burning, remesh, extraOxygenRateRender);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            //tree.SetItemstack("contents", WorkItemStack); - why was this still enabled in 1.22?
            tree.SetFloat("partialFuelConsumed", partialFuelConsumed);
            tree.SetInt("burning", burning ? 1 : 0);
            tree.SetDouble("lastTickTotalHours", lastTickTotalHours);
            tree.SetFloat("meshAngle", MeshAngleRad);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (!WorkItemSlot.Empty)
            {
                int temp = (int)WorkItemStack!.Collectible.GetTemperature(Api.World, WorkItemStack);
                if (temp <= 25)
                {
                    dsc.AppendLine(Lang.Get("forge-contentsandtemp-cold", WorkItemStack.StackSize, WorkItemStack.GetName()));
                }
                else
                {
                    dsc.AppendLine(Lang.Get("forge-contentsandtemp", WorkItemStack.StackSize, WorkItemStack.GetName(), temp));
                }
            }

            if (!FuelSlot.Empty)
            {
                var oxygenBurnMul = 1 + extraOxygenRate;
                dsc.AppendLine(Lang.Get("forge-fuel", FuelSlot.Itemstack.GetName()));
                dsc.AppendLine(Lang.Get("forge-fuel-for-hour-amount", FuelLevel / oxygenBurnMul / BurnRate));
            }
        }



        // radiant heat for warming players
        public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
        {
            return IsBurning ? 7 : 0;
        }

        public void CoolNow(float amountRel, OnStackToCool onStackToCoolCallback)
        {
            bool playsound = false;
            if (burning)
            {
                playsound = true;
                partialFuelConsumed += (float)amountRel / 250f;
                updateFuelLevel();
                if (Api.World.Rand.NextDouble() < amountRel / 30f || FuelLevel <= 0)
                {
                    burning = false;
                }
                MarkDirty(true);
            }

            float temp = WorkItemStack?.Collectible.GetTemperature(Api.World, WorkItemStack) ?? 0;
            if (temp > 20)
            {
                playsound = temp > 100;
                onStackToCoolCallback(WorkItemSlot, Pos.ToVec3d(), GlobalConstants.CollectibleDefaultTemperature, playsound);
                MarkDirty(true);
            }

            if (playsound)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), Pos, 0.25, null, false, 16);
            }
        }

        private void updateFuelLevel()
        {
            if (partialFuelConsumed > 1)
            {
                FuelSlot.Itemstack!.StackSize = Math.Max(0, FuelSlot.StackSize - (int)partialFuelConsumed);
                partialFuelConsumed %= 1;
                if (FuelSlot.Itemstack.StackSize <= 0) FuelSlot.Itemstack = null;
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            var mesh = (Api as ICoreClientAPI)!.TesselatorManager.GetDefaultBlockMesh(Block);
            float[] rotMatrix = new Matrixf().Translate(0.5f, 0, 0.5f).RotateY(MeshAngleRad).Translate(-0.5f, 0f, -0.5f).Values;
            mesher.AddMeshData(mesh, rotMatrix);
            return true;
        }


    }
}
