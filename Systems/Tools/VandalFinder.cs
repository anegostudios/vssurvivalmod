using System;
using System.Collections.Generic;
using Vintagestory.API;

namespace Vintagestory.ServerMods
{
	public class VandalFinder : ModBase
	{
        public override bool ShouldLoad(EnumApplicationSide side)
        {
            return side == EnumApplicationSide.Server;
        }


        public override void DeclareDependencies(ICoreAPI m)
		{
			//m.RequireMod("ModBasicBlocksLoader");
			m.RequireMod("Vintagestory.ServerMods.BuildLog");
		}

		public override void StartServerSide(ICoreAPI manager)
		{
			m = manager;
			/*m.World.SetBlockType("VandalFinder", new Block()
			               {
			               	AllTextures = "VandalFinder",
			               	DrawType = EnumDrawType.Solid,
			               	MatterState = EnumMatterState.Solid,
			               	IsUsable = true,
			               	IsTool = true,
			               });
			m.World.AddToCreativeInventory("VandalFinder");*/
			//m.RegisterOnEvent.BlockUseWithTool(OnUseWithTool);
			lines = (List<object[]>)m.World.GetGlobalDataNotSaved("LogLines");
		}
		
		ICoreAPI m;
		List<object[]> lines = new List<object[]>();
		
		void OnUseWithTool(int player, int x, int y, int z, int blockid)
		{
			if (m.World.GetBlockType(blockid).Code == "VandalFinder")
			{
				ShowBlockLog(player, x, y, z);
			}
		}
		
		void ShowBlockLog(int player, int x, int y, int z)
		{
			List<string> messages = new List<string>();
			for (int i = lines.Count - 1; i >= 0; i--)
			{
				object[] l = lines[i];
				int lx = (short)l[1];
				int ly = (short)l[2];
				int lz = (short)l[3];
				DateTime ltimestamp = (DateTime)l[0];
				string lplayername = (string)l[6];
				int blockid = (short)l[4];
				bool lbuild = (bool)l[5];
				if (lx == x && ly == y && lz == z)
				{
					messages.Add(string.Format("{0} {1} {2} {3}", ltimestamp.ToString(), lplayername, m.World.GetBlockType(blockid).Code, lbuild ? "build" : "delete"));
					if (messages.Count > 10)
					{
						return;
					}
				}
			}
			messages.Reverse();
			for (int i = 0; i < messages.Count; i++)
			{
				m.Player.SendMessage(player, GlobalProperties.CurrentChatGroup, messages[i], EnumChatType.CommandSuccess);
			}
			if (messages.Count == 0)
			{
				m.Player.SendMessage(player, GlobalProperties.CurrentChatGroup, "Block was never changed.", EnumChatType.CommandSuccess);
			}
		}
	}
}
