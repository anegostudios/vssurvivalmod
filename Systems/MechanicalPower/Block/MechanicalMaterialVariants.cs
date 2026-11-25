using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent.Mechanics
{
    public class MechanicalMaterialVariants
    {
        public RegistryObjectVariantGroup[] VariantGroups { get; set; } = [];

        // Textures that change with the material, each naming one variant group as a placeholder such as {metal}.
        public Dictionary<string, CompositeTexture> Textures { get; set; } = [];
    }
}
