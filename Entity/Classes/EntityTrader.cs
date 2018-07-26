using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityTrader : EntityHumanoid
    {
        public InventoryTrader Inventory;


        public EntityTrader()
        {
            
        }

        public override void Initialize(ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(api, InChunkIndex3d);

            if (Inventory == null) Inventory = new InventoryTrader("traderInv", "" + EntityId, api);
            else Inventory.LateInitialize("traderInv-" + EntityId, api);
        }

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();

            LoadInitialTradeInventory();
        }

        private void LoadInitialTradeInventory()
        {
            
        }

        public override void OnInteract(EntityAgent byEntity, IItemSlot slot, Vec3d hitPosition, int mode)
        {
            if (mode != 1 || !(byEntity is EntityPlayer))
            {
                base.OnInteract(byEntity, slot, hitPosition, mode);
                return;
            }

            EntityPlayer entityplr = byEntity as EntityPlayer;
            IPlayer player = World.PlayerByUid(entityplr.PlayerUID);
            Inventory.Open(player);

            if (World.Side == EnumAppSide.Client)
            {
                (World as IClientWorldAccessor).OpenDialog("trader", this, Inventory);
            }
        }

        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            if(!WatchedAttributes.HasAttribute("traderInventory"))
            {
                if (Inventory == null)
                {
                    Inventory = new InventoryTrader("traderInv", "" + EntityId, null);
                }

                ITreeAttribute tree = new TreeAttribute();
                Inventory.ToTreeAttributes(tree);

                WatchedAttributes["traderInventory"] = tree;
            }

            Inventory.FromTreeAttributes(WatchedAttributes["traderInventory"] as ITreeAttribute);
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            Inventory.ToTreeAttributes(WatchedAttributes["traderInventory"] as ITreeAttribute);

            base.ToBytes(writer, forClient);
        }
    }
}
