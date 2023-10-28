using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntityBasketTrap : BlockEntityContainer
    {
        protected ICoreServerAPI sapi;

        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "baskettrap";

        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>().animUtil; }
        }

        byte[] creatureData;
        double totalDaysCaught = -1;



        public BlockEntityBasketTrap()
        {
            inv = new InventoryGeneric(1, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            inv.LateInitialize("baskettrap-" + Pos, api);

            sapi = api as ICoreServerAPI;
            if (sapi != null)
            {
                RegisterGameTickListener(OnServerTick, 1000);
            } else
            {
                RegisterGameTickListener(OnClientTick, 1000);
            }
            if (sapi == null) animUtil?.InitializeAnimator("riftward");

        }

        private void OnClientTick(float dt)
        {
            if (totalDaysCaught >0)
            {
                if (Api.World.Rand.NextDouble() > 0.8)
                {
                    animUtil?.StartAnimation(new AnimationMetaData() { Animation = "hopshake", Code = "hopshake", EaseInSpeed = 1, EaseOutSpeed = 2, AnimationSpeed = 1f });
                }
            }
        }

        private void OnServerTick(float dt)
        {
            
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            if (byItemStack != null)
            {
                inv[0].Itemstack = byItemStack.Clone();
                inv[0].Itemstack.StackSize = 1;
            }
        }

        public void Interact(IPlayer player)
        {

        }

        
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (inv[0].Empty) return true;

            return true;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            dsc.Append(BlockEntityShelf.PerishableInfoCompact(Api, inv[0], 0));
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (worldForResolving.Side == EnumAppSide.Client)
            {
                MarkDirty(true);
            }
        }
    }
}
