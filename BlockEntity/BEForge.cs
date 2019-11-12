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

namespace Vintagestory.GameContent
{
    public class BlockEntityForge : BlockEntity
    {
        ForgeContentsRenderer renderer;
        ItemStack contents;
        float fuelLevel;
        bool burning;

        double lastHeatTotalHours;

        public ItemStack Contents => contents;
        public float FuelLevel => fuelLevel;

        static SimpleParticleProperties smokeParticles;

        static BlockEntityForge()
        {
            smokeParticles = new SimpleParticleProperties(
                   1, 1,
                   ColorUtil.ToRgba(150, 80, 80, 80),
                   new Vec3d(),
                   new Vec3d(0.75, 0, 0.75),
                   new Vec3f(-1 / 32f, 0.1f, -1 / 32f),
                   new Vec3f(1 / 32f, 0.1f, 1 / 32f),
                   1.5f,
                   -0.025f / 4,
                   0.2f,
                   0.4f,
                   EnumParticleModel.Quad
               );

            smokeParticles.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.25f);
            smokeParticles.SelfPropelled = true;
            smokeParticles.addPos.Set(8 / 16.0, 0, 8 / 16.0);
        }



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (contents != null) contents.ResolveBlockOrItem(api.World);

            if (api is ICoreClientAPI)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                capi.Event.RegisterRenderer(renderer = new ForgeContentsRenderer(Pos, capi), EnumRenderStage.Opaque);  
                renderer.SetContents(contents, fuelLevel, burning, true);
            }

            lastHeatTotalHours = api.World.Calendar.TotalHours;

            RegisterGameTickListener(OnGameTick, 50);
        }

        private void OnGameTick(float dt)
        {
            if (burning)
            {
                if (Api.Side == EnumAppSide.Client && Api.World.Rand.NextDouble() < 0.1) 
                {
                    smokeParticles.minPos.Set(Pos.X + 4/16f, Pos.Y + 14/16f, Pos.Z + 4 / 16f);
                    int g = 50 + Api.World.Rand.Next(50);
                    smokeParticles.color = ColorUtil.ToRgba(150, g, g, g);
                    Api.World.SpawnParticles(smokeParticles);
                }

                if (fuelLevel > 0) fuelLevel = Math.Max(0, fuelLevel - 0.0001f);

                if (fuelLevel <= 0)
                {
                    burning = false;
                }

                if (contents != null)
                {
                    float temp = contents.Collectible.GetTemperature(Api.World, contents);
                    if (temp < 1100)
                    {
                        float tempGain = (float)(Api.World.Calendar.TotalHours - lastHeatTotalHours) * 1500;

                        contents.Collectible.SetTemperature(Api.World, contents, Math.Min(1100, temp + tempGain));
                    }
                }
            }

            if (renderer != null)
            {
                renderer.SetContents(contents, fuelLevel, burning, false);
            }

            lastHeatTotalHours = Api.World.Calendar.TotalHours;
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
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!byPlayer.Entity.Controls.Sneak)
            {
                if (contents == null) return false;
                ItemStack split = contents.Clone();
                split.StackSize = 1;
                contents.StackSize--;
                
                if (contents.StackSize == 0) contents = null;

                if (!byPlayer.InventoryManager.TryGiveItemstack(split))
                {
                    world.SpawnItemEntity(split, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }

                renderer?.SetContents(contents, fuelLevel, burning, true);
                MarkDirty();
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos.X, Pos.Y, Pos.Z, byPlayer, false);

                return true;

            } else
            {   
                if (slot.Itemstack == null) return false;

                // Add fuel
                CombustibleProperties combprops = slot.Itemstack.Collectible.CombustibleProps;
                if (combprops != null && combprops.BurnTemperature > 1000)
                {
                    if (fuelLevel >= 5 / 16f) return false;
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
                    Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos.X, Pos.Y, Pos.Z, byPlayer, false);

                    return true;
                }

                // Merge heatable item
                if (contents != null && contents.Equals(Api.World, slot.Itemstack, GlobalConstants.IgnoredStackAttributes) && contents.StackSize < 4 && contents.StackSize < contents.Collectible.MaxStackSize)
                {
                    float myTemp = contents.Collectible.GetTemperature(Api.World, contents);
                    float histemp = slot.Itemstack.Collectible.GetTemperature(Api.World, slot.Itemstack);

                    contents.Collectible.SetTemperature(world, contents, (myTemp * contents.StackSize + histemp * 1) / (contents.StackSize + 1));
                    contents.StackSize++;

                    slot.TakeOut(1);
                    slot.MarkDirty();

                    renderer?.SetContents(contents, fuelLevel, burning, true);
                    Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos.X, Pos.Y, Pos.Z, byPlayer, false);

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
            base.OnBlockBroken();

            if (contents != null)
            {
                Api.World.SpawnItemEntity(contents, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            contents = tree.GetItemstack("contents");
            fuelLevel = tree.GetFloat("fuelLevel");
            burning = tree.GetInt("burning") > 0;

            if (Api != null)
            {
                contents?.ResolveBlockOrItem(Api.World);
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

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (contents != null)
            {
                dsc.AppendLine(string.Format("Contents: {0}x {1}\nTemperature: {2}°C", contents.StackSize, contents.GetName(), (int)contents.Collectible.GetTemperature(Api.World, contents)));
            }
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
