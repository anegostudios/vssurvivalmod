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
        protected IngotMoldRenderer ingotRenderer;

        public MeshData MoldMesh;
        public ItemStack ContentsLeft;
        public ItemStack ContentsRight;
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
        public bool CanReceiveAny => Block.Code.Path.Contains("burned") && !BothShattered;

        bool BothShattered => ShatteredLeft && ShatteredRight;


        ICoreClientAPI capi;
        public float MeshAngle;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (ContentsLeft != null)
            {
                ContentsLeft.ResolveBlockOrItem(api.World);
            }
            if (ContentsRight != null)
            {
                ContentsRight.ResolveBlockOrItem(api.World);
            }

            capi = api as ICoreClientAPI;
            if (capi != null && !BothShattered)
            {
                capi.Event.RegisterRenderer(ingotRenderer = new IngotMoldRenderer(this, capi), EnumRenderStage.Opaque, "ingotmold");

                UpdateIngotRenderer();

                if (MoldMesh == null)
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
                || (ContentsLeft.Collectible.Equals(ContentsLeft, metal, GlobalConstants.IgnoredStackAttributes) && FillLevelLeft < RequiredUnits)
                || (ContentsRight.Collectible.Equals(ContentsRight, metal, GlobalConstants.IgnoredStackAttributes) && FillLevelRight < RequiredUnits)
            ;
        }



        public void BeginFill(Vec3d hitPosition)
        {
            SetSelectedSide(hitPosition);
        }

        private void SetSelectedSide(Vec3d hitPosition)
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


        public bool OnPlayerInteract(IPlayer byPlayer, BlockFacing onFace, Vec3d hitPosition)
        {
            if (BothShattered) return false;

            bool moldInHands = HasMoldInHands(byPlayer);
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

        public ItemStack GetStateAwareContentsLeft()
        {
            if (ContentsLeft != null && FillLevelLeft >= RequiredUnits)
            {
                if (ShatteredLeft)
                {
                    return GetShatteredStack(ContentsLeft, FillLevelLeft);
                }

                if (TemperatureLeft < 300)
                {
                    ItemStack outstack = ContentsLeft.Clone();
                    (outstack.Attributes["temperature"] as ITreeAttribute)?.RemoveAttribute("cooldownSpeed");
                    return outstack;
                }
            }

            return null;
        }

        /// <summary>
        /// Retrieves the molded ingot, will always return null for incomplete pours. Has a chance of returning metal bits if retrieved while still hot.
        /// </summary>
        /// <returns></returns>
        public ItemStack GetStateAwareContentsRight()
        {
            if (ContentsRight != null && FillLevelRight >= RequiredUnits)
            {
                if (ShatteredRight)
                {
                    return GetShatteredStack(ContentsRight, FillLevelRight);
                }

                if (TemperatureRight < 300)
                {
                    ItemStack outstack = ContentsRight.Clone();
                    (outstack.Attributes["temperature"] as ITreeAttribute)?.RemoveAttribute("cooldownSpeed");
                    return outstack;
                }
            }

            return null;
        }


        protected ItemStack GetShatteredStack(ItemStack contents, int fillLevel)
        {
            var shatteredStack = contents.Collectible.Attributes?["shatteredStack"].AsObject<JsonItemStack>();
            if (shatteredStack != null)
            {
                shatteredStack.Resolve(Api.World, "shatteredStack for" + contents.Collectible.Code);
                if (shatteredStack.ResolvedItemstack != null)
                {
                    var stack = shatteredStack.ResolvedItemstack;
                    stack.StackSize = (int)(fillLevel / 5f * (0.7f + Api.World.Rand.NextDouble() * 0.1f));
                    return stack;
                }
            }

            return null;
        }


        protected bool TryTakeIngot(IPlayer byPlayer, Vec3d hitPosition)
        {
            if (Api is ICoreServerAPI) MarkDirty();

            ItemStack leftStack = !IsHardenedLeft ? null : GetStateAwareContentsLeft();
            SetSelectedSide(hitPosition);
            if (leftStack != null && (!IsRightSideSelected || QuantityMolds == 1) && !ShatteredLeft)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos, -0.5, byPlayer, false);
                if (Api is ICoreServerAPI)
                {
                    if (!byPlayer.InventoryManager.TryGiveItemstack(leftStack))
                    {
                        Api.World.SpawnItemEntity(leftStack, Pos.ToVec3d().Add(0.5, 0.2, 0.5));
                    }
                    Api.World.Logger.Audit("{0} Took 1x{1} from Ingot mold at {2}.",
                        byPlayer.PlayerName,
                        leftStack.Collectible.Code,
                        Pos
                    );

                    ContentsLeft = null;
                    FillLevelLeft = 0;
                }

                return true;
            }

            ItemStack rightStack = !IsHardenedRight ? null : GetStateAwareContentsRight();
            if (rightStack != null && IsRightSideSelected && !ShatteredRight)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos, -0.5, byPlayer, false);
                if (Api is ICoreServerAPI)
                {
                    if (!byPlayer.InventoryManager.TryGiveItemstack(rightStack))
                    {
                        Api.World.SpawnItemEntity(rightStack, Pos.ToVec3d().Add(0.5, 0.2, 0.5));
                    }
                    Api.World.Logger.Audit("{0} Took 1x{1} from Ingot mold at {2}.",
                        byPlayer.PlayerName,
                        rightStack.Collectible.Code,
                        Pos
                    );

                    ContentsRight = null;
                    FillLevelRight = 0;
                }

                return true;
            }



            return false;
        }

        protected bool TryTakeMold(IPlayer byPlayer, Vec3d hitPosition)
        {
            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeSlot.Itemstack != null && !(activeSlot.Itemstack.Collectible is BlockToolMold)) return false;
            if (FillLevelLeft != 0 || FillLevelRight != 0) return false;

            var itemStack = new ItemStack(Block);
            if (FillLevelLeft == 0 && !ShatteredLeft)
            {
                QuantityMolds--;
                if (ingotRenderer != null) ingotRenderer.QuantityMolds = QuantityMolds;

                if (!byPlayer.InventoryManager.TryGiveItemstack(itemStack))
                {
                    Api.World.SpawnItemEntity(itemStack, Pos);
                }
                Api.World.Logger.Audit("{0} Took 1x{1} from Ingot mold at {2}.",
                    byPlayer.PlayerName,
                    itemStack.Collectible.Code,
                    Pos
                );
                if (QuantityMolds == 0)
                {
                    Api.World.BlockAccessor.SetBlock(0, Pos);
                } else
                {
                    MarkDirty(true);
                }

                if (Block.Sounds?.Place != null)
                {
                    Api.World.PlaySoundAt(Block.Sounds.Place, Pos, -0.5, byPlayer, false);
                }

                return true;
            }

            if (FillLevelRight == 0 && !ShatteredRight)
            {
                QuantityMolds--;
                if (ingotRenderer != null) ingotRenderer.QuantityMolds = QuantityMolds;

                if (!byPlayer.InventoryManager.TryGiveItemstack(itemStack))
                {
                    Api.World.SpawnItemEntity(itemStack, Pos);
                }
                Api.World.Logger.Audit("{0} Took 1x{1} from Ingot mold at {2}.",
                    byPlayer.PlayerName,
                    itemStack.Collectible.Code,
                    Pos
                );
                if (QuantityMolds == 0)
                {
                    Api.World.BlockAccessor.SetBlock(0, Pos);
                } else
                {
                    MarkDirty(true);
                }

                if (Block.Sounds?.Place != null)
                {
                    Api.World.PlaySoundAt(Block.Sounds.Place, Pos, -0.5, byPlayer, false);
                }

                return true;
            }

            return false;
        }

        protected bool TryPutMold(IPlayer byPlayer)
        {
            if (QuantityMolds >= 2) return false;

            QuantityMolds++;
            if (ingotRenderer != null) ingotRenderer.QuantityMolds = QuantityMolds;

            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.StackSize--;
                if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.StackSize == 0)
                {
                    byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack = null;
                }

                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
            }

            if (Block.Sounds?.Place != null)
            {
                Api.World.PlaySoundAt(Block.Sounds.Place, Pos, -0.5, byPlayer, false);
            }

            MarkDirty(true);
            return true;
        }



        protected bool HasMoldInHands(IPlayer byPlayer)
        {
            return
                byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack != null &&
                byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible == Block
            ;
        }

        public void UpdateIngotRenderer()
        {
            if (ingotRenderer == null) return;

            if (BothShattered)
            {
                capi.Event.UnregisterRenderer(ingotRenderer, EnumRenderStage.Opaque);
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

            if ((QuantityMolds == 1 || !IsRightSideSelected) && FillLevelLeft < RequiredUnits && (ContentsLeft == null || metal.Collectible.Equals(ContentsLeft, metal, GlobalConstants.IgnoredStackAttributes)))
            {
                if (ContentsLeft == null)
                {
                    ContentsLeft = metal.Clone();
                    ContentsLeft.ResolveBlockOrItem(Api.World);
                    ContentsLeft.Collectible.SetTemperature(Api.World, ContentsLeft, temperature, false);
                    ContentsLeft.StackSize = 1;
                    (ContentsLeft.Attributes["temperature"] as ITreeAttribute)?.SetFloat("cooldownSpeed", 300);
                } else
                {
                    ContentsLeft.Collectible.SetTemperature(Api.World, ContentsLeft, temperature, false);
                }

                int amountToFill = Math.Min(amount, RequiredUnits - FillLevelLeft);
                FillLevelLeft += amountToFill;
                amount -= amountToFill;
                UpdateIngotRenderer();
                return;
            }

            if (IsRightSideSelected && QuantityMolds > 1 && FillLevelRight < RequiredUnits && (ContentsRight == null || metal.Collectible.Equals(ContentsRight, metal, GlobalConstants.IgnoredStackAttributes)))
            {
                if (ContentsRight == null)
                {
                    ContentsRight = metal.Clone();
                    ContentsRight.ResolveBlockOrItem(Api.World);
                    ContentsRight.Collectible.SetTemperature(Api.World, ContentsRight, temperature, false);
                    ContentsRight.StackSize = 1;
                    (ContentsRight.Attributes["temperature"] as ITreeAttribute)?.SetFloat("cooldownSpeed", 300);
                } else
                {
                    ContentsRight.Collectible.SetTemperature(Api.World, ContentsRight, temperature, false);
                }

                int amountToFill = Math.Min(amount, RequiredUnits - FillLevelRight);
                FillLevelRight += amountToFill;
                amount -= amountToFill;
                UpdateIngotRenderer();

                return;
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
        ITexPositionSource tmpTextureSource;
        AssetLocation metalTexLoc;
        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "metal")
                {
                    return capi.BlockTextureAtlas[metalTexLoc];
                }

                return tmpTextureSource[textureCode];
            }
        }

        MeshData shatteredMeshLeft;
        MeshData shatteredMeshRight;

        public static Vec3f left = new Vec3f(-4 / 16f, 0, 0);
        public static Vec3f right = new Vec3f(3 / 16f, 0, 0);

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
                        mesher.AddMeshData(ShatteredLeft ? shatteredMeshLeft : MoldMesh, leftTfMat);
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

                        mesher.AddMeshData(ShatteredLeft ? shatteredMeshLeft : MoldMesh, matrixfl.Values);
                        mesher.AddMeshData(ShatteredRight ? shatteredMeshRight : MoldMesh, matrixfr.Values);
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
                capi.Tesselator.TesselateShape("shatteredmold", getShatteredShape(), out shatteredMeshLeft, this);
            }
            if (ShatteredRight && shatteredMeshRight == null)
            {
                metalTexLoc = ContentsRight == null ? new AssetLocation("block/transparent") : new AssetLocation("block/metal/ingot/" + ContentsRight.Collectible.LastCodePart());
                capi.Tesselator.TesselateShape("shatteredmold", getShatteredShape(), out shatteredMeshRight, this);
            }
        }

        private Shape getShatteredShape()
        {
            tmpTextureSource = capi.Tesselator.GetTextureSource(Block);
            var cshape = Block.Attributes["shatteredShape"].AsObject<CompositeShape>();
            cshape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            return Shape.TryGet(Api, cshape.Base);
        }

        private void GenMeshes()
        {
            MoldMesh = ObjectCacheUtil.GetOrCreate(Api, "ingotmold", () =>
            {
                MeshData mesh;

                ITexPositionSource tmpTextureSource = ((ICoreClientAPI)Api).Tesselator.GetTextureSource(Block);
                ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;

                var shape = Shape.TryGet(Api, "shapes/block/clay/mold/ingot.json");
                mesher.TesselateShape("ingotmold", shape, out mesh, tmpTextureSource);

                return mesh;
            });
        }


        #endregion


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            ContentsLeft = tree.GetItemstack("contentsLeft");
            FillLevelLeft = tree.GetInt("fillLevelLeft");
            if (Api?.World != null && ContentsLeft != null) ContentsLeft.ResolveBlockOrItem(Api.World);

            ContentsRight = tree.GetItemstack("contentsRight");
            FillLevelRight = tree.GetInt("fillLevelRight");
            if (Api?.World != null && ContentsRight != null) ContentsRight.ResolveBlockOrItem(Api.World);

            QuantityMolds = tree.GetInt("quantityMolds");

            ShatteredLeft = tree.GetBool("shatteredLeft");
            ShatteredRight = tree.GetBool("shatteredRight");
            MeshAngle = tree.GetFloat("meshAngle");

            UpdateIngotRenderer();

            if (Api?.Side == EnumAppSide.Client)
            {
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
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            string contents = "";

            if (BothShattered)
            {
                dsc.AppendLine(Lang.Get("Has shattered."));
                return;
            }

            if (ContentsLeft != null)
            {
                if (ShatteredLeft)
                {
                    dsc.AppendLine(Lang.Get("Has shattered."));
                }
                else
                {
                    string mat = ContentsLeft.Collectible?.Variant["metal"];
                    string contentsLocalized = mat == null ? ContentsLeft.GetName() : Lang.Get("material-" + mat);
                    string state = IsLiquidLeft ? Lang.Get("liquid") : (IsHardenedLeft ? Lang.Get("hardened") : Lang.Get("soft"));
                    string temp = TemperatureLeft < 21 ? Lang.Get("Cold") : Lang.Get("{0}°C", (int)TemperatureLeft);
                    contents = Lang.Get("{0} units of {1} {2} ({3})", FillLevelLeft, state, contentsLocalized, temp) + "\n";
                }
            }

            if (ContentsRight != null)
            {
                if (ShatteredRight)
                {
                    dsc.AppendLine(Lang.Get("Has shattered."));
                }
                else
                {
                    string mat = ContentsRight.Collectible?.Variant["metal"];
                    string contentsLocalized = mat == null ? ContentsRight.GetName() : Lang.Get("material-" + mat);
                    string state = IsLiquidRight ? Lang.Get("liquid") : (IsHardenedRight ? Lang.Get("hardened") : Lang.Get("soft"));
                    string temp = TemperatureRight < 21 ? Lang.Get("Cold") : Lang.Get("{0}°C", (int)TemperatureRight);
                    contents += Lang.Get("{0} units of {1} {2} ({3})", FillLevelRight, state, contentsLocalized, temp) + "\n";
                }
            }

            dsc.AppendLine(contents.Length == 0 ? Lang.Get("Empty") : contents);
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
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            if (ContentsLeft?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false)
            {
                ContentsLeft = null;
            }

            if (ContentsRight?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false)
            {
                ContentsRight = null;
            }
        }

        public void CoolNow(float amountRel)
        {
            float leftbreakchance = Math.Max(0, amountRel - 0.6f) * Math.Max(TemperatureLeft - 250f, 0) / 5000f;
            float rightbreakchance = Math.Max(0, amountRel - 0.6f) * Math.Max(TemperatureRight - 250f, 0) / 5000f;

            if (Api.World.Rand.NextDouble() < leftbreakchance)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ceramicbreak"), Pos, -0.4);
                ShatteredLeft = true;
                ContentsLeft.Collectible.SetTemperature(Api.World, ContentsLeft, 20, false);
                this.Block.SpawnBlockBrokenParticles(Pos);
                this.Block.SpawnBlockBrokenParticles(Pos);
                MarkDirty(true);
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
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ceramicbreak"), Pos, -0.4);
                ShatteredRight = true;
                ContentsRight.Collectible.SetTemperature(Api.World, ContentsRight, 20, false);
                this.Block.SpawnBlockBrokenParticles(Pos);
                this.Block.SpawnBlockBrokenParticles(Pos);
                MarkDirty(true);
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
