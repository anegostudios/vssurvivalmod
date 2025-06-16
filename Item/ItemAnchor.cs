using System;
using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemAnchor : Item, IAttachableToEntity, IAttachedInteractions
    {
        CompositeShape loweredShape;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            loweredShape = Attributes["loweredAnchorShape"].AsObject<CompositeShape>();
        }

        public CompositeShape GetAttachedShape(ItemStack stack, string slotCode)
        {
            return stack.Attributes.GetBool("lowered") ? loweredShape : Shape;
        }

        public void OnInteract(ItemSlot itemslot, int slotIndex, Entity onEntity, EntityAgent byEntity, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled, Action onRequireSave)
        {
            itemslot.Itemstack.Attributes.SetBool("lowered", !itemslot.Itemstack.Attributes.GetBool("lowered"));
            onEntity.MarkShapeModified();
        }




        public int RequiresBehindSlots { get; set; } = 0;
        public string GetCategoryCode(ItemStack stack) => "anchor";
        public string[] GetDisableElements(ItemStack stack) => Array.Empty<string>();
        public string[] GetKeepElements(ItemStack stack) => Array.Empty<string>();
        public string GetTexturePrefixCode(ItemStack stack) => "anchor";
        public bool IsAttachable(Entity toEntity, ItemStack itemStack) => true;
        public void CollectTextures(ItemStack stack, Shape shape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict) { }

        public void OnAttached(ItemSlot itemslot, int slotIndex, Entity toEntity, EntityAgent byEntity) {
            itemslot.Itemstack.Attributes.RemoveAttribute("lowered");
        }

        public void OnDetached(ItemSlot itemslot, int slotIndex, Entity fromEntity, EntityAgent byEntity) {
            itemslot.Itemstack.Attributes.RemoveAttribute("lowered");
        }

        public void OnEntityDeath(ItemSlot itemslot, int slotIndex, Entity onEntity, DamageSource damageSourceForDeath) { }

        public void OnEntityDespawn(ItemSlot itemslot, int slotIndex, Entity onEntity, EntityDespawnData despawn) { }


        public void OnReceivedClientPacket(ItemSlot itemslot, int slotIndex, Entity onEntity, IServerPlayer player, int packetid, byte[] data, ref EnumHandling handled, Action onRequireSave) { }

        public bool OnTryAttach(ItemSlot itemslot, int slotIndex, Entity toEntity) { return true; }

        public bool OnTryDetach(ItemSlot itemslot, int slotIndex, Entity toEntity) { return true; }
    }
}
