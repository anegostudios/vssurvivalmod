using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.Common.Collectible.Block;

namespace Vintagestory.ServerMods;

public class DungeonGenWorkspace
{
    public TiledDungeon DungeonGenerator;
    public int MinTiles;
    public int MaxTiles;

    public int PlacedTiles; // This is not the same as PlaceTasks.Count

    /// <summary>
    /// The list of open connections. That have nothing connected to yet
    /// </summary>
    public List<ConnectorMetaData> OpenSet;

    public List<TilePlaceTask> PlaceTasks;
    public List<GeneratedStructure> ExistingStructures;
    public List<GeneratedStructure> GeneratedStructures;
    public Dictionary<string, int> TileQuantityByCode;

    public List<DungeonTile> CanGenerate { get; set; }

    public List<DungeonTile> MustGenerate { get; set; }

    public Dictionary<string, int> GroupMaxCount = new Dictionary<string, int>();


    public List<ConnectorMetaData>? ParentOpenSet;

    public DungeonGenWorkspace ParentWorkspace;

    public DungeonGenWorkspace(TiledDungeon dungeon, int minTiles, int maxTiles, List<ConnectorMetaData> openSet, List<TilePlaceTask> placeTasks, List<GeneratedStructure> existingStructures, List<GeneratedStructure> generatedStructures, List<ConnectorMetaData>? parentOpenSet)
    {
        DungeonGenerator = dungeon;
        MinTiles = minTiles;
        MaxTiles = maxTiles;
        OpenSet = openSet;
        PlaceTasks = placeTasks;
        ExistingStructures = existingStructures;
        GeneratedStructures = generatedStructures;
        ParentOpenSet = parentOpenSet;
        MustGenerate = new List<DungeonTile>();
        CanGenerate = new List<DungeonTile>();
        TileQuantityByCode = new Dictionary<string, int>();

        for (int i = 0; i < DungeonGenerator.Tiles.Count; i++)
        {
            var tile = DungeonGenerator.Tiles[i];
            TileQuantityByCode[tile.Code] = 0;

            if (tile.Min > 0)
            {
                for (int j = 0; j < tile.Min; j++) MustGenerate.Add(tile);
            }

            if (tile.Max > tile.Min) CanGenerate.Add(tile);
        }

        if (dungeon.GroupMax != null)
        {
            foreach (var (code,_) in dungeon.GroupMax)
            {
                GroupMaxCount[code] = 0;
            }
        }
    }

    public void OnTileAdded(string code, int cnt = 1)
    {
        if (TileQuantityByCode.TryGetValue(code, out var quantity))
        {
            TileQuantityByCode[code] = quantity + cnt;
        }
        else
        {
            TileQuantityByCode[code] = cnt;
        }
    }

    public DungeonGenWorkspace SpawnChild(DungeonTile tile)
    {
        var ws = new DungeonGenWorkspace(
            tile.TileGenerator,
            tile.TileGenerator.MinTiles,
            tile.TileGenerator.MaxTiles,
            new List<ConnectorMetaData>(),
            new List<TilePlaceTask>(),
            ExistingStructures,
            new List<GeneratedStructure>(),
            null
        );
        ws.ParentWorkspace = this;

        return ws;
    }

    public void Reset(ConnectorMetaData openside, List<GeneratedStructure> dgdGeneratedStructures)
    {
        OpenSet.Clear();
        OpenSet.Add(openside);
        GeneratedStructures.Clear();
        GeneratedStructures.AddRange(dgdGeneratedStructures);
        PlaceTasks.Clear();
        ParentOpenSet?.Clear();
        PlacedTiles = 0;
        TileQuantityByCode.Clear();
    }



    public void CommitToParent(bool debugLogging, List<string> debugLogs)
    {
        if (debugLogging) debugLogs.Add("Leaving child dungeon");

        ParentWorkspace.PlaceTasks.AddRange(PlaceTasks);
        string added = "";

        foreach (var val in OpenSet)
        {
            if (val.TargetsForParent != null)
            {
                var conn = new ConnectorMetaData(val.Position, val.Facing, val.Rotation, val.Name, val.Targets.Concat(val.TargetsForParent).ToArray(), null);
                ParentWorkspace.OpenSet.Add(conn);

                if (debugLogging) added += conn.ToString();
            } else
            {
                // We'll ditch unclosed connectors unless they are required to be opened
                if (DungeonGenerator.RequireOpened != null && DungeonGenerator.RequireOpened.Contains(val.Name))
                {
                    ParentWorkspace.OpenSet.Add(val);
                    if (debugLogging) added += val.ToString();
                }
            }
        }

        if (debugLogging && added.Length > 0) debugLogs.Add("Moving opened connectors to parent: " + added);

        ParentWorkspace.GeneratedStructures.Clear();
        ParentWorkspace.GeneratedStructures.AddRange(GeneratedStructures);
        foreach (var (code, quantity) in TileQuantityByCode)
        {
            ParentWorkspace.OnTileAdded(code, quantity);
        }
    }
}
