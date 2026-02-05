using System;
using System.Collections.Generic;
using SkiaSharp;
using System.Text.RegularExpressions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public enum EnumSkinnableType
    {
        Shape, 
        Texture, 
        Voice
    }

    public class SkinnablePart
    {
        public bool Hidden;
        public bool Colbreak;
        public bool UseDropDown;
        public string Code;
        public EnumSkinnableType Type;
        public string[] DisableElements;
        public CompositeShape ShapeTemplate;
        public SkinnablePartVariant[] Variants;
        public Vec2i TextureRenderTo = null;
        public string TextureTarget;
        public AssetLocation TextureTemplate;
        public Dictionary<string, SkinnablePartVariant> VariantsByCode;
    }

    public class SkinnablePartVariant
    {
        public string Category = "standard";
        public string Code;
        public CompositeShape Shape;
        public AssetLocation Texture;
        public AssetLocation Sound;
        public int Color;

        public AppliedSkinnablePartVariant AppliedCopy(string partCode)
        {
            return new AppliedSkinnablePartVariant()
            {
                Code = Code,
                Shape = Shape,
                Texture = Texture,
                Color = Color,
                PartCode = partCode
            };
        }
    }

    public class AppliedSkinnablePartVariant : SkinnablePartVariant
    {
        public string PartCode;
    }

    public class EntityBehaviorExtraSkinnable : EntityBehavior
    {
        public Dictionary<string, SkinnablePart> AvailableSkinPartsByCode = new Dictionary<string, SkinnablePart>();
        public SkinnablePart[] AvailableSkinParts;
        public string VoiceType = "altoflute";
        public string VoicePitch = "medium";
        public string mainTextureCode;
        public List<AppliedSkinnablePartVariant> appliedTemp = new List<AppliedSkinnablePartVariant>();
        protected ITreeAttribute skintree;

        public IReadOnlyList<AppliedSkinnablePartVariant> AppliedSkinParts
        {
            get
            {
                appliedTemp.Clear();

                ITreeAttribute appliedTree = skintree.GetTreeAttribute("appliedParts");
                if (appliedTree == null) return appliedTemp;

                foreach (SkinnablePart part in AvailableSkinParts)
                {
                    string code = appliedTree.GetString(part.Code);
                    if (code != null && part.VariantsByCode.TryGetValue(code, out SkinnablePartVariant variant))
                    {
                        appliedTemp.Add(variant.AppliedCopy(part.Code));
                    }
                }

                return appliedTemp;
            }
        }


        public EntityBehaviorExtraSkinnable(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            skintree = entity.WatchedAttributes.GetTreeAttribute("skinConfig");
            if (skintree == null)
            {
                entity.WatchedAttributes["skinConfig"] = skintree = new TreeAttribute();
            }

            mainTextureCode = properties.Attributes["mainTextureCode"].AsString("seraph");

            entity.WatchedAttributes.RegisterModifiedListener("skinConfig", onSkinConfigChanged);
            entity.WatchedAttributes.RegisterModifiedListener("voicetype", onVoiceConfigChanged);
            entity.WatchedAttributes.RegisterModifiedListener("voicepitch", onVoiceConfigChanged);

            AvailableSkinParts = properties.Attributes["skinnableParts"].AsObject<SkinnablePart[]>();
            AvailableSkinParts = entity.Api.ModLoader.GetModSystem<ModSystemSkinnableAdditions>().AppendAdditions(AvailableSkinParts);

            foreach (var part in AvailableSkinParts)
            {
                string partCode = part.Code;
                part.VariantsByCode = new Dictionary<string, SkinnablePartVariant>();

                AvailableSkinPartsByCode[part.Code] = part;

                if (part.Type == EnumSkinnableType.Texture && entity.Api.Side == EnumAppSide.Client)
                {
                    ICoreClientAPI capi = entity.Api as ICoreClientAPI;

                    LoadedTexture texture = new LoadedTexture(capi);
                    foreach (var variant in part.Variants)
                    {
                        AssetLocation textureLoc;

                        if (part.TextureTemplate != null)
                        {
                            textureLoc = part.TextureTemplate.Clone();
                            string resolvedPath = resolveTemplate(textureLoc.Path, variant?.Code, true);

                            if (variant.Color != 0)
                            {
                                part.VariantsByCode[variant.Code] = variant;
                            }

                            textureLoc.Path = resolvedPath;
                        }
                        else
                        {
                            textureLoc = variant.Texture;
                        }

                        AssetLocation assetLoc = textureLoc.Clone().WithPathAppendixOnce(".png").WithPathPrefixOnce("textures/");
                        IAsset asset = capi.Assets.TryGet(assetLoc, true);
                        if (asset != null && variant.Color == 0)
                        {
                            int r = 0, g = 0, b = 0;
                            float c = 0;

                            BitmapRef bmp = asset.ToBitmap(capi);
                            for (int i = 0; i < 8; i++)
                            {
                                Vec2d vec = GameMath.R2Sequence2D(i);
                                SKColor col2 = bmp.GetPixelRel((float)vec.X, (float)vec.Y);
                                if (col2.Alpha > 0.5)
                                {
                                    r += col2.Red;
                                    g += col2.Green;
                                    b += col2.Blue;
                                    c++;
                                }
                            }

                            bmp.Dispose();

                            c = Math.Max(1, c);
                            variant.Color = ColorUtil.ColorFromRgba((int)(b / c), (int)(g / c), (int)(r / c), 255);
                            part.VariantsByCode[variant.Code] = variant;
                        }
                        else
                        {
                            part.VariantsByCode[variant.Code] = variant;
                        }
                    }
                }
                else
                {
                    foreach (var variant in part.Variants)
                    {
                        part.VariantsByCode[variant.Code] = variant;
                    }
                }
            }

            if (entity.Api.Side == EnumAppSide.Server && AppliedSkinParts.Count == 0)
            {
                entity.Api.ModLoader.GetModSystem<CharacterSystem>().randomizeSkin(entity, null, false);
            }

            onVoiceConfigChanged();
            entity.MarkShapeModified();
        }

        private void onSkinConfigChanged()
        {
            skintree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;
           
            entity.MarkShapeModified();

        }


        private void onVoiceConfigChanged()
        {
            VoiceType = entity.WatchedAttributes.GetString("voicetype");
            VoicePitch = entity.WatchedAttributes.GetString("voicepitch");

            ApplyVoice(VoiceType, VoicePitch, false);
        }


        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            init();
        }

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();
            init();
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            var ebhtc = entity.GetBehavior<EntityBehaviorTexturedClothing>();
            if (ebhtc != null)
            {
                ebhtc.OnReloadSkin -= Essr_OnReloadSkin;
            }
        }

        bool didInit = false;
        void init()
        {
            if (entity.World.Side != EnumAppSide.Client) return;

            if (!didInit)
            {
                var essr = entity.Properties.Client.Renderer as EntityShapeRenderer;
                if (essr == null) throw new InvalidOperationException("The extra skinnable entity behavior requires the entity to use the Shape renderer.");

                var ebhtc = entity.GetBehavior<EntityBehaviorTexturedClothing>();
                if (ebhtc == null) throw new InvalidOperationException("The extra skinnable entity behavior requires the entity to have the TextureClothing entitybehavior.");

                ebhtc.OnReloadSkin += Essr_OnReloadSkin;
                didInit = true;
            }
        }

        /// <summary>
        /// Resolves a template path by substituting placeholders.
        /// {code} is replaced by the current variant's code.
        /// {partCode} is replaced by the currently selected variant code for that part.
        /// If a placeholder is not set, automatically substitutes the first variant's code for that part.
        /// </summary>
        private string resolveTemplate(string template, string code, bool forPreload)
        {
            if (template == null) return null;

            string resolvedPath = template;

            // Always replace {code} with the current variant's code first.
            if (code != null)
            {
                resolvedPath = resolvedPath.Replace("{code}", code);
            }

            // At runtime (not pre-load), resolve cross-dependencies from other applied skin parts.
            if (!forPreload)
            {
                ITreeAttribute appliedTree = skintree.GetTreeAttribute("appliedParts");
                if (appliedTree != null)
                {
                    foreach (var part in AvailableSkinParts)
                    {
                        string placeholder = "{" + part.Code + "}";
                        if (resolvedPath.Contains(placeholder))
                        {
                            string variantCode = appliedTree.GetString(part.Code);

                            // If no variant is set, automatically use the first variant for this part
                            if (variantCode == null && part.Variants != null && part.Variants.Length > 0)
                            {
                                variantCode = part.Variants[0].Code;
                            }

                            if (variantCode != null)
                            {
                                resolvedPath = resolvedPath.Replace(placeholder, variantCode);
                            }
                        }
                    }
                }
            }

            return resolvedPath;
        }      


        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
        {
            // Make a copy so we don't mess up the original
            if (!shapeIsCloned)
            {
                Shape newShape = entityShape.Clone();
                entityShape = newShape;
                shapeIsCloned = true;
            }

            string[] disabledElements = [];
            var appliedSkinParts = AppliedSkinParts;
            foreach (var skinpart in appliedSkinParts)
            {
                AvailableSkinPartsByCode.TryGetValue(skinpart.PartCode, out SkinnablePart part);
                if (part == null) continue;

                if (part.Type == EnumSkinnableType.Shape)
                {
                    entityShape = addSkinPart(skinpart, entityShape, part.DisableElements, shapePathForLogging);
                }
                else if (part.Type == EnumSkinnableType.Texture && part.TextureTarget != null && part.TextureTarget != mainTextureCode)
                {
                    AssetLocation textureLoc;
                    if (part.TextureTemplate != null)
                    {
                        textureLoc = part.TextureTemplate.Clone();
                        textureLoc.Path = resolveTemplate(textureLoc.Path, skinpart?.Code, false);
                    }
                    else
                    {
                        textureLoc = skinpart.Texture;
                    }

                    string code = "skinpart-" + part.TextureTarget;
                    entityShape.TextureSizes.TryGetValue(code, out int[] sizes);
                    if (sizes != null)
                    {
                        loadTexture(entityShape, code, textureLoc, sizes[0], sizes[1], shapePathForLogging);
                    }
                    else
                    {
                        entity.Api.Logger.Error(
                            "Skinpart '{0}' (targeting texture code '{1}') failed. The main entity shape file '{2}' is missing a required texture dimension definition. " +
                            "Please add '\"{1}\": [width, height]' to the 'textureSizes' section inside that file. Replace 'width' and 'height' with the actual dimensions of your texture.",
                            part.Code, code, shapePathForLogging
                        );
                    }
                }

                if (part.DisableElements != null)
                {
                    disabledElements = disabledElements.Append(part.DisableElements);
                }
            }

            if (disabledElements.Length > 0)
            {
                entityShape.RemoveElements(disabledElements);
            }

            var ebhtc = entity.GetBehavior<EntityBehaviorTexturedClothing>();
            var inv = ebhtc.Inventory;
            if (inv != null)
            {
                foreach (var slot in inv)
                {
                    if (slot.Empty) continue;

                    if (ebhtc.hideClothing)
                    {
                        continue;
                    }

                    ItemStack stack = slot.Itemstack;
                    JsonObject attrObj = stack.Collectible.Attributes;

                    entityShape.RemoveElements(attrObj?["disableElements"]?.AsArray<string>(null));
                    var keepEles = attrObj?["keepElements"]?.AsArray<string>(null);
                    if (keepEles != null && willDeleteElements != null)
                    {
                        foreach (var val in keepEles) willDeleteElements = willDeleteElements.Remove(val);
                    }
                }
            }
        }


        private void Essr_OnReloadSkin(LoadedTexture atlas, TextureAtlasPosition skinTexPos, int textureSubId)
        {
            ICoreClientAPI capi = entity.World.Api as ICoreClientAPI;

            foreach (var val in AppliedSkinParts)
            {
                SkinnablePart part = AvailableSkinPartsByCode[val.PartCode];

                if (part.Type != EnumSkinnableType.Texture) continue;
                if (part.TextureTarget != null && part.TextureTarget != mainTextureCode) continue;
                
                LoadedTexture texture = new LoadedTexture(capi);

                capi.Render.GetOrLoadTexture(val.Texture.Clone().WithPathAppendixOnce(".png"), ref texture);


                int posx = part.TextureRenderTo?.X ?? 0;
                int posy = part.TextureRenderTo?.Y ?? 0;

                capi.EntityTextureAtlas.RenderTextureIntoAtlas(
                    skinTexPos.atlasTextureId,
                    texture,
                    0,
                    0,
                    texture.Width,
                    texture.Height,
                    skinTexPos.x1 * capi.EntityTextureAtlas.Size.Width + posx,
                    skinTexPos.y1 * capi.EntityTextureAtlas.Size.Height + posy,
                    part.Code == "baseskin" ? -1 : 0.005f
                );
            }

            var textures = entity.Properties.Client.Textures;

            var bct = textures[mainTextureCode];
            bct.Baked.TextureSubId = textureSubId;
            textures["skinpart-" + mainTextureCode] = bct;
        }


        public void selectSkinPart(string partCode, string variantCode, bool retesselateShape = true, bool playVoice = true)
        {
            AvailableSkinPartsByCode.TryGetValue(partCode, out var part);

            
            ITreeAttribute appliedTree = skintree.GetTreeAttribute("appliedParts");
            if (appliedTree == null) skintree["appliedParts"] = appliedTree = new TreeAttribute();
            appliedTree[partCode] = new StringAttribute(variantCode);


            if (part?.Type == EnumSkinnableType.Voice)
            {
                entity.WatchedAttributes.SetString(partCode, variantCode);

                if (partCode == "voicetype")
                {
                    VoiceType = variantCode;
                }
                if (partCode == "voicepitch")
                {
                    VoicePitch = variantCode;
                }

                ApplyVoice(VoiceType, VoicePitch, playVoice);
                return;
            }

            var essr = entity.Properties.Client.Renderer as EntityShapeRenderer;
            if (retesselateShape) essr?.TesselateShape();
            return;
        }


        public void ApplyVoice(string voiceType, string voicePitch, bool testTalk)
        {
            if (!AvailableSkinPartsByCode.TryGetValue("voicetype", out var availVoices) || !AvailableSkinPartsByCode.TryGetValue("voicepitch", out var availPitches))
            {
                return;
            }

            VoiceType = voiceType;
            VoicePitch = voicePitch;

            if (entity is EntityPlayer plr && plr.talkUtil != null && voiceType != null) {

                if (!availVoices.VariantsByCode.ContainsKey(voiceType))
                {
                    voiceType = availVoices.Variants[0].Code;
                }

                plr.talkUtil.soundName = availVoices.VariantsByCode[voiceType].Sound;
                
                float pitchMod = 1;
                switch (VoicePitch)
                {
                    case "verylow": pitchMod = 0.6f; break;
                    case "low": pitchMod = 0.8f; break;
                    case "medium": pitchMod = 1f; break;
                    case "high": pitchMod = 1.2f; break;
                    case "veryhigh": pitchMod = 1.4f; break;
                }

                plr.talkUtil.pitchModifier = pitchMod;
                plr.talkUtil.chordDelayMul = 1.1f;

                if (testTalk)
                {
                    plr.talkUtil.Talk(EnumTalkType.Idle);
                }
            }
        }

        protected Shape addSkinPart(AppliedSkinnablePartVariant part, Shape entityShape, string[] disableElements, string shapePathForLogging)
        {
            var skinpart = AvailableSkinPartsByCode[part.PartCode];
            if (skinpart.Type == EnumSkinnableType.Voice)
            {
                entity.WatchedAttributes.SetString("voicetype", part.Code);
                return entityShape;
            }
            
            entityShape.RemoveElements(disableElements);

            var api = entity.World.Api;
            ICoreClientAPI capi = entity.World.Api as ICoreClientAPI;
            AssetLocation shapePath;
            CompositeShape tmpl = skinpart.ShapeTemplate;

            if (part.Shape == null && tmpl != null)
            {
                shapePath = tmpl.Base.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json");
                shapePath.Path = resolveTemplate(shapePath.Path, part?.Code, false);
            }
            else
            {
                shapePath = part.Shape.Base.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json");
            }

            Shape partShape = Shape.TryGet(api, shapePath);
            if (partShape == null)
            {
                api.World.Logger.Warning("Entity skin shape {0} defined in entity config {1} not found or errored, was supposed to be at {2}. Skin part will be invisible.", shapePath, entity.Properties.Code, shapePath);
                return null;
            }

            string prefixcode = "skinpart";
            partShape.SubclassForStepParenting(prefixcode + "-");

            var textures = entity.Properties.Client.Textures;
            entityShape.StepParentShape(partShape, shapePath.ToShortString(), shapePathForLogging, api.Logger, (texcode, loc) =>
            {
                if (capi == null) return;
                
                string textureKey = "skinpart-" + texcode;
                bool isDynamic = skinpart.TextureTemplate != null;

                // If the texture isn't dynamic (no template) and it's already cached, we can skip it.
                // But if it IS dynamic, we must ALWAYS proceed, ignoring the cache to catch dependency changes.
                if (!isDynamic && textures.ContainsKey(textureKey)) return;


                AssetLocation textureLoc;
                if (isDynamic)
                {
                    textureLoc = skinpart.TextureTemplate.Clone();
                    textureLoc.Path = resolveTemplate(textureLoc.Path, part?.Code, false);
                }
                else
                {
                    textureLoc = loc;
                }

                var cmpt = textures[textureKey] = new CompositeTexture(textureLoc);
                cmpt.Bake(api.Assets);
                if (capi.EntityTextureAtlas.GetOrInsertTexture(cmpt.Baked.TextureFilenames[0], out int textureSubid, out _))
                {
                    cmpt.Baked.TextureSubId = textureSubid;
                }
                else
                {
                    api.Logger.Warning("Could not find texture '{0}' for skin part '{1}'.", textureLoc, part.PartCode);
                }
            });

            return entityShape;
        }

        private void loadTexture(Shape entityShape, string code, AssetLocation location, int textureWidth, int textureHeight, string shapePathForLogging)
        {
            if (entity.World.Side == EnumAppSide.Server) return;

            var textures = entity.Properties.Client.Textures;
            ICoreClientAPI capi = entity.World.Api as ICoreClientAPI;

            var cmpt = textures[code] = new CompositeTexture(location);
            cmpt.Bake(capi.Assets);
            if (!capi.EntityTextureAtlas.GetOrInsertTexture(cmpt.Baked.TextureFilenames[0], out int textureSubid, out _, null, -1))
            {
                capi.Logger.Warning("Skin part shape {0} defined texture {1}, no such texture found.", shapePathForLogging, location);
            }
            cmpt.Baked.TextureSubId = textureSubid;

            entityShape.TextureSizes[code] = new int[] { textureWidth, textureHeight };
            textures[code] = cmpt;
         }

        public override string PropertyName()
        {
            return "skinnableplayer";
        }
    }
}
