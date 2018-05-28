using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class ModBasicBlocksLoader : ModSystem
    {
        ICoreServerAPI api;

        //SoundSet solidSounds;
        //SoundSet snowSounds;
        BlockSounds noSound;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override bool AllowRuntimeReload()
        {
            return false;
        }
        
        public override double ExecuteOrder()
        {
            return 0.1;
        }


        public override void StartServerSide(ICoreServerAPI manager)
        {
            api = manager;

            noSound = new BlockSounds();
      
            #region Block types
            
            api.RegisterBlock(new Block()
            {
                Code = new AssetLocation("mantle"),
                Textures = new Dictionary<string, CompositeTexture> {
                    { "all", new CompositeTexture(new AssetLocation("block/mantle")) },
                },
                DrawType = EnumDrawType.Cube,
                MatterState = EnumMatterState.Solid,
                BlockMaterial = EnumBlockMaterial.Stone,
                Replaceable = 0,
                Resistance = 31337,
                CreativeInventoryTabs = new string[] { "general" }
            });

            #endregion

        }

    }
}
