using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class CharacterSelectedState
    {
        public bool DidSelect;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ClothStack
    {
        public EnumItemClass Class;
        public string Code;
        public int SlotNum;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class CharacterSelectionPacket
    {
        public bool DidSelect;
        public ClothStack[] Clothes;
        public string CharacterClass;
        public Dictionary<string, string> SkinParts;
        public string VoiceType;
        public string VoicePitch;
    }



    public class CharacterSystem : ModSystem
    {
        ICoreAPI api;
        ICoreClientAPI capi;
        ICoreServerAPI sapi;

        GuiDialogCreateCharacter createCharDlg;
        GuiDialogCharacterBase charDlg;

        bool didSelect;

        public List<CharacterClass> characterClasses = new List<CharacterClass>();
        public List<Trait> traits = new List<Trait>();

        public Dictionary<string, CharacterClass> characterClassesByCode = new Dictionary<string, CharacterClass>();

        public Dictionary<string, Trait> TraitsByCode = new Dictionary<string, Trait>();

        public override void Start(ICoreAPI api)
        {
            this.api = api;

            api.Network
                .RegisterChannel("charselection")
                .RegisterMessageType<CharacterSelectionPacket>()
                .RegisterMessageType<CharacterSelectedState>()
            ;

            api.Event.MatchesGridRecipe += Event_MatchesGridRecipe;
        }


        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;

            api.Network.GetChannel("charselection")
                .SetMessageHandler<CharacterSelectedState>(onSelectedState)
            ;

            api.Event.IsPlayerReady += Event_IsPlayerReady;
            api.Event.PlayerJoin += Event_PlayerJoin;

            api.RegisterCommand("charsel", "", "", onCharSelCmd);

            api.Event.BlockTexturesLoaded += loadCharacterClasses;


            charDlg = api.Gui.LoadedGuis.Find(dlg => dlg is GuiDialogCharacterBase) as GuiDialogCharacterBase;
            charDlg.Tabs.Add(new GuiTab() { Name = Lang.Get("charactertab-traits"), DataInt = 1 });
            charDlg.RenderTabHandlers.Add(composeTraitsTab);
        }

        private void composeTraitsTab(GuiComposer compo)
        {
            compo
                .AddRichtext(getClassTraitText(), CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15), ElementBounds.Fixed(0, 25, 385, 200));
        }

        string getClassTraitText()
        {
            string charClass = capi.World.Player.Entity.WatchedAttributes.GetString("characterClass");
            CharacterClass chclass = characterClasses.FirstOrDefault(c => c.Code == charClass);

            StringBuilder fulldesc = new StringBuilder();
            StringBuilder attributes = new StringBuilder();

            //fulldesc.AppendLine(Lang.Get("characterdesc-" + chclass.Code));
            //fulldesc.AppendLine();
            //fulldesc.AppendLine(Lang.Get("traits-title"));

            var chartraits = chclass.Traits.Select(code => TraitsByCode[code]).OrderBy(trait => (int)trait.Type);

            foreach (var trait in chartraits)
            {
                attributes.Clear();
                foreach (var val in trait.Attributes)
                {
                    if (attributes.Length > 0) attributes.Append(", ");
                    attributes.Append(Lang.Get(string.Format(GlobalConstants.DefaultCultureInfo, "charattribute-{0}-{1}", val.Key, val.Value)));
                }

                if (attributes.Length > 0)
                {
                    fulldesc.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + trait.Code), attributes));
                }
                else
                {
                    string desc = Lang.GetIfExists("traitdesc-" + trait.Code);
                    if (desc != null)
                    {
                        fulldesc.AppendLine(Lang.Get("traitwithattributes", Lang.Get("trait-" + trait.Code), desc));
                    }
                    else
                    {
                        fulldesc.AppendLine(Lang.Get("trait-" + trait.Code));
                    }


                }
            }

            if (chclass.Traits.Length == 0)
            {
                fulldesc.AppendLine(Lang.Get("No positive or negative traits"));
            }

            return fulldesc.ToString();
        }






        private void loadCharacterClasses()
        {
            traits = api.Assets.Get("config/traits.json").ToObject<List<Trait>>();
            characterClasses = api.Assets.Get("config/characterclasses.json").ToObject<List<CharacterClass>>();

            foreach (var trait in traits)
            {
                TraitsByCode[trait.Code] = trait;

                /*string col = "#ff8484";
                if (trait.Type == EnumTraitType.Positive) col = "#84ff84";
                if (trait.Type == EnumTraitType.Mixed) col = "#fff584";

                Console.WriteLine("\"trait-" + trait.Code + "\": \"<font color=\\"" + col + "\\">• " + trait.Code + "</font> ({0})\",");*/

                /*foreach (var val in trait.Attributes)
                {
                    Console.WriteLine("\"charattribute-" + val.Key + "-"+val.Value+"\": \"\",");
                }*/
            }

            foreach (var charclass in characterClasses)
            {
                characterClassesByCode[charclass.Code] = charclass;

                foreach (var jstack in charclass.Gear)
                {
                    if (!jstack.Resolve(api.World, "character class gear", false))
                    {
                        api.World.Logger.Warning("Unable to resolve character class gear " + jstack.Type + " with code " + jstack.Code + " item/bloc does not seem to exist. Will ignore.");
                    }
                }
            }
        }


        public void setCharacterClass(EntityPlayer player, string classCode, bool initializeGear = true)
        {
            CharacterClass charclass = characterClasses.FirstOrDefault(c => c.Code == classCode);
            if (charclass == null) throw new ArgumentException("Not a valid character class code!");

            player.WatchedAttributes.SetString("characterClass", charclass.Code);

            if (initializeGear)
            {
                var essr = capi?.World.Player.Entity.Properties.Client.Renderer as EntitySkinnableShapeRenderer;
                if (essr != null) essr.doReloadShapeAndSkin = false;

                IInventory inv = player.GearInventory;
                if (inv != null)
                {
                    for (int i = 0; i < inv.Count; i++)
                    {
                        if (i >= 12) break; // Armor
                        if (!player.GearInventory[i].Empty)
                        {
                            api.World.SpawnItemEntity(player.GearInventory[i].TakeOutWhole(), player.Pos.XYZ);
                        }
                    }


                    foreach (var jstack in charclass.Gear)
                    {
                        // no idea why this is needed here, it yields the wrong item otherwise
                        if (!jstack.Resolve(api.World, "character class gear", false))
                        {
                            api.World.Logger.Warning("Unable to resolve character class gear " + jstack.Type + " with code " + jstack.Code + " item/bloc does not seem to exist. Will ignore.");
                            continue;
                        }

                        ItemStack stack = jstack.ResolvedItemstack?.Clone();
                        if (stack != null)
                        {
                            EnumCharacterDressType dresstype;
                            string strdress = stack.ItemAttributes["clothescategory"].AsString();
                            if (!Enum.TryParse(strdress, true, out dresstype)) return;



                            inv[(int)dresstype].Itemstack = stack;
                            inv[(int)dresstype].MarkDirty();
                        }
                        else
                        {
                            player.TryGiveItemStack(stack);
                        }
                    }

                    if (essr != null)
                    {
                        essr.doReloadShapeAndSkin = true;
                        essr.TesselateShape();
                    }
                }
            }

            applyTraitAttributes(player);
        }

        private void applyTraitAttributes(EntityPlayer eplr)
        {
            string classcode = eplr.WatchedAttributes.GetString("characterClass");
            CharacterClass charclass = characterClasses.FirstOrDefault(c => c.Code == classcode);
            if (charclass == null) throw new ArgumentException("Not a valid character class code!");

            // Reset 
            foreach (var stats in eplr.Stats)
            {
                foreach (var statmod in stats.Value.ValuesByKey)
                {
                    if (statmod.Key == "trait")
                    {
                        stats.Value.Remove(statmod.Key);
                        break;
                    }
                }
            }



            // Then apply
            string[] extraTraits = eplr.WatchedAttributes.GetStringArray("extraTraits");
            var allTraits = extraTraits == null ? charclass.Traits : charclass.Traits.Concat(extraTraits);

            foreach (var traitcode in allTraits)
            {
                Trait trait;
                if (TraitsByCode.TryGetValue(traitcode, out trait))
                {
                    foreach (var val in trait.Attributes)
                    {
                        string attrcode = val.Key;
                        double attrvalue = val.Value;

                        eplr.Stats.Set(attrcode, "trait", (float)attrvalue, true);
                    }
                }
            }

            eplr.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();
        }

        private void onCharSelCmd(int groupId, CmdArgs args)
        {
            if (createCharDlg == null)
            {
                createCharDlg = new GuiDialogCreateCharacter(capi, this);
                createCharDlg.PrepAndOpen();
            }

            if (!createCharDlg.IsOpened())
            {
                createCharDlg.TryOpen();
            }
        }

        private void onSelectedState(CharacterSelectedState p)
        {
            didSelect = p.DidSelect;
        }

        private void Event_PlayerJoin(IClientPlayer byPlayer)
        {
            if (!didSelect && byPlayer.PlayerUID == capi.World.Player.PlayerUID)
            {
                createCharDlg = new GuiDialogCreateCharacter(capi, this);
                createCharDlg.PrepAndOpen();
            }
        }

        private bool Event_IsPlayerReady(ref EnumHandling handling)
        {
            if (didSelect) return true;

            handling = EnumHandling.PreventDefault;
            return false;
        }

        private bool Event_MatchesGridRecipe(IPlayer player, GridRecipe recipe, ItemSlot[] ingredients, int gridWidth)
        {
            if (recipe.RequiresTrait == null) return true;

            string classcode = player.Entity.WatchedAttributes.GetString("characterClass");
            CharacterClass charclass;
            if (classcode == null) return true;

            if (characterClassesByCode.TryGetValue(classcode, out charclass))
            {
                if (charclass.Traits.Contains(recipe.RequiresTrait)) return true;

                string[] extraTraits = player.Entity.WatchedAttributes.GetStringArray("extraTraits");
                if (extraTraits != null && extraTraits.Contains(recipe.RequiresTrait)) return true;
            }

            return false;
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            api.Network.GetChannel("charselection")
                .SetMessageHandler<CharacterSelectionPacket>(onCharacterSelection)
            ;

            api.Event.PlayerJoin += Event_PlayerJoinServer;
            api.Event.ServerRunPhase(EnumServerRunPhase.ModsAndConfigReady, loadCharacterClasses);
        }

        private void Event_PlayerJoinServer(IServerPlayer byPlayer)
        {
            didSelect = SerializerUtil.Deserialize(byPlayer.GetModdata("createCharacter"), false);

            if (!didSelect)
            {
                randomizeSkin(byPlayer);
                setCharacterClass(byPlayer.Entity, characterClasses[0].Code, false);
            }

            sapi.Network.GetChannel("charselection").SendPacket(new CharacterSelectedState() { DidSelect = didSelect }, byPlayer);
        }

        private void randomizeSkin(IServerPlayer byPlayer)
        {
            var skinMod = byPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();

            foreach (var skinpart in skinMod.AvailableSkinParts)
            {
                int index = api.World.Rand.Next(skinpart.Variants.Length);

                if ((skinpart.Code == "mustache" || skinpart.Code == "beard") && api.World.Rand.NextDouble() < 0.5)
                {
                    index = 0;
                }

                string variantCode = skinpart.Variants[index].Code;

                skinMod.selectSkinPart(skinpart.Code, variantCode);
            }
        }

        private void onCharacterSelection(IServerPlayer fromPlayer, CharacterSelectionPacket p)
        {
            bool didSelectBefore = SerializerUtil.Deserialize(fromPlayer.GetModdata("createCharacter"), false);
            if (didSelectBefore && fromPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                fromPlayer.BroadcastPlayerData(true);
                fromPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
                return;
            }

            if (p.DidSelect)
            {
                fromPlayer.SetModdata("createCharacter", SerializerUtil.Serialize<bool>(p.DidSelect));

                setCharacterClass(fromPlayer.Entity, p.CharacterClass, !didSelectBefore || fromPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative);

                var bh = fromPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
                bh.ApplyVoice(p.VoiceType, p.VoicePitch, false);

                foreach (var skinpart in p.SkinParts)
                {
                    bh.selectSkinPart(skinpart.Key, skinpart.Value, false);
                }
            }

            fromPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
            fromPlayer.BroadcastPlayerData(true);
        }


        internal void ClientSelectionDone(IInventory characterInv, string characterClass, bool didSelect)
        {
            List<ClothStack> clothesPacket = new List<ClothStack>();
            for (int i = 0; i < characterInv.Count; i++)
            {
                ItemSlot slot = characterInv[i];
                if (slot.Itemstack == null) continue;

                clothesPacket.Add(new ClothStack()
                {
                    Code = slot.Itemstack.Collectible.Code.ToShortString(),
                    SlotNum = i,
                    Class = slot.Itemstack.Class
                });
            }

            Dictionary<string, string> skinParts = new Dictionary<string, string>();
            var bh = capi.World.Player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();

            var applied = bh.AppliedSkinParts;
            foreach (var val in applied)
            {
                skinParts[val.PartCode] = val.Code;
            }

            capi.Network.GetChannel("charselection").SendPacket(new CharacterSelectionPacket()
            {
                Clothes = clothesPacket.ToArray(),
                DidSelect = didSelect,
                SkinParts = skinParts,
                CharacterClass = characterClass,
                VoicePitch = bh.VoicePitch,
                VoiceType = bh.VoiceType
            });

            capi.Network.SendPlayerNowReady();

            createCharDlg = null;
        }
    }
}
