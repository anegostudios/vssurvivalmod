using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
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

        

        int requiredUnits = 100;
        float fillHeight = 1;

        public float Temperature
        {
            get { return metalContent?.Collectible.GetTemperature(Api.World, metalContent) ?? 0; }
        }

        public bool IsHardened
        {
            get
            {
                return Temperature < 0.3f * metalContent?.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(metalContent));
            }
        }

        public bool IsLiquid
        {
            get
            {
                return Temperature > 0.8f * metalContent?.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(metalContent));
            }
        }

        public bool IsFull
        {
            get
            {
                return fillLevel >= requiredUnits;
            }
        }

        public bool CanReceiveAny
        {
            get { return Block.Code.Path.Contains("burned"); }
        }

        public bool CanReceive(ItemStack metal)
        {
            return
                (metalContent == null || (metalContent.Collectible.Equals(metalContent, metal, GlobalConstants.IgnoredStackAttributes) && fillLevel < requiredUnits))
                && GetMoldedStacks(metal) != null && GetMoldedStacks(metal).Length > 0
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
                metalContent.ResolveBlockOrItem(Api.World);
            }
            

            if (Block == null || Block.Code == null || Block.Attributes == null) return;

            fillHeight = Block.Attributes["fillHeight"].AsFloat(1);
            requiredUnits = Block.Attributes["requiredUnits"].AsInt(100);

            if (Block.Attributes["fillQuadsByLevel"].Exists)
            {
                fillQuadsByLevel = Block.Attributes["fillQuadsByLevel"].AsObject<Cuboidf[]>();
            }
            

            if (fillQuadsByLevel == null)
            {
                fillQuadsByLevel = new Cuboidf[] { new Cuboidf(2, 0, 2, 14, 0, 14), };
            }

            if (api is ICoreClientAPI)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                capi.Event.RegisterRenderer(renderer = new ToolMoldRenderer(Pos, capi, fillQuadsByLevel), EnumRenderStage.Opaque, "toolmoldrenderer");
                
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
                renderer.Temperature = Math.Min(1300, metalContent.Collectible.GetTemperature(Api.World, metalContent));
            }
            
        }




        public void BeginFill(Vec3d hitPosition)
        {
            fillSide = hitPosition.X >= 0.5f;
        }


        public bool OnPlayerInteract(IPlayer byPlayer, BlockFacing onFace, Vec3d hitPosition)
        {
            bool sneaking = byPlayer.Entity.Controls.ShiftKey;

            if (!sneaking)
            {
                if (byPlayer.Entity.Controls.HandUse != EnumHandInteract.None) return false;

                bool handled = TryTakeContents(byPlayer);

                if (!handled && fillLevel == 0)
                {
                    ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                    if (activeSlot.Itemstack == null || activeSlot.Itemstack.Collectible is BlockToolMold)
                    {
                        if (!byPlayer.InventoryManager.TryGiveItemstack(new ItemStack(Block)))
                        {
                            Api.World.SpawnItemEntity(new ItemStack(Block), Pos.ToVec3d().Add(0.5, 0.2, 0.5));
                        }

                        Api.World.BlockAccessor.SetBlock(0, Pos);

                        if (Block.Sounds?.Place != null)
                        {
                            Api.World.PlaySoundAt(Block.Sounds.Place, Pos.X, Pos.Y, Pos.Z, byPlayer, false);
                        }

                        handled = true;
                    }

                    
                }

                return handled;
            }
            

            return false;
        }

        protected virtual bool TryTakeContents(IPlayer byPlayer)
        {
            if (Api is ICoreServerAPI) MarkDirty();

            if (metalContent != null && fillLevel >= requiredUnits && IsHardened)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos.X, Pos.Y, Pos.Z, byPlayer, false);

                if (Api is ICoreServerAPI)
                {
                    ItemStack[] outstacks = GetReadyMoldedStacks();

                    if (outstacks != null)
                    {
                        foreach (ItemStack outstack in outstacks)
                        {
                            outstack.Collectible.SetTemperature(Api.World, outstack, metalContent.Collectible.GetTemperature(Api.World, metalContent));

                            if (!byPlayer.InventoryManager.TryGiveItemstack(outstack))
                            {
                                Api.World.SpawnItemEntity(outstack, Pos.ToVec3d().Add(0.5, 0.2, 0.5));
                            }
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
                    metalContent.ResolveBlockOrItem(Api.World);
                    metalContent.Collectible.SetTemperature(Api.World, metalContent, temperature, false);
                    metalContent.StackSize = 1;
                    (metalContent.Attributes["temperature"] as ITreeAttribute)?.SetFloat("cooldownSpeed", 300);
                }
                else
                {
                    metalContent.Collectible.SetTemperature(Api.World, metalContent, temperature, false);
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

            renderer?.Dispose();
            renderer = null;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            renderer?.Dispose();
            renderer = null;
        }



        public ItemStack[] GetReadyMoldedStacks()
        {
            if (fillLevel < requiredUnits || !IsHardened) return null;
            if (metalContent?.Collectible == null) return null;

            ItemStack[] stacks = GetMoldedStacks(metalContent);

            return stacks;
        }

        public ItemStack[] GetMoldedStacks(ItemStack fromMetal)
        {
            try
            {
                if (Block.Attributes["drop"].Exists)
                {
                    JsonItemStack jstack = Block.Attributes["drop"].AsObject<JsonItemStack>(null, Block.Code.Domain);
                    if (jstack == null) return null;

                    ItemStack stack = stackFromCode(jstack, fromMetal);
                    if (stack == null) return new ItemStack[0];

                    return new ItemStack[] { stack };
                }
                else
                {
                    JsonItemStack[] jstacks = Block.Attributes["drops"].AsObject<JsonItemStack[]>(null, Block.Code.Domain);
                    List<ItemStack> stacks = new List<ItemStack>();

                    foreach (var jstack in jstacks)
                    {
                        ItemStack stack = stackFromCode(jstack, fromMetal);
                        if (stack != null)
                        {
                            stacks.Add(stack);
                        }
                    }

                    return stacks.ToArray();
                }
            } catch (JsonReaderException)
            {
                Api.World.Logger.Error("Failed getting molded stacks from tool mold of block {0}, probably unable to parse drop or drops attribute", Block.Code);
                throw;
            }
        }


        public ItemStack stackFromCode(JsonItemStack jstack, ItemStack fromMetal)
        {
            string metaltype = fromMetal.Collectible.LastCodePart();
            string tooltype = Block.LastCodePart();
            jstack.Code.Path = jstack.Code.Path.Replace("{tooltype}", tooltype).Replace("{metal}", metaltype);
            jstack.Resolve(Api.World, "tool mold drop for " + Block.Code);
            return jstack.ResolvedItemstack;
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolve)
        {
            base.FromTreeAttributes(tree, worldForResolve);

            metalContent = tree.GetItemstack("contents");
            fillLevel = tree.GetInt("fillLevel");
            if (Api?.World != null && metalContent != null) metalContent.ResolveBlockOrItem(Api.World);
            
            UpdateRenderer();

            if (Api?.Side == EnumAppSide.Client)
            {
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
            }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetItemstack("contents", metalContent);
            tree.SetInt("fillLevel", fillLevel);
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            string contents = "";

            if (this.metalContent != null)
            {
                string state = IsLiquid ? Lang.Get("liquid") : (IsHardened ? Lang.Get("hardened") : Lang.Get("soft"));

                string temp = Temperature < 21 ? Lang.Get("Cold") : Lang.Get("{0}°C", (int)Temperature);
                string matkey = "material-" + metalContent.Collectible.Variant["metal"];
                string mat = Lang.HasTranslation(matkey) ? Lang.Get(matkey) : Lang.Get(metalContent.GetName());
                contents = Lang.Get("{0}/{4} units of {1} {2} ({3})", fillLevel, state, mat, temp, requiredUnits) + "\n";
            }
            else
            {
                contents = Lang.Get("0/{0} units of metal", requiredUnits) + "\n";
            }


            dsc.AppendLine(contents.Length == 0 ? Lang.Get("Empty") : contents);
        }


        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            metalContent?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(metalContent), blockIdMapping, itemIdMapping);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            if (metalContent?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == null)
            {
                metalContent = null;
            }
            // update the time for the temperature to the current ingame time if imported from another game
            if ((metalContent?.Attributes["temperature"] as ITreeAttribute)?.HasAttribute("temperatureLastUpdate") == true)
            {
                ((ITreeAttribute)metalContent.Attributes["temperature"]).SetDouble("temperatureLastUpdate", worldForResolve.Calendar.TotalHours);
            }
        }


    }
}
