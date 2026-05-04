using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable enable

namespace Vintagestory.GameContent
{
    public enum EnumOvenContentMode
    {
        Firewood,
        SingleCenter,
        Quadrants
    }

    public class BlockEntityOven : BlockEntityDisplay, IHeatSource
    {
        /// <summary>
        /// The number of times to re-render the baked item in the rising stage
        /// </summary>
        public static int BakingStageThreshold = 100; // Every 1% growth, retesselate

        /// <summary>
        /// The maximum baking (or smelting) temperature of items which this oven will accept to be placed
        /// Modded clay ovens wishing to raise this will need to override BlockClayOven.OnLoaded()
        /// </summary>
        public const int maxBakingTemperatureAccepted = 260;

        /// <summary>
        /// The maximum temperature this oven will reach in the burning stage
        /// </summary>
        public virtual float maxTemperature => 300;
        /// <summary>
        /// The number of "regular" bread items the oven can accept
        /// </summary>
        public virtual int bakeableCapacity => 4;
        /// <summary>
        /// The number of logs of firewood the oven can accept
        /// </summary>
        public virtual int fuelitemCapacity => 6;

        /// <summary>
        /// Is there currently burning fuel in the oven?
        /// </summary>
        bool burning;
        /// <summary>
        /// Used client side to know whether to play the burning sound
        /// </summary>
        bool clientSidePrevBurning;

        /// <summary>
        /// Temperature before the half second tick
        /// </summary>
        public float prevOvenTemperature = 20;
        /// <summary>
        /// Current temperature of the oven
        /// </summary>
        public float ovenTemperature = 20;

        /// <summary>
        /// The length of time remaining, for this fuel to continue burning
        /// </summary>
        private float fuelBurnTime;
        /// <summary>
        /// Data about the level of browning/baking reached for the baked items
        /// </summary>
        private readonly OvenItemData[] bakingData;
        /// <summary>
        /// For rendering: degrees of rotation of contents depending on block variant - 0 is east
        /// </summary>
        private int rotationDeg;
        Random? prng = null;
        private int syncCount;

        ILoadedSound? ambientSound;

        /// <summary>
        /// Slots 0-3: Baking items. Slot 0: Fuel  Note: Slot 0 doubles up for both uses, as an oven cannot hold both fuel and baking items at the same time!
        /// </summary>
        internal InventoryOven ovenInv;

        public EnumOvenContentMode OvenContentMode
        {
            get
            {
                if (ovenInv.FirstNonEmptySlot is not ItemSlot slot
                    || BakingProperties.ReadFrom(slot.Itemstack) is not BakingProperties bakingProps) return EnumOvenContentMode.Firewood;

                return bakingProps.LargeItem ? EnumOvenContentMode.SingleCenter : EnumOvenContentMode.Quadrants;
            }
        }

        public BlockEntityOven()
        {
            bakingData = new OvenItemData[bakeableCapacity];
            for (int i = 0; i < bakeableCapacity; i++)
            {
                bakingData[i] = new OvenItemData();
            }
            ovenInv = new InventoryOven("oven-0", bakeableCapacity);
        }

        public override InventoryBase Inventory => ovenInv;

        public override string InventoryClassName => "oven";

        public ItemSlot FuelSlot => ovenInv[0];

        public bool HasFuel => FuelSlot.Itemstack?.ItemAttributes?.IsTrue("isClayOvenFuel") == true;

        public bool IsBurning => burning;

        public bool HasBakeables
        {
            get
            {
                if (HasFuel && FuelSlot.Itemstack?.ItemAttributes?.KeyExists("combustibleProperties") == true) return false;
                return ovenInv.Take(4).Any(slot => !slot.Empty);
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            capi = api as ICoreClientAPI;
            base.Initialize(api);
            ovenInv.LateInitialize(InventoryClassName + "-" + Pos, api);

            RegisterGameTickListener(OnBurnTick, 100);
            this.prng ??= new Random((int)(this.Pos.GetHashCode()));
            this.SetRotation();
        }

        private void SetRotation()
        {
            this.rotationDeg = Block.Variant["side"] switch
            {
                "south" => 270,
                "west" => 180,
                "east" => 0,
                _ => 90,
            };

        }


        #region Interaction: Code for placing and taking items

        public virtual bool OnInteract(IPlayer byPlayer, BlockSelection bs)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!byPlayer.Entity.Controls.ShiftKey)
            {
                if (TryTake(byPlayer))
                {
                    byPlayer.InventoryManager.BroadcastHotbarSlot();
                    return true;
                }

                return false;
            }

            if (slot.Itemstack?.Collectible is not CollectibleObject co) return false;
            AssetLocation stackName = co.Code;
            CombustibleProperties? combustibleProperties = co.GetCombustibleProperties(Api.World, slot.Itemstack, null);

            string? errCode;
            string? errMessage;

            if (co.Attributes?.IsTrue("isClayOvenFuel") == true)
            {
                if (TryAddFuel(slot, out errCode, out errMessage))
                {
                    SoundAttributes? sound = slot.Itemstack?.Block?.Sounds?.Place;
                    Api.World.PlaySoundAt(sound ?? GlobalConstants.DefaultBuildSound, byPlayer.Entity, byPlayer);
                    byPlayer.InventoryManager.BroadcastHotbarSlot();
                    Api.World.Logger.Audit("{0} Put 1x{1} into Clay oven at {2}.",
                        byPlayer.PlayerName,
                        stackName,
                        Pos
                    );
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                    return true;
                }

                (Api as ICoreClientAPI)?.TriggerIngameError(this, errCode, errMessage);

                return false;
            }


            // Can't meaningfully bake anything requiring heat over 260 in the basic clay oven
            if (co?.Attributes?.KeyExists("bakingProperties") == true || combustibleProperties?.SmeltingType == EnumSmeltType.Bake && combustibleProperties.MeltingPoint < maxBakingTemperatureAccepted)
            {

                if (TryPut(slot, out errCode, out errMessage))
                {
                    SoundAttributes? sound = slot.Itemstack?.Block?.Sounds?.Place;
                    Api.World.PlaySoundAt(sound ?? new SoundAttributes(new AssetLocation("sounds/player/buildhigh"), true) { Range = 16 }, byPlayer.Entity, byPlayer);
                    byPlayer.InventoryManager.BroadcastHotbarSlot();
                    Api.World.Logger.Audit("{0} Put 1x{1} into Clay oven at {2}.",
                        byPlayer.PlayerName,
                        stackName,
                        Pos
                    );
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                }
                else if (slot.Itemstack.Block?.GetBehavior<BlockBehaviorCanIgnite>() == null)
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, errCode, errMessage);
                }

                return true;
            }

            return false;
        }

        protected virtual bool TryAddFuel(ItemSlot slot)
        {
            return TryAddFuel(slot, out _, out _);
        }

        protected virtual bool TryAddFuel(ItemSlot slot, out string? errCode, out string? errMessage)
        {
            errCode = null;
            errMessage = null;

            if (!CanAddFuel(slot.Itemstack, out errCode, out errMessage)) return false;

            int moved = slot.TryPutInto(Api.World, FuelSlot);

            if (moved > 0)
            {
                updateMesh(0);
                MarkDirty();
            }

            return moved > 0;
        }

        protected virtual bool TryPut(ItemSlot slot)
        {
            return TryPut(slot, out _, out _);
        }

        protected virtual bool TryPut(ItemSlot slot, out string? errCode, out string? errMessage)
        {
            // CanAddBakeable checks for large item requirements
            if (!CanAddBakeable(slot.Itemstack, out errCode, out errMessage)) return false;

            int empty = Array.FindIndex(ovenInv.Take(4).ToArray(), slot => slot.Empty);
            if (empty == -1) return false;

            int moved = slot.TryPutInto(Api.World, ovenInv[empty]);

            if (moved > 0)
            {
                // We store the baked level data into the BlockEntity itself, for continuity and to avoid adding unwanted attributes to the ItemStacks (which e.g. could cause them not to stack)
                bakingData[empty] = new OvenItemData(ovenInv[empty].Itemstack);
                updateMesh(empty);

                MarkDirty();
            }

            return moved > 0;
        }

        protected virtual bool TryTake(IPlayer byPlayer)
        {
            // We cannot remove fuel once it is lit
            if (IsBurning) return false;

            for (int index = bakeableCapacity; index >= 0; index--)
            {
                if (ovenInv[index].Empty) continue;

                ItemStack stack = ovenInv[index].TakeOut(1);
                if (byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    SoundAttributes? sound = stack.Block?.Sounds?.Place;
                    Api.World.PlaySoundAt(sound ?? new SoundAttributes(new AssetLocation("sounds/player/throw"), true) { Range = 16 }, byPlayer.Entity, byPlayer);
                }

                if (stack.StackSize > 0)
                {
                    Api.World.SpawnItemEntity(stack, Pos);
                }

                Api.World.Logger.Audit("{0} Took 1x{1} from Clay oven at {2}.",
                    byPlayer.PlayerName,
                    stack.Collectible.Code,
                    Pos
                );

                bakingData[index].CurHeightMul = 1; // Reset risenLevel to avoid brief render of unwanted size on next item inserted, if server/client not perfectly in sync - note this only really works if the newly inserted item can be assumed to have risenLevel of 0 i.e. dough
                updateMesh(index);
                MarkDirty();
                return true;
            }

            return false;
        }

        public bool CanAddBakeable(ItemStack stack)
        {
            return CanAddBakeable(stack, out _, out _);
        }

        /// <summary>
        /// Whether or not the oven can currently accept a given bakeable item.
        /// </summary>
        /// <returns>True if either the oven is empty or there is enough space to accept the given item. Oven must not be burning or contain fuel.</returns>
        public virtual bool CanAddBakeable(ItemStack? stack, out string? errCode, out string? errMessage)
        {
            errCode = null;
            errMessage = null;

            if (IsBurning)
            {
                errCode = "fuelburning";
                errMessage = Lang.Get("Wait until the fire is out");
                return false;
            }

            if (HasFuel)
            {
                errCode = "fuelpresent";
                errMessage = Lang.Get("ovenerror-notfuel");
                return false;
            }

            // Don't invite player to insert bakeable items in a cold oven - 25 degrees allows some hysteresis if SEASONS causes changes in enviro temperature
            if (ovenTemperature <= EnvironmentTemperature() + 25)
            {
                errCode = "toocold";
                errMessage = Lang.Get("ovenerror-toocold");
                return false;
            }


            if (ovenInv[0].Empty) return true;


            if (stack?.ItemAttributes?.KeyExists("bakingProperties") == false)
            {
                errCode = "notbakeable";
                errMessage = Lang.Get("This item is not bakeable.");
                return false;
            }

            // Large items take up all slots
            if (BakingProperties.ReadFrom(ovenInv[0].Itemstack)?.LargeItem ?? false)
            {
                errCode = "ovenfull";
                errMessage = Lang.Get("Oven is full");
                return false;
            }

            if (ovenInv.Take(4).All(slot => !slot.Empty))
            {
                errCode = "ovenfull";
                errMessage = Lang.Get("Oven is full");
                return false;
            }

            // Handle held large item separately if not all slots are full
            if (BakingProperties.ReadFrom(stack)?.LargeItem ?? false && !ovenInv[0].Empty)
            {
                errCode = "notenoughspace";
                errMessage = Lang.Get("ovenerror-notenoughspace");
                return false;
            }

            return true;
        }

        public bool CanAddFuel(ItemStack? stack)
        {
            return CanAddFuel(stack, out _, out _);
        }

        /// <summary>
        /// Whether or not the oven can currently accept a given fuel item.
        /// </summary>
        /// <returns>True if either the oven is empty or the fuel slot is of the same type and has space. Oven must not be burning or contain bakeables.</returns>
        public virtual bool CanAddFuel(ItemStack? stack, out string? errCode, out string? errMessage)
        {
            errCode = null;
            errMessage = null;

            if (FuelSlot.Empty) return true;

            if (IsBurning)
            {
                errCode = "fuelburning";
                errMessage = Lang.Get("Wait until the fire is out");
                return false;
            }

            if (stack?.ItemAttributes?.IsTrue("isClayOvenFuel") == false)
            {
                errCode = "notfuel";
                errMessage = Lang.Get("ovenerror-notfuel");
                return false;
            }

            if (HasFuel && FuelSlot.StackSize < fuelitemCapacity && !FuelSlot.Itemstack.Satisfies(stack))
            {
                errCode = "nonmatchingfuel";
                errMessage = Lang.Get("ovenerror-nonmatchingfuel");
                return false;
            }

            // Bakeables are already present
            if (!FuelSlot.Empty && !HasFuel)
            {
                errCode = "notbakeable";
                errMessage = Lang.Get("This item is not bakeable.");
                return false;
            }

            if (FuelSlot.StackSize >= fuelitemCapacity)
            {
                errCode = "ovenfull";
                errMessage = Lang.Get("Oven is full");
                return false;
            }

            return true;
        }

        public bool TryIgnite()
        {
            if (!CanIgnite()) return false;

            burning = true;
            fuelBurnTime = 45 + FuelSlot.StackSize * 5;  //approximately 1 hour of game time for a full stack of fuel
            MarkDirty();
            ambientSound?.Start();
            return true;
        }

        public bool CanIgnite()
        {
            return HasFuel && !burning;
        }

        #endregion

        #region Heating, Cooling
        public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
        {
            return Math.Max((this.ovenTemperature - 20f) / (this.maxTemperature - 20f) * 8f, 0f);
        }


        protected virtual void OnBurnTick(float dt)
        {
            dt *= 1.25f;   //slight speedup because everything just felt too slow...

            // Only tick on the server and merely sync to client
            if (Api is ICoreClientAPI)
            {
                // TODO: maybe update rendered transitions gradually?  similar to renderer?.contentStackRenderer?.OnUpdate(InputStackTemp);
                return;
            }

            // Use up fuel
            if (fuelBurnTime > 0)
            {
                fuelBurnTime -= dt;

                if (fuelBurnTime <= 0)
                {
                    fuelBurnTime = 0;
                    burning = false;
                    CombustibleProperties? props = FuelSlot.Itemstack?.Collectible.GetCombustibleProperties(Api.World, FuelSlot.Itemstack, null);
                    if (props?.SmeltedStack?.ResolvedItemstack?.Clone() is ItemStack smeltedStack)
                    {
                        // Allows for wood ash inserted by mods (for example, LazyWarlock's Tweaks)
                        int count = FuelSlot.StackSize;
                        FuelSlot.Itemstack = smeltedStack;
                        FuelSlot.Itemstack.StackSize = count * props.SmeltedRatio;
                    }
                    else
                    {
                        FuelSlot.Itemstack = null;
                        for (int i = 0; i < bakeableCapacity; i++) bakingData[i].CurHeightMul = 1;
                    }
                    MarkDirty(true);
                }
            }

            // Heat furnace slowly if fire is burning, or else cool down very slowly
            if (IsBurning)
            {
                ovenTemperature = ChangeTemperature(ovenTemperature, maxTemperature, dt * FuelSlot.StackSize / fuelitemCapacity);
            }
            else
            {
                int environmentTemperature = EnvironmentTemperature();
                if (ovenTemperature > environmentTemperature)
                {
                    HeatInput(dt);
                    ovenTemperature = ChangeTemperature(ovenTemperature, environmentTemperature, dt / 24f);
                }
            }


            // Sync to client every 500ms
            if (++syncCount % 5 == 0 && (IsBurning || prevOvenTemperature != ovenTemperature || !Inventory[0].Empty || !Inventory[1].Empty || !Inventory[2].Empty || !Inventory[3].Empty))
            {
                MarkDirty();
                prevOvenTemperature = ovenTemperature;
            }
        }

        protected virtual void HeatInput(float dt)
        {
            for (int slotIndex = 0; slotIndex < bakeableCapacity; slotIndex++)
            {
                if (ovenInv[slotIndex].Itemstack is ItemStack stack && !stack.ItemAttributes.KeyExists("combustibleProperties"))
                {
                    float nowTemp = HeatStack(stack, dt, slotIndex);
                    // Begin baking - or at least rising - when hot enough
                    if (nowTemp >= 100f)
                    {
                        IncrementallyBake(dt * 1.2f, slotIndex);
                    }
                }
            }
        }

        protected virtual float HeatStack(ItemStack stack, float dt, int i)
        {
            float oldTemp = bakingData[i].temp;
            float nowTemp = oldTemp;

            if (oldTemp < ovenTemperature)
            {
                float f = (1 + GameMath.Clamp((ovenTemperature - oldTemp) / 28, 0, 1.6f)) * dt;
                nowTemp = ChangeTemperature(oldTemp, ovenTemperature, f);
                CombustibleProperties combustibleProps = stack.Collectible.GetCombustibleProperties(Api.World, stack, null);
                int maxTemp = Math.Max(combustibleProps?.MaxTemperature ?? 0, stack.ItemAttributes?["maxTemperature"].AsInt(0) ?? 0);
                if (maxTemp > 0)
                {
                    nowTemp = Math.Min(maxTemp, nowTemp);
                }
            }
            else if (oldTemp > ovenTemperature)
            {
                float f = (1 + GameMath.Clamp((oldTemp - ovenTemperature) / 28, 0, 1.6f)) * dt;
                nowTemp = ChangeTemperature(oldTemp, ovenTemperature, f);
            }

            if (oldTemp != nowTemp)
            {
                bakingData[i].temp = nowTemp;
            }

            return nowTemp;
        }

        protected virtual void IncrementallyBake(float dt, int slotIndex)
        {
            ItemSlot slot = Inventory[slotIndex];
            OvenItemData bakeData = bakingData[slotIndex];

            float targetTemp = bakeData.BrowningPoint;
            if (targetTemp == 0) targetTemp = 160f;  //prevents any possible divide by zero
            float diff = bakeData.temp / targetTemp;
            float timeFactor = bakeData.TimeToBake;
            if (timeFactor == 0) timeFactor = 1;  //prevents any possible divide by zero
            float delta = GameMath.Clamp((int)diff, 1, 30) * dt / timeFactor;

            float currentLevel = bakeData.BakedLevel;
            if (bakeData.temp > targetTemp)
            {
                currentLevel = bakeData.BakedLevel + delta;
                bakeData.BakedLevel = currentLevel;
            }

            var bakeProps = BakingProperties.ReadFrom(slot.Itemstack);
            float levelFrom = bakeProps?.LevelFrom ?? 0f;
            float levelTo = bakeProps?.LevelTo ?? 1f;
            float startHeightMul = bakeProps?.StartScaleY ?? 1f;
            float endHeightMul = bakeProps?.EndScaleY ?? 1f;

            float progress = GameMath.Clamp((currentLevel - levelFrom) / (levelTo - levelFrom), 0, 1);
            float heightMul = GameMath.Mix(startHeightMul, endHeightMul, progress);
            float nowHeightMulStaged = (int)(heightMul * BakingStageThreshold) / (float)BakingStageThreshold;

            bool reDraw = nowHeightMulStaged != bakeData.CurHeightMul;

            bakeData.CurHeightMul = nowHeightMulStaged;

            // see if increasing the partBaked by delta, has moved this stack up to the next "bakedStage", i.e. a different item

            if (currentLevel > levelTo)
            {
                float nowTemp = bakeData.temp;

                if (bakeProps?.ResultCode is string resultCode)
                {
                    ItemStack? resultStack = null;
                    if (slot.Itemstack?.Class == EnumItemClass.Block)
                    {
                        if (Api.World.GetBlock(new AssetLocation(resultCode)) is Block block) resultStack = new ItemStack(block);
                    }
                    else
                    {
                        if (Api.World.GetItem(new AssetLocation(resultCode)) is Item item) resultStack = new ItemStack(item);
                    }

                    if (resultStack != null)
                    {
                        TransitionableProperties?[] tprops = resultStack.Collectible.GetTransitionableProperties(Api.World, slot.Itemstack, null);

                        // Carry over freshness
                        if (tprops?.FirstOrDefault(p => p?.Type == EnumTransitionType.Perish) is TransitionableProperties perishProps)
                        {
                            CollectibleObject.CarryOverFreshness(Api, slot, resultStack, perishProps);
                        }

                        ovenInv[slotIndex].Itemstack?.Collectible.GetCollectibleInterface<IBakeableCallback>()?.OnBaked(ovenInv[slotIndex].Itemstack!, resultStack);

                        ovenInv[slotIndex].Itemstack = resultStack;
                        bakingData[slotIndex] = new OvenItemData(resultStack)
                        {
                            temp = nowTemp
                        };

                        reDraw = true;
                    }
                }
                else
                {
                    // Allow the oven also to 'smelt' low-temperature bakeable items which do not have specific baking properties

                    ItemSlot result = new DummySlot(null);
                    if (slot.Itemstack?.Collectible.CanSmelt(Api.World, ovenInv, slot.Itemstack, null) ?? false)
                    {
                        slot.Itemstack.Collectible.DoSmelt(Api.World, ovenInv, ovenInv[slotIndex], result);
                        if (!result.Empty)
                        {
                            ovenInv[slotIndex].Itemstack = result.Itemstack;
                            bakingData[slotIndex] = new OvenItemData(result.Itemstack)
                            {
                                temp = nowTemp
                            };
                            reDraw = true;
                        }
                    }
                }
            }

            if (reDraw) MarkDirty();
        }

        /// <summary>
        /// Resting temperature - note it can change
        /// </summary>
        protected virtual int EnvironmentTemperature()
        {
            float temperature = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, Api.World.Calendar.TotalDays).Temperature;
            return (int)temperature;
        }

        public virtual float ChangeTemperature(float fromTemp, float toTemp, float dt)
        {
            float diff = Math.Abs(fromTemp - toTemp);
            diff *= GameMath.Sqrt(diff);
            dt += dt * (diff / 480);
            if (diff < dt)
            {
                return toTemp;
            }
            if (fromTemp > toTemp)
            {
                dt = -dt / 2;
            }
            if (Math.Abs(fromTemp - toTemp) < 1)
            {
                return toTemp;
            }
            return fromTemp + dt;
        }
        #endregion

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            ovenInv.FromTreeAttributes(tree);
            burning = tree.GetInt("burn") > 0;
            rotationDeg = tree.GetInt("rota");
            ovenTemperature = tree.GetFloat("temp");
            fuelBurnTime = tree.GetFloat("tfuel");

            for (int i = 0; i < bakeableCapacity; i++)
            {
                bakingData[i] = OvenItemData.ReadFromTree(tree, i);
            }

            if (Api?.Side == EnumAppSide.Client)
            {
                MarkMeshesDirty();
                if (clientSidePrevBurning != IsBurning)
                {
                    ToggleAmbientSounds(IsBurning);
                    clientSidePrevBurning = IsBurning;
                    MarkDirty(true);
                }
                else
                {
                    Api.World.BlockAccessor.MarkBlockDirty(Pos);   // always redraw on client after updating meshes
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            ovenInv.ToTreeAttributes(tree);
            tree.SetInt("burn", burning ? 1 : 0);
            tree.SetInt("rota", rotationDeg);
            tree.SetFloat("temp", ovenTemperature);
            tree.SetFloat("tfuel", fuelBurnTime);

            for (int i = 0; i < bakeableCapacity; i++)
            {
                bakingData[i].WriteToTree(tree, i);
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            if (ovenTemperature <= 25)
            {
                sb.AppendLine(Lang.Get("Temperature: {0}", Lang.Get("Cold")));
                if (!IsBurning)
                {
                    sb.AppendLine(Lang.Get("clayoven-preheat-warning"));
                }
            }
            else
            {
                sb.AppendLine(Lang.Get("Temperature: {0}°C", (int)ovenTemperature));
                if (ovenTemperature < 100 && !IsBurning)
                {
                    sb.AppendLine(Lang.Get("Reheat to continue baking"));
                }
            }

            sb.AppendLine();

            for (int index = 0; index < bakeableCapacity; index++)
            {
                if (!ovenInv[index].Empty)
                {
                    sb.Append(ovenInv[index].Itemstack?.GetName());
                    sb.AppendLine(" (" + Lang.Get("{0}°C", (int)bakingData[index].temp) + ")");
                }
            }
        }


        #region Sounds

        public virtual void ToggleAmbientSounds(bool on)
        {
            if (Api.Side != EnumAppSide.Client) return;

            if (on)
            {
                if (ambientSound == null || !ambientSound.IsPlaying)
                {
                    ambientSound = ((IClientWorldAccessor)Api.World).LoadSound(new SoundParams()
                    {
                        Location = new AssetLocation("sounds/environment/fireplace.ogg"),
                        ShouldLoop = true,
                        Position = Pos.ToVec3f().Add(0.5f, 0.1f, 0.5f),
                        DisposeOnFinish = false,
                        Volume = 0.66f
                    });

                    ambientSound.Start();
                }
            }
            else
            {
                ambientSound!.Stop();
                ambientSound!.Dispose();
                ambientSound = null;
            }

        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (ambientSound != null)
            {
                ambientSound.Stop();
                ambientSound.Dispose();
            }
        }

        #endregion


        #region Meshing

        public override int DisplayedItems
        {
            get
            {
                if (OvenContentMode == EnumOvenContentMode.Quadrants) return 4;
                return 1;
            }
        }

        protected override float[][] genTransformationMatrices()
        {
            float[][] tfMatrices = new float[DisplayedItems][];
            Vec3f[] offs = new Vec3f[DisplayedItems];

            switch (OvenContentMode)
            {
                case EnumOvenContentMode.Firewood:
                    offs[0] = new Vec3f();
                    break;
                case EnumOvenContentMode.Quadrants:
                    // Top left
                    offs[0] = new Vec3f(-2 / 16f, 1 / 16f, -2.5f / 16f);
                    // Top right
                    offs[1] = new Vec3f(-2 / 16f, 1 / 16f, 2.5f / 16f);
                    // Bot left
                    offs[2] = new Vec3f(3 / 16f, 1 / 16f, -2.5f / 16f);
                    // Bot right
                    offs[3] = new Vec3f(3 / 16f, 1 / 16f, 2.5f / 16f);
                    break;
                case EnumOvenContentMode.SingleCenter:
                    offs[0] = new Vec3f(0, 1 / 16f, 0);
                    break;
            }

            for (int i = 0; i < tfMatrices.Length; i++)
            {
                Vec3f off = offs[i];

                float scaleY = OvenContentMode == EnumOvenContentMode.Firewood ? 0.9f : bakingData[i].CurHeightMul;

                tfMatrices[i] =
                    new Matrixf()
                    .Translate(off.X, off.Y, off.Z)
                    .Translate(0.5f, 0, 0.5f)
                    .RotateYDeg(rotationDeg + (OvenContentMode == EnumOvenContentMode.Firewood ? 270 : 0))
                    .Scale(0.9f, scaleY, 0.9f)
                    .Translate(-0.5f, 0, -0.5f)
                    .Values
                ;
            }

            return tfMatrices;
        }

        protected override string getMeshCacheKey(ItemSlot slot)
        {
            string scaleY = "";
            for (int i = 0; i < bakingData.Length; i++)
            {
                if (Inventory[i].Itemstack == slot.Itemstack)
                {
                    scaleY = "-" + bakingData[i].CurHeightMul;
                    break;
                }
            }

            return (OvenContentMode == EnumOvenContentMode.Firewood ? slot.StackSize + "x" : "") + base.getMeshCacheKey(slot) + scaleY;
        }

        protected override MeshData? getOrCreateMesh(ItemSlot slot, int index)
        {
            if (OvenContentMode == EnumOvenContentMode.Firewood)
            {
                if (getMesh(slot) is MeshData mesh) return mesh;

                ItemStack? stack = slot.Itemstack;
                string? shapeLoc = FuelSlot.Itemstack?.ItemAttributes["ovenFuelShape"].AsString() ?? Block.Attributes["ovenFuelShape"].AsString();

                var loc = AssetLocation.Create(shapeLoc, Block.Code.Domain).WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
                nowTesselatingShape = Shape.TryGet(capi, loc);
                nowTesselatingObj = stack?.Collectible;

                if (nowTesselatingShape == null)
                {
                    capi.Logger.Error("Stacking model shape for collectible " + stack?.Collectible.Code + " not found. Block will be invisible!");
                    return null;
                }

                capi.Tesselator.TesselateShape("ovenFuelShape", nowTesselatingShape, out mesh, this, null, 0, 0, 0, stack?.StackSize);

                string key = getMeshCacheKey(slot);
                MeshCache[key] = mesh;

                return mesh;
            }

            return base.getOrCreateMesh(slot, index);
        }


        public virtual void RenderParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking, AdvancedParticleProperties[] particles)
        {
            if (this.fuelBurnTime < 3) return;
            int logsCount = FuelSlot.StackSize;
            bool fireFull = logsCount > 3;  //fireFull means it's a fire with 2 rows of logs (between 4 and 6 logs) - flames will start higher
            double[] x = new double[4];
            float[] z = new float[4];
            for (int i = 0; i < particles.Length; i++)
            {
                //reduced number of bright yellow flames - and reduces as the fuel burns
                if (i >= 12 && prng!.Next(0, 90) > this.fuelBurnTime) continue;

                //limit orange flames when fuel is almost burned
                if (i >= 8 && i < 12 && prng!.Next(0, 12) > this.fuelBurnTime) continue;

                //also randomise red flames
                if (i >= 4 && i < 4 && prng!.Next(0, 6) == 0) continue;

                //adjust flames to the number of logs, if less than 3 logs
                if (i >= 4 && logsCount < 3)
                {
                    bool rotated = this.rotationDeg >= 180;
                    if (!rotated && z[i % 2] > logsCount * 0.2f + 0.14f) continue;
                    if (rotated && z[i % 2] < (3 - logsCount) * 0.2f + 0.14f) continue;
                }

                AdvancedParticleProperties bps = particles[i];
                bps.WindAffectednesAtPos = 0f;
                bps.basePos.X = pos.X;
                bps.basePos.Y = pos.InternalY + (fireFull ? 3 / 32f : 1 / 32f);
                bps.basePos.Z = pos.Z;

                //i >= 4 is flames; i < 4 is smoke
                if (i >= 4)
                {
                    bool rotate = this.rotationDeg % 180 > 0;
                    if (fireFull) rotate = !rotate;
                    bps.basePos.Z += rotate ? x[i % 2] : z[i % 2];
                    bps.basePos.X += rotate ? z[i % 2] : x[i % 2];
                    bps.basePos.Y += (fireFull ? 4 : 3) / 32f;
                    switch (this.rotationDeg)
                    {
                        case 0:
                            bps.basePos.X -= fireFull ? 0.08f : 0.12f;
                            break;
                        case 180:
                            bps.basePos.X += fireFull ? 0.08f : 0.12f;
                            break;
                        case 90:
                            bps.basePos.Z += fireFull ? 0.08f : 0.12f;
                            break;
                        default:
                            bps.basePos.Z -= fireFull ? 0.08f : 0.12f;
                            break;
                    }
                }
                else
                //set up flame positions with RNG (this way all three flame evolution particles will be in approx. same position)
                {
                    x[i] = prng!.NextDouble() * 0.4f + 0.33f;   // the multiplier and offset gets the flame position aligned with the top surface of the logs
                    z[i] = 0.26f + prng.Next(0, 3) * 0.2f + (float)prng!.NextDouble() * 0.08f;
                }

                manager.Spawn(bps);
            }
        }

        #endregion

    }
}
