using Godot;
using System.Collections.Generic;

public partial class BuildingManager : Node2D
{
    // -----------------------------------------
    // INSPECTOR VARIABLES
    // -----------------------------------------
    [Export]
    public TileMapLayer GroundTileMap;

    [Export]
    public PackedScene HouseScene;

    [Export]
    public Vector2 BuildingVisualOffset = new Vector2(0, -16); // Tweak this in the editor!

    // -----------------------------------------
    // TILE IDENTIFIERS
    // -----------------------------------------
    // Change these to match your specific "building space" tile on your sprite sheet
    private readonly int TargetSourceId = 1;
    private readonly Vector2I TargetAtlasCoord = new Vector2I(0, 0);

    // -----------------------------------------
    // THE TILE DATA CLASS
    // -----------------------------------------
    public class CityTile
    {
        public Vector2I GridPosition;
        public bool IsOccupied;
        public List<CityTile> Neighbors;

        // Tracks the actual physical building sitting on this tile
        public Node2D BuildingNode;

        public CityTile(Vector2I gridPosition)
        {
            GridPosition = gridPosition;
            IsOccupied = false;
            Neighbors = new List<CityTile>();
            BuildingNode = null;
        }
    }

    // Our master dictionary connecting grid coordinates to our smart data
    private Dictionary<Vector2I, CityTile> buildableTiles = new Dictionary<Vector2I, CityTile>();

    // -----------------------------------------
    // INITIALIZATION
    // -----------------------------------------
    public override void _Ready()
    {
        if (GroundTileMap == null)
        {
            GD.PrintErr("CRITICAL: Missing TileMapLayer or HouseScene in Inspector!");
            return;
        }

        ScanForBuildableTiles();
        ConnectNeighbors();
    }

    private void ScanForBuildableTiles()
    {
        Godot.Collections.Array<Vector2I> usedCells = GroundTileMap.GetUsedCells();

        foreach (Vector2I cell in usedCells)
        {
            int actualSourceId = GroundTileMap.GetCellSourceId(cell);
            Vector2I actualAtlasCoord = GroundTileMap.GetCellAtlasCoords(cell);

            // Check BOTH the sprite sheet ID and the specific tile coordinates
            if (actualSourceId == TargetSourceId && actualAtlasCoord == TargetAtlasCoord)
            {
                buildableTiles.Add(cell, new CityTile(cell));
            }
        }

        GD.Print($"System initialized. Found {buildableTiles.Count} valid building spaces.");
    }

    private void ConnectNeighbors()
    {
        // The four cardinal directions on a grid
        Vector2I[] directions = { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right };

        foreach (var kvp in buildableTiles)
        {
            CityTile currentTile = kvp.Value;

            foreach (Vector2I dir in directions)
            {
                Vector2I neighborPos = currentTile.GridPosition + dir;

                // If a tile exists at the neighbor position in our dictionary, link them!
                if (buildableTiles.ContainsKey(neighborPos))
                {
                    currentTile.Neighbors.Add(buildableTiles[neighborPos]);
                }
            }
        }
    }

    // -----------------------------------------
    // INTERACTION & BUILDING
    // -----------------------------------------
    public override void _Input(InputEvent @event)
    {
        // Detect a Left Mouse Click
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            // Convert mouse pixels to grid coordinates
            Vector2 localMousePos = GroundTileMap.ToLocal(GetGlobalMousePosition());
            Vector2I clickGridPos = GroundTileMap.LocalToMap(localMousePos);

            // Check if the clicked tile is in our smart dictionary
            if (buildableTiles.TryGetValue(clickGridPos, out CityTile clickedTile))
            {
                // Ensure it's not already occupied
                if (!clickedTile.IsOccupied)
                {
                    BuildHouse(clickedTile);
                }
                else
                {
                    GD.Print("Cannot build: Tile is already occupied!");
                }
            }
        }
    }

    private void BuildHouse(CityTile tile)
    {


        // 1. Mark as occupied in the data
        tile.IsOccupied = true;

        // 2. Instantiate the physical house scene
        Node2D newHouse = HouseScene.Instantiate<Node2D>();

        // Add it to the scene tree (making it a child of this manager)
        AddChild(newHouse);

        // 3. Calculate the exact center of the grid tile in world space
        Vector2 tileCenter = GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(tile.GridPosition));

        // 4. Position the house with your custom visual offset applied
        newHouse.GlobalPosition = tileCenter + BuildingVisualOffset;

        // We use Mathf.RoundToInt because Z-Index requires an integer.
        // We add a base number (like 10) to guarantee it's always > 0 
        // and stays strictly above your TileMap (which defaults to Z-Index 0).
        newHouse.ZIndex = Mathf.RoundToInt(newHouse.GlobalPosition.Y) + 10;

        // 5. Tell the data grid exactly which node is sitting here!
        tile.BuildingNode = newHouse;

        GD.Print($"Built at {tile.GridPosition}! Tracking node: {tile.BuildingNode.Name}. Neighbors: {tile.Neighbors.Count}");
    }
}