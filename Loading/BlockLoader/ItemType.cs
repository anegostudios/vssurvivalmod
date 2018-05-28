using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ItemType : CollectibleType
    {
        public ItemType()
        {
            Class = "Item";
            GuiTransform = ModelTransform.ItemDefaultGui();
            FpHandTransform = ModelTransform.ItemDefault();
            TpHandTransform = ModelTransform.ItemDefaultTp();
            GroundTransform = ModelTransform.ItemDefault();
        }

        [JsonProperty]
        public int Durability;
        [JsonProperty]
        public EnumItemDamageSource[] DamagedBy;
        [JsonProperty]
        public EnumTool? Tool = null;

        public void InitItem(IClassRegistryAPI instancer, Item item, Dictionary<string, string> searchReplace)
        {
            item.CreativeInventoryTabs = BlockType.GetCreativeTabs(item.Code, CreativeInventory, searchReplace);
        }
    }
}
