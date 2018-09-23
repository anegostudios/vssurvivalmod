using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntityToolMold : BlockEntity, ILiquidMetalSink
    {
        ToolMoldRenderer renderer;

        public ItemStack metalContent;

        public int fillLevel = 0;
        public bool fillSide;

        Block block;

        int requiredUnits = 100;
        float fillHeight = 1;

        public float Temperature
        {
            get { return metalContent.Collectible.GetTemperature(api.World, metalContent); }
        }

        public bool IsSolid
        {
            get
            {
                return Temperature < 0.2f * metalContent?.Collectible.GetMeltingPoint(api.World, null, new DummySlot(metalContent));
            }
        }
        

        public bool CanReceiveAny
        {
            get { return block.Code.Path.Contains("burned"); }
        }

        public bool CanReceive(ItemStack metal)
        {
            return
                (metalContent == null || (metalContent.Collectible.Equals(metalContent, metal, GlobalConstants.IgnoredStackAttributes) && fillLevel < requiredUnits))
                && GetMoldedStack(metal) != null
            ;
        }

        public BlockEntityToolMold()
        {

        }

        Cuboidf[] fillQuadsByLevel = null;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (metalContent != null)
            {
                metalContent.ResolveBlockOrItem(api.World);
            }
            

            block = api.World.BlockAccessor.GetBlock(pos);
            if (block == null || block.Code == null) return;

            fillHeight = block.Attributes["fillHeight"].AsFloat(1);
            requiredUnits = block.Attributes["requiredUnits"].AsInt(100);

            if (block.Attributes["fillQuadsByLevel"].Exists)
            {
                fillQuadsByLevel = block.Attributes["fillQuadsByLevel"].AsObject<Cuboidf[]>();
            }
            

            if (fillQuadsByLevel == null)
            {
                fillQuadsByLevel = new Cuboidf[] { new Cuboidf(2, 0, 2, 14, 0, 14), };
            }

            if (api is ICoreClientAPI)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;



                capi.Event.RegisterRenderer(renderer = new ToolMoldRenderer(pos, capi, fillQuadsByLevel), EnumRenderStage.Opaque);
                
                UpdateRenderer();
            }

            RegisterGameTickListener(OnGameTick, 50);
        }
        

        private void OnGameTick(float dt)
        {
            if (renderer != null)
            {
                renderer.Level = (float)fillLevel * fillHeight / requiredUnits;
            }

            if (metalContent != null && renderer != null)
            {
                renderer.Temperature = Math.Min(1300, metalContent.Collectible.GetTemperature(api.World, metalContent));
            }
            
        }




        public void BeginFill(Vec3d hitPosition)
        {
            fillSide = hitPosition.X >= 0.5f;
        }


        public bool OnPlayerInteract(IPlayer byPlayer, BlockFacing onFace, Vec3d hitPosition)
        {
            bool sneaking = byPlayer.Entity.Controls.Sneak;

            if (!sneaking)
            {
                if (byPlayer.Entity.Controls.HandUse != EnumHandInteract.None) return false;

                bool handled = TryTakeContents(byPlayer);

                if (!handled && fillLevel == 0)
                {
                    ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                    if (activeSlot.Itemstack == null || activeSlot.Itemstack.Collectible is BlockToolMold)
                    {
                        if (!byPlayer.InventoryManager.TryGiveItemstack(new ItemStack(block)))
                        {
                            api.World.SpawnItemEntity(new ItemStack(block), pos.ToVec3d().Add(0.5, 0.2, 0.5));
                        }

                        api.World.BlockAccessor.SetBlock(0, pos);

                        if (block.Sounds?.Place != null)
                        {
                            api.World.PlaySoundAt(block.Sounds.Place, pos.X, pos.Y, pos.Z, byPlayer, false);
                        }

                        handled = true;
                    }

                    
                }

                return handled;
            }
            

            return false;
        }

        private bool TryTakeContents(IPlayer byPlayer)
        {
            if (api is ICoreServerAPI) MarkDirty();

            if (metalContent != null && fillLevel >= requiredUnits && IsSolid)
            {
                api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), pos.X, pos.Y, pos.Z, byPlayer, false);

                if (api is ICoreServerAPI)
                {
                    ItemStack outstack = GetReadyMoldedStack();

                    if (outstack != null)
                    {
                        outstack.Collectible.SetTemperature(api.World, outstack, metalContent.Collectible.GetTemperature(api.World, metalContent));

                        if (!byPlayer.InventoryManager.TryGiveItemstack(outstack))
                        {
                            api.World.SpawnItemEntity(outstack, pos.ToVec3d().Add(0.5, 0.2, 0.5));
                        }
                    }

                    metalContent = null;
                    fillLevel = 0;
                }

                UpdateRenderer();

                return true;
            }
            

            return false;
        }



        



        internal void UpdateRenderer()
        {
            if (renderer == null) return;

            renderer.Level = (float)fillLevel * fillHeight / requiredUnits;


            if (metalContent?.Collectible != null)
            {
                renderer.TextureName = new AssetLocation("block/metal/ingot/" + metalContent.Collectible.LastCodePart() + ".png");
            }
            else
            {
                renderer.TextureName = null;
            }
        }

        public void ReceiveLiquidMetal(ItemStack metal, ref int amount, float temperature)
        {
            if (fillLevel < requiredUnits && (metalContent == null || metal.Collectible.Equals(metalContent, metal, GlobalConstants.IgnoredStackAttributes)))
            {
                if (metalContent == null)
                {
                    metalContent = metal.Clone();
                    metalContent.ResolveBlockOrItem(api.World);
                    metalContent.Collectible.SetTemperature(api.World, metalContent, temperature, false);
                    metalContent.StackSize = 1;
                    (metalContent.Attributes["temperature"] as ITreeAttribute)?.SetFloat("cooldownSpeed", 300);
                }
                else
                {
                    metalContent.Collectible.SetTemperature(api.World, metalContent, temperature, false);
                }

                int amountToFill = Math.Min(amount, requiredUnits - fillLevel);
                fillLevel += amountToFill;
                amount -= amountToFill;
                UpdateRenderer();
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

            if (renderer != null)
            {
                renderer.Unregister();
                renderer = null;
            }
        }


        public ItemStack GetReadyMoldedStack()
        {
            if (fillLevel < requiredUnits || !IsSolid) return null;
            if (metalContent?.Collectible == null) return null;

            ItemStack stack = GetMoldedStack(metalContent);

            return stack;
        }

        public ItemStack GetMoldedStack(ItemStack fromMetal)
        { 
            string itemclass = block.Attributes["drop"]["class"].AsString();
            string code = block.Attributes["drop"]["code"].AsString();

            string metaltype = fromMetal.Collectible.LastCodePart();
            string tooltype = block.LastCodePart();
            code = code.Replace("{tooltype}", tooltype).Replace("{metal}", metaltype);

            ItemStack outstack = null;

            if (itemclass == "Block")
            {
                Block block = api.World.GetBlock(new AssetLocation(code));
                
                if (block == null)
                {
                    //api.World.Logger.Error("Tool mold block drop " + code + " does not exist!");
                    return null;
                }

                outstack = new ItemStack(block);
            }
            else
            {
                Item item = api.World.GetItem(new AssetLocation(code));
                if (item == null)
                {
                    //api.World.Logger.Error("Tool mold item drop " + code + " does not exist!");
                    return null;
                }

                outstack = new ItemStack(item);
            }

            return outstack;
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolve)
        {
            base.FromTreeAtributes(tree, worldForResolve);

            metalContent = tree.GetItemstack("contents");
            fillLevel = tree.GetInt("fillLevel");
            if (api?.World != null && metalContent != null) metalContent.ResolveBlockOrItem(api.World);
            
            UpdateRenderer();

            if (api?.Side == EnumAppSide.Client)
            {
                api.World.BlockAccessor.MarkBlockDirty(pos);
            }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetItemstack("contents", metalContent);
            tree.SetInt("fillLevel", fillLevel);
        }


        public override string GetBlockInfo(IPlayer forPlayer)
        {
            string contents = "";

            if (this.metalContent != null)
            {
                string temp = Temperature < 21 ? Lang.Get("Cold") : Lang.Get("{0} °C", (int)Temperature);
                contents = string.Format("{0}/{4} units of {1} {2} ({3})\n", fillLevel, IsSolid ? "solidified" : "liquid", this.metalContent.GetName(), temp, requiredUnits);
            }
            

            return contents.Length == 0 ? "Empty" : contents;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Unregister();
        }


        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            metalContent?.Collectible.OnStoreCollectibleMappings(api.World, new DummySlot(metalContent), blockIdMapping, itemIdMapping);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping)
        {
            if (metalContent?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == null)
            {
                metalContent = null;
            }
        }


    }
}
