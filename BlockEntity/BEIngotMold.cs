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

    public class BlockEntityIngotMold : BlockEntity, IBlockShapeSupplier, ILiquidMetalSink
    {
        internal MeshData[] meshesByQuantity;

        IngotMoldRenderer ingotRenderer;

        public ItemStack contentsLeft;
        public ItemStack contentsRight;

        public int fillLevelLeft = 0;
        public int fillLevelRight = 0;

        public int quantityMolds = 1;
        public bool fillSide;

        Block block;
        long lastPouringMarkdirtyMs;

        public float TemperatureLeft
        {
            get { return contentsLeft.Collectible.GetTemperature(api.World, contentsLeft); }
        }
        public float TemperatureRight
        {
            get { return contentsRight.Collectible.GetTemperature(api.World, contentsRight); }
        }

        public bool IsSolidLeft
        {
            get {
                return TemperatureLeft < 0.2f * contentsLeft?.Collectible.GetMeltingPoint(api.World, null, new DummySlot(contentsLeft));
            }
        }

        public bool IsSolidRight
        {
            get
            {
                return TemperatureRight < 0.2f * contentsRight?.Collectible.GetMeltingPoint(api.World, null, new DummySlot(contentsRight));
            }
        }

        public bool CanReceiveAny
        {
            get { return block.Code.Path.Contains("burned"); }
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

            block = api.World.BlockAccessor.GetBlock(pos);

            if (api is ICoreClientAPI)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                capi.Event.RegisterRenderer(ingotRenderer = new IngotMoldRenderer(pos, capi), EnumRenderStage.Opaque);

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
            
            ITexPositionSource tmpTextureSource = ((ICoreClientAPI)api).Tesselator.GetTexSource(block);
            ITesselatorAPI mesher = ((ICoreClientAPI)api).Tesselator;

            Shape shape = api.Assets.TryGet("shapes/block/clay/mold/ingot-1middle.json").ToObject<Shape>();
            mesher.TesselateShape("ingotPile", shape, out meshesByQuantity[0], tmpTextureSource);

            shape = api.Assets.TryGet("shapes/block/clay/mold/ingot-2.json").ToObject<Shape>();
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
                ingotRenderer.TemperatureLeft = Math.Min(1300, contentsLeft.Collectible.GetTemperature(api.World, contentsLeft));
            }

            if (contentsRight != null && ingotRenderer != null)
            {
                ingotRenderer.TemperatureRight = Math.Min(1300, contentsRight.Collectible.GetTemperature(api.World, contentsRight));
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
            if (contentsLeft != null && fillLevelLeft >= 100 && IsSolidLeft)
            {
                ItemStack outstack = contentsLeft.Clone();
                (outstack.Attributes["temperature"] as ITreeAttribute)?.RemoveAttribute("cooldownSpeed");
                return outstack;
            }

            return null;
        }

        public ItemStack GetRightContents()
        {
            if (contentsRight != null && fillLevelRight >= 100 && IsSolidRight)
            {
                ItemStack outstack = contentsRight.Clone();
                (outstack.Attributes["temperature"] as ITreeAttribute)?.RemoveAttribute("cooldownSpeed");
                return outstack;
            }

            return null;
        }



        private bool TryTakeIngot(IPlayer byPlayer, Vec3d hitPosition)
        {
            if (api is ICoreServerAPI) MarkDirty();
            

            ItemStack leftStack = GetLeftContents();
            if (leftStack != null && hitPosition.X < 0.5f)
            {
                api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), pos.X, pos.Y, pos.Z, byPlayer, false);
                if (api is ICoreServerAPI)
                {
                    if (!byPlayer.InventoryManager.TryGiveItemstack(leftStack))
                    {
                        api.World.SpawnItemEntity(leftStack, pos.ToVec3d().Add(0.5, 0.2, 0.5));
                    }

                    contentsLeft = null;
                    fillLevelLeft = 0;
                }

                return true;
            }

            ItemStack rightStack = GetRightContents();
            if (rightStack != null && hitPosition.X >= 0.5f)
            {
                api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), pos.X, pos.Y, pos.Z, byPlayer, false);
                if (api is ICoreServerAPI)
                {
                    if (!byPlayer.InventoryManager.TryGiveItemstack(rightStack))
                    {
                        api.World.SpawnItemEntity(rightStack, pos.ToVec3d().Add(0.5, 0.2, 0.5));
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

                

                if (!byPlayer.InventoryManager.TryGiveItemstack(new ItemStack(this.block)))
                {
                    api.World.SpawnItemEntity(new ItemStack(block), pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
                if (quantityMolds == 0)
                {
                    api.World.BlockAccessor.SetBlock(0, pos);
                } else
                {
                    MarkDirty(true);
                }
                
                if (block.Sounds?.Place != null)
                {
                    api.World.PlaySoundAt(block.Sounds.Place, pos.X, pos.Y, pos.Z, byPlayer, false);
                }

                return true;
            }

            if (fillLevelRight == 0)
            {
                quantityMolds--;
                if (ingotRenderer != null) ingotRenderer.QuantityMolds = quantityMolds;

                if (!byPlayer.InventoryManager.TryGiveItemstack(new ItemStack(block)))
                {
                    api.World.SpawnItemEntity(new ItemStack(block), pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
                if (quantityMolds == 0)
                {
                    api.World.BlockAccessor.SetBlock(0, pos);
                } else
                {
                    MarkDirty(true);
                }

                if (block.Sounds?.Place != null)
                {
                    api.World.PlaySoundAt(block.Sounds.Place, pos.X, pos.Y, pos.Z, byPlayer, false);
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

            if (block.Sounds?.Place != null)
            {
                api.World.PlaySoundAt(block.Sounds.Place, pos.X, pos.Y, pos.Z, byPlayer, false);
            }

            MarkDirty(true);
            return true;
        }


        private bool HasMoldInHands(IPlayer byPlayer)
        {
            return
                byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack != null &&
                byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible == block
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
            if (lastPouringMarkdirtyMs + 500 < api.World.ElapsedMilliseconds)
            {
                MarkDirty(true);
                lastPouringMarkdirtyMs = api.World.ElapsedMilliseconds + 500;
            }

            if ((quantityMolds == 1 || !fillSide) && fillLevelLeft < 100 && (contentsLeft == null || metal.Collectible.Equals(contentsLeft, metal, GlobalConstants.IgnoredStackAttributes)))
            {
                if (contentsLeft == null)
                {
                    contentsLeft = metal.Clone();
                    contentsLeft.ResolveBlockOrItem(api.World);
                    contentsLeft.Collectible.SetTemperature(api.World, contentsLeft, temperature, false);
                    contentsLeft.StackSize = 1;
                    (contentsLeft.Attributes["temperature"] as ITreeAttribute)?.SetFloat("cooldownSpeed", 300);
                } else
                {
                    contentsLeft.Collectible.SetTemperature(api.World, contentsLeft, temperature, false);
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
                    contentsRight.ResolveBlockOrItem(api.World);
                    contentsRight.Collectible.SetTemperature(api.World, contentsRight, temperature, false);
                    contentsRight.StackSize = 1;
                    (contentsRight.Attributes["temperature"] as ITreeAttribute)?.SetFloat("cooldownSpeed", 300);
                } else
                {
                    contentsRight.Collectible.SetTemperature(api.World, contentsRight, temperature, false);
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
                ingotRenderer.Unregister();
                ingotRenderer = null;
            }

        }


        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (quantityMolds == 0) return true;

            mesher.AddMeshData(meshesByQuantity[quantityMolds - 1]);

            return true;
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            contentsLeft = tree.GetItemstack("contentsLeft");
            fillLevelLeft = tree.GetInt("fillLevelLeft");
            if (api?.World != null && contentsLeft != null) contentsLeft.ResolveBlockOrItem(api.World);

            contentsRight = tree.GetItemstack("contentsRight");
            fillLevelRight = tree.GetInt("fillLevelRight");
            if (api?.World != null && contentsRight != null) contentsRight.ResolveBlockOrItem(api.World);

            quantityMolds = tree.GetInt("quantityMolds");

            UpdateIngotRenderer();

            if (api?.Side == EnumAppSide.Client)
            {
                api.World.BlockAccessor.MarkBlockDirty(pos);
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


        public override string GetBlockInfo(IPlayer forPlayer)
        {
            string contents = "";

            if (contentsLeft != null)
            {
                contents = string.Format("{0} units of {1} {2} ({3} °C)\n", fillLevelLeft, IsSolidLeft ? "solidified" : "liquid", contentsLeft.GetName(), (int)TemperatureLeft);
            }

            if (contentsRight != null)
            {
                contents += string.Format("{0} units of {1} {2} ({3} °C)\n", fillLevelRight, IsSolidRight ? "solidified" : "liquid", contentsRight.GetName(), (int)TemperatureRight);
            }

            return contents.Length == 0 ? "Empty" : contents;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            ingotRenderer?.Unregister();
        }

    }
}
