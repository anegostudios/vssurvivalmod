using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;


namespace Vintagestory.GameContent
{
    public class ItemScythe : ItemShears
    {
        string[] allowedPrefixes;
        string[] disallowedSuffixes;

        public override int MultiBreakQuantity { get { return 5; } }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            allowedPrefixes = Attributes["codePrefixes"].AsArray<string>();
            disallowedSuffixes = Attributes["disallowedSuffixes"].AsArray<string>();
        }

        public override bool CanMultiBreak(Block block)
        {
            for (int i = 0; i < allowedPrefixes.Length; i++)
            {
                if (block.Code.Path.StartsWith(allowedPrefixes[i]))
                {
                    // Disable scything on thick snow variants (-snow2, -snow3 etc.)
                    if (disallowedSuffixes != null)
                    {
                        for (int j = 0; j < disallowedSuffixes.Length; j++)
                        {
                            if (block.Code.Path.EndsWith(disallowedSuffixes[j])) return false;
                        }
                    }

                    return true;
                }
            }

            return false;   
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
            if (blockSel == null) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            if (CanMultiBreak(api.World.BlockAccessor.GetBlock(blockSel.Position)))
            {
                handling = EnumHandHandling.PreventDefault;
                byEntity.Attributes.SetBool("didBreakBlocks", false);
                byEntity.Attributes.SetBool("didPlaySound", false);
            } else
            {
                handling = EnumHandHandling.PreventDefault;
                byEntity.Attributes.SetBool("didBreakBlocks", false);
            }
        }

        public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            if (blockSelection == null) return false;
            if (!CanMultiBreak(api.World.BlockAccessor.GetBlock(blockSelection.Position)) && byEntity.Attributes.GetBool("didBreakBlocks") == false) return false;

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float t = secondsPassed / 1.35f;

                float f = (float)Easings.EaseOutBack(Math.Min(t * 2f, 1));
                float f2 = (float)Math.Sin(GameMath.Clamp(Math.PI * 1.4f * (t - 0.5f), 0, 3));

                tf.Translation.X += Math.Min(0.2f, t * 3);
                tf.Translation.Y -= Math.Min(0.75f, t * 3);
                tf.Translation.Z -= Math.Min(1, t * 3);
                tf.ScaleXYZ += Math.Min(1, t * 3);
                tf.Origin.X -= Math.Min(0.75f, t * 3);
                tf.Rotation.X = -Math.Min(30, t * 30) + f * 30 + (float)f2 * 120f;
                tf.Rotation.Z = -f * 110;

                if (secondsPassed > 1.75f)
                {
                    float b = 2 * (secondsPassed - 1.75f);
                    tf.Rotation.Z += b * 140;
                    tf.Rotation.X /= (1 + b * 10);
                    tf.Translation.X -= b * 0.4f;
                    tf.Translation.Y += b * 2 / 0.75f;
                    tf.Translation.Z += b * 2;
                    //tf.Origin.X += b * 2 / 0.75f;
                    //tf.ScaleXYZ -= b * 2;
                }

                byEntity.Controls.UsingHeldItemTransformBefore = tf;
            }

            if (secondsPassed > 0.75f && byEntity.Attributes.GetBool("didPlaySound") == false)
            {
                api.World.PlaySoundAt(new AssetLocation("sounds/tool/scythe1"), byEntity, (byEntity as EntityPlayer)?.Player, true, 16);
                byEntity.Attributes.SetBool("didPlaySound", true);
            }

            if (secondsPassed > 1.45f && byEntity.Attributes.GetBool("didBreakBlocks") == false)
            {
                if (byEntity.World.Side == EnumAppSide.Server)
                {
                    IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
                    if (!byEntity.World.Claims.TryAccess(byPlayer, blockSelection.Position, EnumBlockAccessFlags.BuildOrBreak))
                    {
                        return false;
                    }

                    OnBlockBrokenWith(byEntity.World, byEntity, slot, blockSelection);
                }
                byEntity.Attributes.SetBool("didBreakBlocks", true);
            }

            // Crappy fix to make harvesting not buggy T_T
            if (api.Side == EnumAppSide.Server) return true;

            return secondsPassed < 2f;
        }

        public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            
        }
    }
}
