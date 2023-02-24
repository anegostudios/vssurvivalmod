using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class DistillationProps
    {
        public JsonItemStack DistilledStack;
        public float Ratio;
    }

    public class BlockEntityBoiler : BlockEntityLiquidContainer, IFirePit
    {
        public override string InventoryClassName => "boiler";


        MeshData firepitMesh;
        public int firepitStage;
        double lastTickTotalHours;
        public float fuelHours;
        float distillationAccum;

        public virtual float SoundLevel
        {
            get { return 0.66f; }
        }

        public bool IsBurning => firepitStage == 6 && fuelHours > 0;
        public bool IsSmoldering => firepitStage == 6 && fuelHours > -3;


        public static AssetLocation[] firepitShapeBlockCodes = new AssetLocation[]
        {
            null,
            new AssetLocation("firepit-construct1"),
            new AssetLocation("firepit-construct2"),
            new AssetLocation("firepit-construct3"),
            new AssetLocation("firepit-construct4"),
            new AssetLocation("firepit-cold"),
            new AssetLocation("firepit-lit"),
            new AssetLocation("firepit-extinct"),
        };


        public BlockEntityBoiler()
        {
            inventory = new InventoryGeneric(1, null, null);
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            RegisterGameTickListener(onBurnTick, 100);
            loadMesh();

            if (firepitStage == 6 && IsBurning)
            {
                GetBehavior<BEBehaviorFirepitAmbient>()?.ToggleAmbientSounds(true);
            }
        }


        private void onBurnTick(float dt)
        {
            if (firepitStage == 6 && !IsBurning)
            {
                GetBehavior<BEBehaviorFirepitAmbient>()?.ToggleAmbientSounds(false);
                firepitStage++;
                MarkDirty(true);
            }

            if (IsBurning)
            {
                heatLiquid(dt);
            }

            double dh = Api.World.Calendar.TotalHours - lastTickTotalHours;
            if (dh > 0.1f)
            {
                if (IsBurning) fuelHours -= (float)dh;
                lastTickTotalHours = Api.World.Calendar.TotalHours;
            }


            var props = DistProps;
            if (InputStackTemp >= 75 && props != null)
            {
                distillationAccum += dt * props.Ratio;

                if (distillationAccum >= 0.2f)
                {
                    distillationAccum -= 0.2f;

                    for (int i = 0; i < 4; i++)
                    {
                        BlockEntityCondenser becd = Api.World.BlockAccessor.GetBlockEntity(Pos.AddCopy(BlockFacing.HORIZONTALS[i])) as BlockEntityCondenser;
                        if (becd != null)
                        {
                            props?.DistilledStack.Resolve(Api.World, "distillationprops");
                            if (becd.ReceiveDistillate(inventory[0], props))
                            {
                                break;
                            }
                        }
                    }
                }

            }
        }

        public float InputStackTemp
        {
            get
            {
                return InputStack?.Collectible.GetTemperature(Api.World, inventory[0].Itemstack) ?? 0;
            }
            set
            {
                InputStack.Collectible.SetTemperature(Api.World, inventory[0].Itemstack, value);
            }
        }

        public DistillationProps DistProps {
            get
            {
                return InputStack?.ItemAttributes?["distillationProps"].AsObject<DistillationProps>();
            }
        }

        public ItemStack InputStack
        {
            get
            {
                return inventory[0]?.Itemstack;
            }
        }



        public void heatLiquid(float dt)
        {
            if (inventory[0].Empty) return;
            
            if (InputStackTemp < 100)
            {
                InputStackTemp += dt * 2;
            }
        }

        public bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            bool addGrass = hotbarSlot.Itemstack?.Collectible is ItemDryGrass && firepitStage == 0;
            bool addFireWood = hotbarSlot.Itemstack?.Collectible is ItemFirewood && firepitStage >= 1 && firepitStage <= 4;
            bool reignite = hotbarSlot.Itemstack?.Collectible is ItemFirewood && (firepitStage >= 5 && fuelHours <= 6f);

            if (addGrass || addFireWood || reignite)
            {
                if (!reignite) firepitStage++;
                else if (firepitStage == 7) firepitStage = 5;

                MarkDirty(true);
                hotbarSlot.TakeOut(1);
                (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                Block block = Api.World.GetBlock(firepitShapeBlockCodes[firepitStage]);
                if (block?.Sounds != null) Api.World.PlaySoundAt(block.Sounds.Place, Pos.X, Pos.Y, Pos.Z, byPlayer);
            }


            if (addGrass) return true;
            if (addFireWood || reignite)
            {
                fuelHours = Math.Max(2, fuelHours + 2);
                return true;
            }


            return false;
        }



        public bool CanIgnite()
        {
            return firepitStage == 5;
        }

        public void TryIgnite()
        {
            if (!CanIgnite()) return;

            firepitStage++;
            GetBehavior<BEBehaviorFirepitAmbient>()?.ToggleAmbientSounds(true);

            MarkDirty(true);
            lastTickTotalHours = Api.World.Calendar.TotalHours;
        }


        private void loadMesh()
        {
            if (Api.Side == EnumAppSide.Server) return;
            if (firepitStage <= 0)
            {
                firepitMesh = null; 
                return;
            }
            Block block = Api.World.GetBlock(firepitShapeBlockCodes[firepitStage]);
            ICoreClientAPI capi = Api as ICoreClientAPI;
            firepitMesh = capi.TesselatorManager.GetDefaultBlockMesh(block);
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            firepitStage = tree.GetInt("firepitConstructionStage");
            lastTickTotalHours = tree.GetDouble("lastTickTotalHours");
            fuelHours = tree.GetFloat("fuelHours");

            if (Api != null) loadMesh();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("firepitConstructionStage", firepitStage);
            tree.SetDouble("lastTickTotalHours", lastTickTotalHours);
            tree.SetFloat("fuelHours", fuelHours);
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            mesher.AddMeshData(firepitMesh);

            return base.OnTesselation(mesher, tessThreadTesselator);
        }




    }
}
