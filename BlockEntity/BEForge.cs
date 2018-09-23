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

namespace Vintagestory.GameContent
{
    public class BlockEntityForge : BlockEntity
    {
        ForgeContentsRenderer renderer;
        ItemStack contents;
        float fuelLevel;
        bool burning;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (contents != null) contents.ResolveBlockOrItem(api.World);

            if (api is ICoreClientAPI)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                capi.Event.RegisterRenderer(renderer = new ForgeContentsRenderer(pos, capi), EnumRenderStage.Opaque);  
                renderer.SetContents(contents, fuelLevel, burning, true);
            }

            
            api.Event.RegisterGameTickListener(OnGameTick, 50);
        }

        private void OnGameTick(float dt)
        {
            if (burning)
            {
                if (fuelLevel > 0) fuelLevel = Math.Max(0, fuelLevel - 0.0001f);

                if (fuelLevel <= 0)
                {
                    burning = false;
                }

                if (contents != null)
                {
                    float temp = contents.Collectible.GetTemperature(api.World, contents);
                    if (temp < 1100)
                    {
                        contents.Collectible.SetTemperature(api.World, contents, temp + 2);
                    }
                }
            }

            if (renderer != null)
            {
                renderer.SetContents(contents, fuelLevel, burning, false);
            }
        }

        public bool IsBurning
        {
            get { return burning; }
        }

        public bool CanIgnite
        {
            get { return !burning && fuelLevel > 0; }
        }

        internal void TryIgnite()
        {
            if (burning) return;

            burning = true;
            renderer?.SetContents(contents, fuelLevel, burning, false);
            MarkDirty();
        }

        internal bool OnPlayerInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            IItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!byPlayer.Entity.Controls.Sneak)
            {
                if (contents == null) return false;
                ItemStack split = contents.Clone();
                split.StackSize = 1;
                contents.StackSize--;
                
                if (contents.StackSize == 0) contents = null;

                //api.World.Logger.Notification("Forge item retrieve temp: {0}, side {1}", split.Collectible.GetTemperature(api.World, split), api.Side);

                if (!byPlayer.InventoryManager.TryGiveItemstack(split))
                {
                    world.SpawnItemEntity(contents, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }

                renderer?.SetContents(contents, fuelLevel, burning, true);
                MarkDirty();
                api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), pos.X, pos.Y, pos.Z, byPlayer, false);

                return true;

            } else
            {   
                if (slot.Itemstack == null) return false;

                // Add fuel
                CombustibleProperties combprops = slot.Itemstack.Collectible.CombustibleProps;
                if (combprops != null && combprops.BurnTemperature > 1000)
                {
                    if (fuelLevel >= 10 / 16f) return false;
                    fuelLevel += 1 / 16f;

                    renderer?.SetContents(contents, fuelLevel, burning, false);
                    MarkDirty();

                    slot.TakeOut(1);
                    slot.MarkDirty();


                    return true;
                }


                string firstCodePart = slot.Itemstack.Collectible.FirstCodePart();

                // Add heatable item
                if (contents == null && (firstCodePart == "ingot" || firstCodePart == "metalplate" || firstCodePart == "workitem"))
                {
                    contents = slot.Itemstack.Clone();
                    contents.StackSize = 1;

                    slot.TakeOut(1);
                    slot.MarkDirty();

                    renderer?.SetContents(contents, fuelLevel, burning, true);
                    MarkDirty();
                    api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), pos.X, pos.Y, pos.Z, byPlayer, false);

                    return true;
                }

                // Merge heatable item
                if (contents != null && contents.Equals(api.World, slot.Itemstack, GlobalConstants.IgnoredStackAttributes) && contents.StackSize < 4 && contents.StackSize < contents.Collectible.MaxStackSize)
                {
                    float myTemp = contents.Collectible.GetTemperature(api.World, contents);
                    float histemp = slot.Itemstack.Collectible.GetTemperature(api.World, slot.Itemstack);

                    contents.Collectible.SetTemperature(world, contents, (myTemp * contents.StackSize + histemp * 1) / (contents.StackSize + 1));
                    contents.StackSize++;

                    slot.TakeOut(1);
                    slot.MarkDirty();

                    renderer?.SetContents(contents, fuelLevel, burning, true);
                    api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), pos.X, pos.Y, pos.Z, byPlayer, false);

                    MarkDirty();
                    return true;
                }

                return false;
            }
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

        public override void OnBlockBroken()
        {
            if (contents != null)
            {
                api.World.SpawnItemEntity(contents, pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            contents = tree.GetItemstack("contents");
            fuelLevel = tree.GetFloat("fuelLevel");
            burning = tree.GetInt("burning") > 0;

            if (api != null)
            {
                contents?.ResolveBlockOrItem(api.World);
            }
            if (renderer != null)
            {
                renderer.SetContents(contents, fuelLevel, burning, true);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetItemstack("contents", contents);
            tree.SetFloat("fuelLevel", fuelLevel);
            tree.SetInt("burning", burning ? 1 : 0);
        }

        public override string GetBlockInfo(IPlayer forPlayer)
        {
            if (contents != null)
            {
                return string.Format("Contents: {0}x {1}\nTemperature: {2}°C", contents.StackSize, contents.GetName(), (int)contents.Collectible.GetTemperature(api.World, contents));
            }

            return null;
        }


        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping)
        {
            if (contents?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false)
            {
                contents = null;
            }
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            if (contents != null)
            {
                if (contents.Class == EnumItemClass.Item)
                {
                    blockIdMapping[contents.Id] = contents.Item.Code;
                }
                else
                {
                    itemIdMapping[contents.Id] = contents.Block.Code;
                }
            }
            
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Unregister();
        }

    }
}
