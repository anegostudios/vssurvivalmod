using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API;

namespace Vintagestory.ServerMods
{
	public class BuildLog : ModBase
	{
        IBlockAccesor blockAccessor;

        public override bool ShouldLoad(EnumApplicationSide side)
        {
            return side == EnumApplicationSide.Server;
        }


        public override void DeclareDependencies(ICoreAPI api)
		{
            //m.RequireMod("ModBasicBlocksLoader");
            blockAccessor = api.World.GetBlockAccessor(false, false, false);
        }

		public override void StartServerSide(ICoreAPI manager)
		{
			m = manager;
            // Disabled until its needed
			/*m.RegisterOnBlockBuild(OnBuild);
			m.RegisterOnBlockDelete(OnDelete);
			m.RegisterOnGameWorldLoad(OnLoad);
			m.RegisterOnGameWorldSave(OnSave);
			m.SetGlobalDataNotSaved("LogLines", lines);*/
		}
		ICoreAPI m;
		public int MaxEntries = 50 * 1000;

		//can't pass LogLine object between mods. Store object as an array of fields instead.
		List<object[]> lines = new List<object[]>();

		void OnLoad()
		{
			try
			{
				byte[] b = m.World.GetGlobalData("BuildLog");
				if (b != null)
				{
					MemoryStream ms = new MemoryStream(b);
					BinaryReader br = new BinaryReader(ms);
					int count = br.ReadInt32();
					for (int i = 0; i < count; i++)
					{
						var l = new object[8];
						l[0] = new DateTime(br.ReadInt64());//timestamp
						l[1] = br.ReadInt16();//x
						l[2] = br.ReadInt16();//y
						l[3] = br.ReadInt16();//z
						l[4] = br.ReadUInt16();//blocktype
						l[5] = br.ReadBoolean();//build
						l[6] = br.ReadString();//playername
						l[7] = br.ReadString();//ip
						lines.Add(l);
					}
				}
			}
			catch
			{
				//corrupted
				OnSave();
			}
		}

		void OnSave()
		{
			MemoryStream ms = new MemoryStream();
			BinaryWriter bw = new BinaryWriter(ms);
			bw.Write((int)lines.Count);
			for (int i = 0; i < lines.Count; i++)
			{
				object[] l = lines[i];
				bw.Write((long)((DateTime)l[0]).Ticks);//timestamp
				bw.Write((short)l[1]);//x
				bw.Write((short)l[2]);//y
				bw.Write((short)l[3]);//z
				bw.Write((ushort)l[4]);//blocktype
				bw.Write((bool)l[5]);//build
				bw.Write((string)l[6]);//playername
				bw.Write((string)l[7]);//ip
			}
			m.World.SetGlobalData("BuildLog", ms.ToArray());
		}

		void OnBuild(int player, int x, int y, int z)
		{
			lines.Add(new object[]
			          {
			          	DateTime.UtcNow,//timestamp
			          	(short)x, //x
			          	(short)y, //y
			          	(short)z, //z
			          	blockAccessor.GetBlockId(x, y, z), //blocktype
			          	true, //build
			          	m.Player.PlayerName(player), //playername
			          	m.Player.IpAddress(player), //ip
			          });
			if (lines.Count > MaxEntries)
			{
				lines.RemoveRange(0, 1000);
			}
		}

		void OnDelete(int player, int x, int y, int z, ushort oldblock)
		{
			lines.Add(new object[]
			          {
			          	DateTime.UtcNow, //timestamp
			          	(short)x, //x
			          	(short)y, //y
			          	(short)z, //z
			          	oldblock, //blocktype
			          	false, //build
			          	m.Player.PlayerName(player), //playername
			          	m.Player.IpAddress(player), //ip
			          });
			if (lines.Count > MaxEntries)
			{
				lines.RemoveRange(0, 1000);
			}
		}
	}
}
