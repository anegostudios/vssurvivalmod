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
using Vintagestory.API.Server;

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

    public class BlockEntityIngotMold : BlockEntity, ILiquidMetalSink
    {
        internal MeshData[] meshesByQuantity;

        IngotMoldRenderer ingotRenderer;

        public ItemStack contentsLeft;
        public ItemStack contentsRight;

        public int fillLevelLeft = 0;
        public int fillLevelRight = 0;

        public int quantityMolds = 1;
        public bool fillSide;

        long lastPouringMarkdirtyMs;

        public float TemperatureLeft
        {
            get { return contentsLeft.Collectible.GetTemperature(Api.World, contentsLeft); }
        }
        public float TemperatureRight
        {
            get { return contentsRight.Collectible.GetTemperature(Api.World, contentsRight); }
        }

        public bool IsHardenedLeft
        {
            get {
                return TemperatureLeft < 0.3f * contentsLeft?.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(contentsLeft));
            }
        }

        public bool IsHardenedRight
        {
            get
            {
                return TemperatureRight < 0.3f * contentsRight?.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(contentsRight));
            }
        }


        public bool IsLiquidLeft
        {
            get {
                return TemperatureLeft > 0.8f * contentsLeft?.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(contentsLeft));
            }
        }

        public bool IsLiquidRight
        {
            get
            {
                return TemperatureRight > 0.8f * contentsRight?.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(contentsRight));
            }
        }

        public bool IsFullLeft
        {
            get
            {
                return fillLevelLeft >= 100;
            }
        }

        public bool IsFullRight
        {
            get
            {
                return fillLevelRight >= 100;
            }
        }


        public bool CanReceiveAny
        {
            get { return Block.Code.Path.Contains("burned"); }
        }

        public bool CanReceive(ItemStack metal)
        {
            return 
                contentsLeft == null 
                || contentsRight == null
                || (contentsLeft.Collectible.Equals(contentsLeft, metal, GlobalConstants.IgnoredStackAttributes) && fillLevelLeft < 100)
                || (contentsRight.Collectible.Equals(contentsRight, metal, GlobalConstants.IgnoredStackAttributes) && fillLevelRight < 100)
            ;
        }

        public BlockEntityIngotMold()
        {

        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (contentsLeft != null)
            {
                contentsLeft.ResolveBlockOrItem(api.World);
            }
            if (contentsRight != null)
            {
                contentsRight.ResolveBlockOrItem(api.World);
            }

            if (api is ICoreClientAPI)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                capi.Event.RegisterRenderer(ingotRenderer = new IngotMoldRenderer(Pos, capi), EnumRenderStage.Opaque, "ingotmold");

                UpdateIngotRenderer();

                if (meshesByQuantity == null)
                {
                    GenMeshes();
                }
            }


            RegisterGameTickListener(OnGameTick, 50);
        }


        private void GenMeshes()
        {
            meshesByQuantity = new MeshData[2];
            
            ITexPositionSource tmpTextureSource = ((ICoreClientAPI)Api).Tesselator.GetTexSource(Block);
            ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;

            Shape shape = Api.Assets.TryGet("shapes/block/clay/mold/ingot-1middle.json").ToObject<Shape>();
            mesher.TesselateShape("ingotPile", shape, out meshesByQuantity[0], tmpTextureSource);

            shape = Api.Assets.TryGet("shapes/block/clay/mold/ingot-2.json").ToObject<Shape>();
            mesher.TesselateShape("ingotPile", shape, out meshesByQuantity[1], tmpTextureSource);
        }


        private void OnGameTick(float dt)
        {
            if (ingotRenderer != null)
            {
                ingotRenderer.QuantityMolds = quantityMolds;
                ingotRenderer.LevelLeft = fillLevelLeft;
                ingotRenderer.LevelRight = fillLevelRight;
            }

            if (contentsLeft != null && ingotRenderer != null)
            {
                ingotRenderer.TemperatureLeft = Math.Min(1300, contentsLeft.Collectible.GetTemperature(Api.World, contentsLeft));
            }

            if (contentsRight != null && ingotRenderer != null)
            {
                ingotRenderer.TemperatureRight = Math.Min(1300, contentsRight.Collectible.GetTemperature(Api.World, contentsRight));
            }
        }



        
        public void BeginFill(Vec3d hitPosition)
        {
            fillSide = hitPosition.X >= 0.5f;
        }


        public bool OnPlayerInteract(IPlayer byPlayer, BlockFacing onFace, Vec3d hitPosition)
        {
            bool moldInHands = HasMoldInHands(byPlayer);
            bool sneaking = byPlayer.Entity.Controls.Sneak;
            
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

        public ItemStack GetLeftContents()
        {
            if (contentsLeft != null && fillLevelLeft >= 100 && IsHardenedLeft)
            {
                ItemStack outstack = contentsLeft.Clone();
                (outstack.Attributes["temperature"] as ITreeAttribute)?.RemoveAttribute("cooldownSpeed");
                return outstack;
            }

            return null;
        }

        public ItemStack GetRightContents()
        {
            if (contentsRight != null && fillLevelRight >= 100 && IsHardenedRight)
            {
                ItemStack outstack = contentsRight.Clone();
                (outstack.Attributes["temperature"] as ITreeAttribute)?.RemoveAttribute("cooldownSpeed");
                return outstack;
            }

            return null;
        }



        private bool TryTakeIngot(IPlayer byPlayer, Vec3d hitPosition)
        {
            if (Api is ICoreServerAPI) MarkDirty();
            
            ItemStack leftStack = GetLeftContents();
            if (leftStack != null && (hitPosition.X < 0.5f || quantityMolds == 1))
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos.X, Pos.Y, Pos.Z, byPlayer, false);
                if (Api is ICoreServerAPI)
                {
                    if (!byPlayer.InventoryManager.TryGiveItemstack(leftStack))
                    {
                        Api.World.SpawnItemEntity(leftStack, Pos.ToVec3d().Add(0.5, 0.2, 0.5));
                    }

                    contentsLeft = null;
                    fillLevelLeft = 0;
                }

                return true;
            }

            ItemStack rightStack = GetRightContents();
            if (rightStack != null && hitPosition.X >= 0.5f)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos.X, Pos.Y, Pos.Z, byPlayer, false);
                if (Api is ICoreServerAPI)
                {
                    if (!byPlayer.InventoryManager.TryGiveItemstack(rightStack))
                    {
                        Api.World.SpawnItemEntity(rightStack, Pos.ToVec3d().Add(0.5, 0.2, 0.5));
                    }

                    contentsRight = null;
                    fillLevelRight = 0;
                }

                return true;
            }



            return false;
        }


        private bool TryTakeMold(IPlayer byPlayer, Vec3d hitPosition)
        {
            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeSlot.Itemstack != null && !(activeSlot.Itemstack.Collectible is BlockToolMold)) return false;
            if (fillLevelLeft != 0 || fillLevelRight != 0) return false;

            if (fillLevelLeft == 0)
            {
                quantityMolds--;
                if (ingotRenderer != null) ingotRenderer.QuantityMolds = quantityMolds;

                

                if (!byPlayer.InventoryManager.TryGiveItemstack(new ItemStack(this.Block)))
                {
                    Api.World.SpawnItemEntity(new ItemStack(Block), Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
                if (quantityMolds == 0)
                {
                    Api.World.BlockAccessor.SetBlock(0, Pos);
                } else
                {
                    MarkDirty(true);
                }
                
                if (Block.Sounds?.Place != null)
                {
                    Api.World.PlaySoundAt(Block.Sounds.Place, Pos.X, Pos.Y, Pos.Z, byPlayer, false);
                }

                return true;
            }

            if (fillLevelRight == 0)
            {
                quantityMolds--;
                if (ingotRenderer != null) ingotRenderer.QuantityMolds = quantityMolds;

                if (!byPlayer.InventoryManager.TryGiveItemstack(new ItemStack(Block)))
                {
                    Api.World.SpawnItemEntity(new ItemStack(Block), Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
                if (quantityMolds == 0)
                {
                    Api.World.BlockAccessor.SetBlock(0, Pos);
                } else
                {
                    MarkDirty(true);
                }

                if (Block.Sounds?.Place != null)
                {
                    Api.World.PlaySoundAt(Block.Sounds.Place, Pos.X, Pos.Y, Pos.Z, byPlayer, false);
                }

                

                return true;
            }

            return false;
        }

        private bool TryPutMold(IPlayer byPlayer)
        {
            if (quantityMolds >= 2) return false;

            quantityMolds++;
            if (ingotRenderer != null) ingotRenderer.QuantityMolds = quantityMolds;

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
                Api.World.PlaySoundAt(Block.Sounds.Place, Pos.X, Pos.Y, Pos.Z, byPlayer, false);
            }

            MarkDirty(true);
            return true;
        }


        private bool HasMoldInHands(IPlayer byPlayer)
        {
            return
                byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack != null &&
                byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible == Block
            ;
        }

        internal void UpdateIngotRenderer()
        {
            if (ingotRenderer == null) return;

            ingotRenderer.QuantityMolds = quantityMolds;
            ingotRenderer.LevelLeft = fillLevelLeft;
            ingotRenderer.LevelRight = fillLevelRight;
                    

            if (contentsLeft?.Collectible != null)
            {
                ingotRenderer.TextureNameLeft = new AssetLocation("block/metal/ingot/" + contentsLeft.Collectible.LastCodePart() + ".png");
            } else
            {
                ingotRenderer.TextureNameLeft = null;
            }
            if (contentsRight?.Collectible != null)
            {
                ingotRenderer.TextureNameRight = new AssetLocation("block/metal/ingot/" + contentsRight.Collectible.LastCodePart() + ".png");
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

            if ((quantityMolds == 1 || !fillSide) && fillLevelLeft < 100 && (contentsLeft == null || metal.Collectible.Equals(contentsLeft, metal, GlobalConstants.IgnoredStackAttributes)))
            {
                if (contentsLeft == null)
                {
                    contentsLeft = metal.Clone();
                    contentsLeft.ResolveBlockOrItem(Api.World);
                    contentsLeft.Collectible.SetTemperature(Api.World, contentsLeft, temperature, false);
                    contentsLeft.StackSize = 1;
                    (contentsLeft.Attributes["temperature"] as ITreeAttribute)?.SetFloat("cooldownSpeed", 300);
                } else
                {
                    contentsLeft.Collectible.SetTemperature(Api.World, contentsLeft, temperature, false);
                }

                int amountToFill = Math.Min(amount, 100 - fillLevelLeft);
                fillLevelLeft += amountToFill;
                amount -= amountToFill;
                UpdateIngotRenderer();
                return;
            }

            if (fillSide && quantityMolds > 1 && fillLevelRight < 100 && (contentsRight == null || metal.Collectible.Equals(contentsRight, metal, GlobalConstants.IgnoredStackAttributes)))
            {
                if (contentsRight == null)
                {
                    contentsRight = metal.Clone();
                    contentsRight.ResolveBlockOrItem(Api.World);
                    contentsRight.Collectible.SetTemperature(Api.World, contentsRight, temperature, false);
                    contentsRight.StackSize = 1;
                    (contentsRight.Attributes["temperature"] as ITreeAttribute)?.SetFloat("cooldownSpeed", 300);
                } else
                {
                    contentsRight.Collectible.SetTemperature(Api.World, contentsRight, temperature, false);
                }

                int amountToFill = Math.Min(amount, 100 - fillLevelRight);
                fillLevelRight += amountToFill;
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


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (quantityMolds == 0) return true;

            mesher.AddMeshData(meshesByQuantity[quantityMolds - 1]);

            return true;
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            contentsLeft = tree.GetItemstack("contentsLeft");
            fillLevelLeft = tree.GetInt("fillLevelLeft");
            if (Api?.World != null && contentsLeft != null) contentsLeft.ResolveBlockOrItem(Api.World);

            contentsRight = tree.GetItemstack("contentsRight");
            fillLevelRight = tree.GetInt("fillLevelRight");
            if (Api?.World != null && contentsRight != null) contentsRight.ResolveBlockOrItem(Api.World);

            quantityMolds = tree.GetInt("quantityMolds");

            UpdateIngotRenderer();

            if (Api?.Side == EnumAppSide.Client)
            {
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
            }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetItemstack("contentsLeft", contentsLeft);
            tree.SetInt("fillLevelLeft", fillLevelLeft);

            tree.SetItemstack("contentsRight", contentsRight);
            tree.SetInt("fillLevelRight", fillLevelRight);

            tree.SetInt("quantityMolds", quantityMolds);
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            string contents = "";

            if (contentsLeft != null)
            {
                string state = IsLiquidLeft ? Lang.Get("liquid") : (IsHardenedLeft ? Lang.Get("hardened") : Lang.Get("soft"));
                string temp = TemperatureLeft < 21 ? Lang.Get("Cold") : Lang.Get("{0}°C", (int)TemperatureLeft);
                contents = Lang.Get("{0} units of {1} {2} ({3})", fillLevelLeft, state, contentsLeft.GetName(), temp) + "\n";
            }

            if (contentsRight != null)
            {
                string state = IsLiquidRight ? Lang.Get("liquid") : (IsHardenedRight ? Lang.Get("hardened") : Lang.Get("soft"));
                string temp = TemperatureRight < 21 ? Lang.Get("Cold") : Lang.Get("{0}°C", (int)TemperatureRight);

                contents += Lang.Get("{0} units of {1} {2} ({3})", fillLevelRight, state, contentsRight.GetName(), temp) + "\n";
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
            contentsLeft?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(contentsLeft), blockIdMapping, itemIdMapping);
            contentsRight?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(contentsRight), blockIdMapping, itemIdMapping);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
        {
            if (contentsLeft?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false)
            {
                contentsLeft = null;
            }

            if (contentsRight?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false)
            {
                contentsRight = null;
            }
        }

    }
}
