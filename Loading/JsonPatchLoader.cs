using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tavis;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace Vintagestory.ServerMods.NoObf
{
    public enum EnumJsonPatchOp {
        Add,
        Remove,
        Replace,
        Copy,
        Move
    }

    public class JsonPatch
    {
        public EnumJsonPatchOp Op;
        public AssetLocation File;
        public string FromPath;
        public string Path;
        public EnumAppSide SideType = EnumAppSide.Universal;

        [JsonProperty, JsonConverter(typeof(JsonAttributesConverter))]
        public JsonObject Value;
    }

    public class ModJsonPatchLoader : ModSystem
    {
        ICoreAPI api;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }

        public override double ExecuteOrder()
        {
            return 0.05;
        }

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            List<IAsset> entries = api.Assets.GetMany("patches/");

            int appliedCount = 0;
            int notfoundCount = 0;
            int errorCount = 0;
            int totalCount = 0;

            foreach (IAsset asset in entries)
            {
                JsonPatch[] patches = null;
                try
                {
                    patches = asset.ToObject<JsonPatch[]>();
                } catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading patches file {0}: {1}", asset.Location, e);
                }

                for (int j = 0; patches != null && j < patches.Length; j++)
                {
                    totalCount++;
                    ApplyPatch(j, asset.Location, patches[j], ref appliedCount, ref notfoundCount, ref errorCount);
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("JsonPatch Loader: ");

            if (totalCount == 0)
            {
                sb.Append(Lang.Get("Nothing to patch", totalCount));
            }
            else
            {

                sb.Append(Lang.Get("{0} patches total", totalCount));

                if (appliedCount > 0)
                {
                    sb.Append(Lang.Get(", successfully applied {0} patches", appliedCount));
                }

                if (notfoundCount > 0)
                {
                    sb.Append(Lang.Get(", missing files on {0} patches", notfoundCount));
                }

                if (errorCount > 0)
                {
                    sb.Append(Lang.Get(", had errors on {0} patches", errorCount));
                }
                else
                {
                    sb.Append(Lang.Get(", no errors", errorCount));
                }
            }

            
            api.World.Logger.Notification(sb.ToString());
            base.Start(api);
        }


        private void ApplyPatch(int patchIndex, AssetLocation patchSourcefile, JsonPatch jsonPatch, ref int applied, ref int notFound, ref int errorCount)
        {
            if (jsonPatch.SideType != EnumAppSide.Universal && jsonPatch.SideType != api.Side) return;

            var path = jsonPatch.File.Path;
            if (!path.EndsWith(".json")) path += ".json";

            var asset = api.Assets.TryGet(path);
            if (asset == null)
            {
                notFound++;
                return;
            }

            Operation op = null;
            switch (jsonPatch.Op)
            {
                case EnumJsonPatchOp.Add:
                    if (jsonPatch.Value == null)
                    {
                        api.World.Logger.Error("Patch {0} in {1} failed probably because it is an add operation and the value property is not set or misspelled", patchIndex, patchSourcefile);
                        errorCount++;
                        return;
                    }
                    op = new AddOperation() { Path = new Tavis.JsonPointer(jsonPatch.Path), Value = jsonPatch.Value.Token };
                    break;
                case EnumJsonPatchOp.Remove:
                    op = new RemoveOperation() { Path = new Tavis.JsonPointer(jsonPatch.Path) };
                    break;
                case EnumJsonPatchOp.Replace:
                    if (jsonPatch.Value == null)
                    {
                        api.World.Logger.Error("Patch {0} in {1} failed probably because it is a replace operation and the value property is not set or misspelled", patchIndex, patchSourcefile);
                        errorCount++;
                        return;
                    }

                    op = new ReplaceOperation() { Path = new Tavis.JsonPointer(jsonPatch.Path), Value = jsonPatch.Value.Token };
                    break;
                case EnumJsonPatchOp.Copy:
                    op = new CopyOperation() { Path = new Tavis.JsonPointer(jsonPatch.Path), FromPath = new JsonPointer(jsonPatch.FromPath) };
                    break;
                case EnumJsonPatchOp.Move:
                    op = new MoveOperation() { Path = new Tavis.JsonPointer(jsonPatch.Path), FromPath = new JsonPointer(jsonPatch.FromPath) };
                    break;
            }

            PatchDocument patchdoc = new PatchDocument(op);
            JToken token = null;
            try
            {
                token = JToken.Parse(asset.ToText());
            }
            catch (Exception e)
            {
                api.World.Logger.Error("Patch {0} in {1} failed probably because the syntax of the value is broken: {2}", patchIndex, patchSourcefile, e);
                errorCount++;
                return;
            }
            
            try
            {
                patchdoc.ApplyTo(token);
            }
            catch (Tavis.PathNotFoundException p)
            {
                api.World.Logger.Error("Patch {0} in {1} failed because supplied path {2} is invalid: {3}", patchIndex, patchSourcefile, jsonPatch.Path, p.Message);
                errorCount++;
                return;
            }
            catch (Exception e)
            {
                api.World.Logger.Error("Patch {0} in {1} failed, following Exception was thrown: {2}", patchIndex, patchSourcefile, e.Message);
                errorCount++;
                return;
            }

            string text = token.ToString();
            asset.Data = System.Text.Encoding.UTF8.GetBytes(text);

            applied++;
        }
        
    }
}
