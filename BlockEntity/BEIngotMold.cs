using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public interface ILiquidMetalSink
    {
        bool CanReceiveAny { get; }
        bool CanReceive(ItemStack key);
        void BeginFill(Vec3d hitPosition);
        void ReceiveLiquidMetal(ItemStack key, ref int transferedAmount, float temp);
        void OnPourOver();
    }

    public class BlockEntityIngotMold : BlockEntity, ILiquidMetalSink, ITemperatureSensitive, ITexPositionSource, IRotatable
    {
        protected long lastPouringMarkdirtyMs;
        protected IngotMoldRenderer? ingotRenderer;

        public MeshData? MoldMeshLeft;
        public MeshData? MoldMeshRight;
        public ItemStack? MoldLeft;
        public ItemStack? MoldRight;
        public ItemStack? ContentsLeft;
        public ItemStack? ContentsRight;
        public int FillLevelLeft = 0;
        public int FillLevelRight = 0;
        public int QuantityMolds = 1;
        public bool IsRightSideSelected;
        public bool ShatteredLeft;
        public bool ShatteredRight;

        public int RequiredUnits = 100;

        public float TemperatureLeft => ContentsLeft?.Collectible.GetTemperature(Api.World, ContentsLeft) ?? 0;
        public float TemperatureRight => ContentsRight?.Collectible.GetTemperature(Api.World, ContentsRight) ?? 0;
        public bool IsHardenedLeft => TemperatureLeft < 0.3f * ContentsLeft?.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(ContentsLeft));
        public bool IsHardenedRight => TemperatureRight < 0.3f * ContentsRight?.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(ContentsRight));
        public bool IsLiquidLeft => TemperatureLeft > 0.8f * ContentsLeft?.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(ContentsLeft));
        public bool IsLiquidRight => TemperatureRight > 0.8f * ContentsRight?.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(ContentsRight));
        public bool IsFullLeft => FillLevelLeft >= RequiredUnits;
        public bool IsFullRight => FillLevelRight >= RequiredUnits;
        public bool IsHot => TemperatureLeft >= 200 || TemperatureRight >= 200;
        public bool CanReceiveAny => !BothShattered && (MoldLeft?.Block?.Variant["type"] == "fired" || MoldLeft?.Block?.Code.Path.Contains("burned") == true || MoldRight?.Block.Variant["type"] == "fired" || MoldRight?.Block?.Code.Path.Contains("burned") == true);

        bool BothShattered => ShatteredLeft && ShatteredRight;

        public ItemStack? SelectedMold
        {
            get => IsRightSideSelected ? MoldRight : MoldLeft; set => (IsRightSideSelected ? ref MoldRight : ref MoldLeft) = value;
        }
        public ItemStack? SelectedContents
        {
            get => IsRightSideSelected ? ContentsRight : ContentsLeft; set => (IsRightSideSelected ? ref ContentsRight : ref ContentsLeft) = value;
        }
        public int SelectedFillLevel
        {
            get => IsRightSideSelected ? FillLevelRight : FillLevelLeft; set => (IsRightSideSelected ? ref FillLevelRight : ref FillLevelLeft) = value;
        }
        public bool SelectedShattered
        {
            get => IsRightSideSelected ? ShatteredRight : ShatteredLeft; set => (IsRightSideSelected ? ref ShatteredRight : ref ShatteredLeft) = value;
        }
        public float SelectedTemperature => IsRightSideSelected ? TemperatureRight : TemperatureLeft;
        public bool SelectedIsHardened => IsRightSideSelected ? IsHardenedRight : IsHardenedLeft;
        public bool SelectedIsLiquid => IsRightSideSelected ? IsLiquidRight : IsLiquidLeft;
        public bool SelectedIsFull => IsRightSideSelected ? IsFullRight : IsFullLeft;


        ICoreClientAPI? capi;
        public float MeshAngle;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            capi = api as ICoreClientAPI;
            if (capi != null && !BothShattered)
            {
                capi.Event.RegisterRenderer(ingotRenderer = new IngotMoldRenderer(this, capi), EnumRenderStage.Opaque, "ingotmold");

                UpdateIngotRenderer();

                if (MoldMeshLeft == null || MoldMeshRight == null)
                {
                    GenMeshes();
                }
            }

            if (!BothShattered) RegisterGameTickListener(OnGameTick, 50);
        }


        private void OnGameTick(float dt)
        {
            if (ingotRenderer != null)
            {
                ingotRenderer.QuantityMolds = QuantityMolds;
                ingotRenderer.LevelLeft = ShatteredLeft ? 0 : FillLevelLeft;
                ingotRenderer.LevelRight = ShatteredRight ? 0 : FillLevelRight;
            }

            if (ContentsLeft != null && ingotRenderer != null)
            {
                ingotRenderer.stack = ContentsLeft;
                ingotRenderer.TemperatureLeft = Math.Min(1300, ContentsLeft.Collectible.GetTemperature(Api.World, ContentsLeft));
            }

            if (ContentsRight != null && ingotRenderer != null)
            {
                ingotRenderer.stack = ContentsRight;
                ingotRenderer.TemperatureRight = Math.Min(1300, ContentsRight.Collectible.GetTemperature(Api.World, ContentsRight));
            }
        }


        public bool CanReceive(ItemStack metal)
        {
            return
                ContentsLeft == null
                || ContentsRight == null
                || (ContentsLeft.Collectible.Equals(ContentsLeft, metal, GlobalConstants.IgnoredStackAttributes) && !IsFullLeft)
                || (ContentsRight.Collectible.Equals(ContentsRight, metal, GlobalConstants.IgnoredStackAttributes) && !IsFullRight)
            ;
        }



        public void BeginFill(Vec3d hitPosition)
        {
            SetSelectedSide(hitPosition);
        }

        public void SetSelectedSide(Vec3d hitPosition)
        {
            if (QuantityMolds > 1)
            {
                var facing = BlockFacing.HorizontalFromAngle(MeshAngle);
                switch (facing.Index)
                {
                    case 0:
                        {
                            IsRightSideSelected = hitPosition.Z < 0.5f;
                            break;
                        }
                    case 1:
                        {
                            IsRightSideSelected = hitPosition.X >= 0.5f;
                            break;
                        }
                    case 2:
                        {
                            IsRightSideSelected = hitPosition.Z >= 0.5f;
                            break;
                        }
                    case 3:
                        {
                            IsRightSideSelected = hitPosition.X < 0.5f;
                            break;
                        }
                }
            }
            else IsRightSideSelected = false;
        }


        public bool OnPlayerInteract(IPlayer byPlayer, BlockFacing onFace, Vec3d hitPosition)
        {
            if (BothShattered) return false;

            bool moldInHands = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible is BlockIngotMold;
            bool sneaking = byPlayer.Entity.Controls.ShiftKey;

            if (!sneaking)
            {
                if (byPlayer.Entity.Controls.HandUse != EnumHandInteract.None) return false;

                bool handled = TryTakeIngot(byPlayer, hitPosition);

                if (!handled)
                {
                    handled = TryTakeMold(byPlayer, hitPosition);
                }

                return handled;
            }

            if (sneaking && moldInHands)
            {
                return TryPutMold(byPlayer);
            }

            return false;
        }

        public ItemStack[] GetStateAwareMolds()
        {
            return [ .. GetStateAwareMoldSided(MoldLeft, ShatteredLeft), .. GetStateAwareMoldSided(MoldRight, ShatteredRight), ];
        }

        public ItemStack[] GetStateAwareMoldSided(ItemStack? mold, bool shattered)
        {
            if (mold == null) return [];

            List<ItemStack> molds = [];

            if (!shattered) molds.Add(mold.Clone());
            else
            {
                if (mold.Block.Attributes?["shatteredDrops"].AsObject<BlockDropItemStack[]?>(null) is BlockDropItemStack[] shatteredDrops)
                {
                    for (int i = 0; i < shatteredDrops.Length; i++)
                    {
                        shatteredDrops[i].Resolve(Api.World, "shatteredDrops[" + i + "] for", mold.Block.Code);
                        ItemStack stack = shatteredDrops[i].GetNextItemStack();
                        if (stack == null) continue;

                        molds.Add(stack);
                        if (shatteredDrops[i].LastDrop) break;
                    }
                }
            }

            return [.. molds];
        }

        public ItemStack[] GetStateAwareMoldedStacks()
        {
            List<ItemStack> moldedStacks = [];

            if (GetStateAwareContentsLeft() is ItemStack leftStack) moldedStacks.Add(leftStack);
            if (GetStateAwareContentsRight() is ItemStack rightStack) moldedStacks.Add(rightStack);

            return [.. moldedStacks];
        }

        public ItemStack? GetSelectedStateAwareContents()
        {
            return GetStateAwareContentsSided(SelectedContents, SelectedFillLevel, SelectedShattered, SelectedIsHardened);
        }

        public ItemStack? GetStateAwareContentsLeft()
        {
            return GetStateAwareContentsSided(ContentsLeft, FillLevelLeft, ShatteredLeft, IsHardenedLeft);
        }

        /// <summary>
        /// Retrieves the molded ingot, will always return null for incomplete pours. Has a chance of returning metal bits if retrieved while still hot.
        /// </summary>
        /// <returns></returns>
        public ItemStack? GetStateAwareContentsRight()
        {
            return GetStateAwareContentsSided(ContentsRight, FillLevelRight, ShatteredRight, IsHardenedRight);
        }

        public ItemStack? GetStateAwareContentsSided(ItemStack? contents, int fillLevel, bool shattered, bool isHardened)
        {
            if (contents != null && isHardened)
            {
                if (shattered)
                {
                    var shatteredStack = contents.Collectible.Attributes?["shatteredStack"].AsObject<JsonItemStack>();
                    shatteredStack?.Resolve(Api.World, "shatteredStack for" + contents.Collectible.Code);

                    if (shatteredStack?.ResolvedItemstack is ItemStack stack)
                    {
                        stack.StackSize = (int)(fillLevel / 5f);
                        return stack;
                    }

                    return null;
                }

                if (fillLevel >= RequiredUnits)
                {
                    ItemStack outstack = contents.Clone();
                    (outstack.Attributes["temperature"] as ITreeAttribute)?.RemoveAttribute("cooldownSpeed");
                    return outstack;
                }
            }

            return null;
        }


        /// <summary>
        /// Retrieves the chiseled stack, for use when the player is removing hardened pours by chisel
        /// </summary>
        /// <returns></returns>
        public ItemStack? GetChiseledStack(ItemStack? contents, int fillLevel, bool shattered, bool isHardened)
        {
            if (contents != null && fillLevel > 0 && !shattered && isHardened)
            {
                var chiseledStack = contents.Collectible.Attributes?["shatteredStack"].AsObject<JsonItemStack>();
                chiseledStack?.Resolve(Api.World, "chiseledStack for" + contents.Collectible.Code);

                if (chiseledStack?.ResolvedItemstack is ItemStack stack)
                {
                    stack.StackSize = (int)(fillLevel / 5f);
                    return stack;
                }
            }

            return null;
        }


        protected bool TryTakeIngot(IPlayer byPlayer, Vec3d hitPosition)
        {
            if (Api is ICoreServerAPI) MarkDirty();

            SetSelectedSide(hitPosition);

            ItemStack? drop = GetSelectedStateAwareContents();
            if (drop != null && SelectedIsHardened && !SelectedShattered)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos, -0.5, byPlayer, false);
                if (Api is ICoreServerAPI)
                {
                    if (!byPlayer.InventoryManager.TryGiveItemstack(drop))
                    {
                        Api.World.SpawnItemEntity(drop, Pos.ToVec3d().Add(0.5, 0.2, 0.5));
                    }
                    Api.World.Logger.Audit("{0} Took 1x{1} from Ingot mold at {2}.",
                        byPlayer.PlayerName,
                        drop.Collectible.Code,
                        Pos
                    );

                    SelectedContents = null;
                    SelectedFillLevel = 0;
                }

                return true;
            }

            return false;
        }

        protected bool TryTakeMold(IPlayer byPlayer, Vec3d hitPosition)
        {
            var activeStack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
            if (activeStack != null && activeStack.Collectible is not BlockToolMold and not BlockIngotMold) return false;
            if (FillLevelLeft != 0 && FillLevelRight != 0) return false;

            SetSelectedSide(hitPosition);
            if (SelectedMold == null) return false;

            var itemStack = new ItemStack(SelectedMold.Block);
            var placeSound = SelectedMold.Block.Sounds?.Place;
            if (SelectedFillLevel == 0 && !SelectedShattered)
            {
                if (!IsRightSideSelected && QuantityMolds > 1)
                {
                    if (MoldRight == null)
                    {
                        QuantityMolds--;

                        ContentsRight = null;
                        FillLevelRight = 0;
                        ShatteredRight = false;
                        return false;
                    }

                    MoldLeft = MoldRight;
                    MoldMeshLeft = MoldMeshRight;
                    ContentsLeft = ContentsRight;
                    FillLevelLeft = FillLevelRight;
                    ShatteredLeft = ShatteredRight;
                    Api.World.BlockAccessor.ExchangeBlock(MoldLeft.Block.BlockId, Pos);
                    MoldRight = null;
                    ContentsRight = null;
                    FillLevelRight = 0;
                    ShatteredRight = false;
                }

                QuantityMolds--;

                if (!byPlayer.InventoryManager.TryGiveItemstack(itemStack))
                {
                    Api.World.SpawnItemEntity(itemStack, Pos);
                }
                Api.World.Logger.Audit("{0} Took 1x{1} from Ingot mold at {2}.",
                    byPlayer.PlayerName,
                    itemStack.Collectible.Code,
                    Pos
                );
                if (QuantityMolds <= 0)
                {
                    Api.World.BlockAccessor.SetBlock(0, Pos);
                }
                else
                {
                    MoldRight = null;
                    MarkDirty(true);
                }

                if (placeSound != null)
                {
                    Api.World.PlaySoundAt(placeSound, Pos, -0.5, byPlayer, false);
                }

                return true;
            }

            return false;
        }

        protected bool TryPutMold(IPlayer byPlayer)
        {
            if (QuantityMolds >= 2) return false;

            QuantityMolds++;
            MoldRight = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Clone();

            if (MoldRight == null) return false;
            MoldRight.StackSize = 1;

            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack!.StackSize--;
                if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.StackSize == 0)
                {
                    byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack = null;
                }

                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
            }

            if (MoldRight.Block.Sounds?.Place is AssetLocation assetLoc)
            {
                Api.World.PlaySoundAt(assetLoc, Pos, -0.5, byPlayer, false);
            }

            if (Api.Side == EnumAppSide.Client) GenMeshes();

            MarkDirty(true);
            return true;
        }


        public void UpdateIngotRenderer()
        {
            if (ingotRenderer == null) return;

            if (BothShattered)
            {
                capi?.Event.UnregisterRenderer(ingotRenderer, EnumRenderStage.Opaque);
                return;
            }

            ingotRenderer.QuantityMolds = QuantityMolds;
            ingotRenderer.LevelLeft = ShatteredLeft ? 0 : FillLevelLeft;
            ingotRenderer.LevelRight = ShatteredRight ? 0 : FillLevelRight;

            if (ContentsLeft?.Collectible != null)
            {
                ingotRenderer.TextureNameLeft = new AssetLocation("block/metal/ingot/" + ContentsLeft.Collectible.LastCodePart() + ".png");
            } else
            {
                ingotRenderer.TextureNameLeft = null;
            }
            if (ContentsRight?.Collectible != null)
            {
                ingotRenderer.TextureNameRight = new AssetLocation("block/metal/ingot/" + ContentsRight.Collectible.LastCodePart() + ".png");
            } else
            {
                ingotRenderer.TextureNameRight = null;
            }
        }

        public void ReceiveLiquidMetal(ItemStack metal, ref int amount, float temperature)
        {
            if (lastPouringMarkdirtyMs + 500 < Api.World.ElapsedMilliseconds)
            {
                MarkDirty(true);
                lastPouringMarkdirtyMs = Api.World.ElapsedMilliseconds + 500;
            }

            if (!SelectedIsFull && (SelectedContents == null || metal.Collectible.Equals(SelectedContents, metal, GlobalConstants.IgnoredStackAttributes)))
            {
                if (SelectedContents == null)
                {
                    SelectedContents = metal.Clone();
                    SelectedContents.ResolveBlockOrItem(Api.World);
                    SelectedContents.Collectible.SetTemperature(Api.World, SelectedContents, temperature, false);
                    SelectedContents.StackSize = 1;
                    (SelectedContents.Attributes["temperature"] as ITreeAttribute)?.SetFloat("cooldownSpeed", 300);
                }
                else
                {
                    SelectedContents.Collectible.SetTemperature(Api.World, SelectedContents, temperature, false);
                }

                int amountToFill = Math.Min(amount, RequiredUnits - SelectedFillLevel);
                SelectedFillLevel += amountToFill;
                amount -= amountToFill;
                UpdateIngotRenderer();
            }
        }

        public void OnPourOver()
        {
            MarkDirty(true);
        }



        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (ingotRenderer != null)
            {
                ingotRenderer.Dispose();
                ingotRenderer = null;
            }

        }


        #region Mesh gen
        ITexPositionSource? tmpTextureSource;
        AssetLocation? metalTexLoc;
        public Size2i AtlasSize => capi?.BlockTextureAtlas.Size ?? new();
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (capi == null || tmpTextureSource == null) return new();

                if (textureCode == "metal")
                {
                    return capi.BlockTextureAtlas[metalTexLoc];
                }

                return tmpTextureSource[textureCode];
            }
        }

        MeshData? shatteredMeshLeft;
        MeshData? shatteredMeshRight;

        public static readonly Vec3f left = new(-4 / 16f, 0, 0);
        public static readonly Vec3f right = new(3 / 16f, 0, 0);

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            switch (QuantityMolds)
            {
                case 0: return true;
                case 1:
                    {
                        if (ShatteredLeft) EnsureShatteredMeshesLoaded();
                        var leftTfMat = Mat4f.Create();
                        Mat4f.Translate(leftTfMat, leftTfMat, 0.5f, 0f,0.5f);
                        Mat4f.RotateY(leftTfMat, leftTfMat, MeshAngle);
                        Mat4f.Translate(leftTfMat, leftTfMat, -0.5f, -0f,-0.5f);
                        mesher.AddMeshData(ShatteredLeft ? shatteredMeshLeft : MoldMeshLeft, leftTfMat);
                    }
                    break;
                case 2:
                    {
                        if (ShatteredLeft || ShatteredRight) EnsureShatteredMeshesLoaded();

                        var matrixfl = new Matrixf().Identity();

                        matrixfl
                            .Translate(0.5f, 0f,0.5f)
                            .RotateY(MeshAngle)
                            .Translate(-0.5f, -0f,-0.5f)
                            .Translate(left);

                        var matrixfr = new Matrixf().Identity();

                        matrixfr
                            .Translate(0.5f, 0f,0.5f)
                            .RotateY(MeshAngle)
                            .Translate(-0.5f, -0f,-0.5f)
                            .Translate(right);

                        mesher.AddMeshData(ShatteredLeft ? shatteredMeshLeft : MoldMeshLeft, matrixfl.Values);
                        mesher.AddMeshData(ShatteredRight ? shatteredMeshRight : MoldMeshRight, matrixfr.Values);
                    }
                    break;
            }

            return true;
        }

        private void EnsureShatteredMeshesLoaded()
        {
            if (ShatteredLeft && shatteredMeshLeft == null)
            {
                metalTexLoc = ContentsLeft == null ? new AssetLocation("block/transparent") : new AssetLocation("block/metal/ingot/" + ContentsLeft.Collectible.LastCodePart());
                capi?.Tesselator.TesselateShape("shatteredmold", getShatteredShape(MoldLeft?.Block ?? Block), out shatteredMeshLeft, this);
            }
            if (ShatteredRight && shatteredMeshRight == null)
            {
                metalTexLoc = ContentsRight == null ? new AssetLocation("block/transparent") : new AssetLocation("block/metal/ingot/" + ContentsRight.Collectible.LastCodePart());
                capi?.Tesselator.TesselateShape("shatteredmold", getShatteredShape(MoldRight?.Block ?? Block), out shatteredMeshRight, this);
            }
        }

        private Shape getShatteredShape(Block block)
        {
            tmpTextureSource = capi?.Tesselator.GetTextureSource(block);
            var cshape = block.Attributes["shatteredShape"].AsObject<CompositeShape>();
            cshape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            return Shape.TryGet(Api, cshape.Base);
        }

        private void GenMeshes()
        {
            MoldMeshLeft = ObjectCacheUtil.GetOrCreate(Api, (MoldLeft?.Block ?? Block).Code.ToString(), () =>
            {

                ITexPositionSource tmpTextureSource = ((ICoreClientAPI)Api).Tesselator.GetTextureSource(MoldLeft?.Block ?? Block);
                ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;

                var shape = Shape.TryGet(Api, "shapes/block/clay/mold/ingot.json");
                mesher.TesselateShape((MoldLeft?.Block ?? Block).Code.ToString(), shape, out MeshData mesh, tmpTextureSource);

                return mesh;
            });
            if (MoldRight != null)
            {
                MoldMeshRight = ObjectCacheUtil.GetOrCreate(Api, (MoldRight?.Block ?? Block).Code.ToString(), () =>
                {

                    ITexPositionSource tmpTextureSource = ((ICoreClientAPI)Api).Tesselator.GetTextureSource(MoldRight?.Block ?? Block);
                    ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;

                    var shape = Shape.TryGet(Api, "shapes/block/clay/mold/ingot.json");
                    mesher.TesselateShape((MoldRight?.Block ?? Block).Code.ToString(), shape, out MeshData mesh, tmpTextureSource);

                    return mesh;
                });
            }
        }


        #endregion


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            ContentsLeft = tree.GetItemstack("contentsLeft");
            FillLevelLeft = tree.GetInt("fillLevelLeft");
            if (worldForResolving != null && ContentsLeft != null) ContentsLeft.ResolveBlockOrItem(worldForResolving);

            ContentsRight = tree.GetItemstack("contentsRight");
            FillLevelRight = tree.GetInt("fillLevelRight");
            if (worldForResolving != null && ContentsRight != null) ContentsRight.ResolveBlockOrItem(worldForResolving);

            QuantityMolds = tree.GetInt("quantityMolds");

            ShatteredLeft = tree.GetBool("shatteredLeft");
            ShatteredRight = tree.GetBool("shatteredRight");
            MeshAngle = tree.GetFloat("meshAngle");

            MoldLeft = tree.GetItemstack("moldLeft");
            if (worldForResolving != null && MoldLeft == null) // Set the values for pre 1.21 blocks
            {
                MoldLeft = new ItemStack(Block);
                if (ShatteredLeft) FillLevelLeft = (int)(FillLevelLeft * (0.7f + worldForResolving.Rand.NextDouble() * 0.1f));
                if (QuantityMolds > 1 && ShatteredRight) FillLevelRight = (int)(FillLevelRight * (0.7f + worldForResolving.Rand.NextDouble() * 0.1f));
            }
            if (worldForResolving != null && MoldLeft != null) MoldLeft.ResolveBlockOrItem(worldForResolving);
            MoldRight = tree.GetItemstack("moldRight", QuantityMolds > 1 ? new ItemStack(Block) : null);
            if (worldForResolving != null && MoldRight != null) MoldRight.ResolveBlockOrItem(worldForResolving);

            UpdateIngotRenderer();

            if (Api?.Side == EnumAppSide.Client)
            {
                GenMeshes();
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
            }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetItemstack("contentsLeft", ContentsLeft);
            tree.SetInt("fillLevelLeft", FillLevelLeft);

            tree.SetItemstack("contentsRight", ContentsRight);
            tree.SetInt("fillLevelRight", FillLevelRight);

            tree.SetInt("quantityMolds", QuantityMolds);

            tree.SetBool("shatteredLeft", ShatteredLeft);
            tree.SetBool("shatteredRight", ShatteredRight);
            tree.SetFloat("meshAngle", MeshAngle);

            tree.SetItemstack("moldLeft", MoldLeft != null ? MoldLeft : new ItemStack(Block));
            tree.SetItemstack("moldRight", MoldRight);
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (forPlayer?.CurrentBlockSelection is BlockSelection blockSel)
            {
                SetSelectedSide(blockSel.HitPosition);

                var metalContent = SelectedContents;
                if (!SelectedShattered)
                {
                    string state = SelectedIsLiquid ? Lang.Get("liquid") : (SelectedIsHardened ? Lang.Get("hardened") : Lang.Get("soft"));

                    string matkey = "material-" + metalContent?.Collectible.Variant["metal"];
                    string? mat = Lang.HasTranslation(matkey) ? Lang.Get(matkey) : metalContent?.GetName();
                    string temp = SelectedTemperature < 21 ? Lang.Get("Cold") : Lang.Get("{0}°C", (int)SelectedTemperature);
                    string contents = Lang.GetWithFallback("metalmold-blockinfo-unitsofmetal", "{0}/{4} units of {1} {2} ({3})", SelectedFillLevel, state, mat, temp, RequiredUnits);
                    dsc.AppendLine((metalContent != null ? contents : Lang.GetWithFallback("metalmold-blockinfo-emptymold", "0/{0} units of metal", RequiredUnits)) + "\n");
                }
                else if (GetSelectedStateAwareContents() is ItemStack shatteredStack)
                {
                    dsc.AppendLine(Lang.Get("metalmold-blockinfo-shatteredmetal", shatteredStack.StackSize, shatteredStack.GetName().ToLower()) + "\n");
                }
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            ingotRenderer?.Dispose();
        }



        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            ContentsLeft?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(ContentsLeft), blockIdMapping, itemIdMapping);
            ContentsRight?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(ContentsRight), blockIdMapping, itemIdMapping);
            MoldLeft?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(MoldLeft), blockIdMapping, itemIdMapping);
            MoldRight?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(MoldRight), blockIdMapping, itemIdMapping);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            if (ContentsLeft?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false) ContentsLeft = null;
            if (ContentsRight?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false) ContentsRight = null;
            if (MoldLeft?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false) MoldLeft = null;
            if (MoldRight?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false) MoldRight = null;
        }

        public void ShatterMoldSided(bool shatterRight)
        {
            Api.World.PlaySoundAt(new AssetLocation("sounds/block/ceramicbreak"), Pos, -0.4);
            (shatterRight ? MoldRight : MoldLeft)?.Block.SpawnBlockBrokenParticles(Pos);
            (shatterRight ? MoldRight : MoldLeft)?.Block.SpawnBlockBrokenParticles(Pos);
            (shatterRight ? ref ShatteredRight : ref ShatteredLeft) = true;
            MarkDirty(true);
        }

        public void CoolNow(float amountRel)
        {
            float leftbreakchance = Math.Max(0, amountRel - 0.6f) * Math.Max(TemperatureLeft - 250f, 0) / 5000f;
            float rightbreakchance = Math.Max(0, amountRel - 0.6f) * Math.Max(TemperatureRight - 250f, 0) / 5000f;

            if (Api.World.Rand.NextDouble() < leftbreakchance)
            {
                FillLevelLeft = (int)(FillLevelLeft * (0.7f + Api.World.Rand.NextDouble() * 0.1f));
                ContentsLeft?.Collectible.SetTemperature(Api.World, ContentsLeft, 20, false);
                ShatterMoldSided(false);
            } else
            {
                if (ContentsLeft != null)
                {
                    float temp = TemperatureLeft;
                    if (temp > 120)
                    {
                        Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), Pos, -0.4, null, false, 16);
                    }

                    ContentsLeft.Collectible.SetTemperature(Api.World, ContentsLeft, Math.Max(20, temp - amountRel * 20), false);
                    MarkDirty(true);
                }
            }

            if (Api.World.Rand.NextDouble() < rightbreakchance)
            {
                FillLevelRight = (int)(FillLevelRight * (0.7f + Api.World.Rand.NextDouble() * 0.1f));
                ContentsRight?.Collectible.SetTemperature(Api.World, ContentsRight, 20, false);
                ShatterMoldSided(true);
            }
            else
            {
                if (ContentsRight != null)
                {
                    float temp = TemperatureRight;
                    if (temp > 120)
                    {
                        Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), Pos, -0.5, null, false, 16);
                    }

                    ContentsRight.Collectible.SetTemperature(Api.World, ContentsRight, Math.Max(20, temp - amountRel * 20), false);
                    MarkDirty(true);
                }
            }

        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            MeshAngle = tree.GetFloat("meshAngle");
            MeshAngle -= degreeRotation * GameMath.DEG2RAD;
            tree.SetFloat("meshAngle", MeshAngle);
        }
    }
}
