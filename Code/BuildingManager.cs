using Godot;
using System.Collections.Generic;

public partial class BuildingManager : Node2D
{
    // -----------------------------------------
    // ENUMS & TYPES
    // -----------------------------------------
    public enum FootprintType
    {
        Single1x1,
        DynamicHorizontal2x1
    }

    // -----------------------------------------
    // INSPECTOR VARIABLES
    // -----------------------------------------
    [Export]
    public TileMapLayer GroundTileMap;

    [Export]
    public Godot.Collections.Array<PackedScene> BuildingScenes = new Godot.Collections.Array<PackedScene>();

    [Export]
    public Godot.Collections.Array<FootprintType> BuildingFootprints = new Godot.Collections.Array<FootprintType>();

    [Export]
    public Vector2 BuildingVisualOffset = new Vector2(0, -16);

    // -----------------------------------------
    // TILE IDENTIFIERS
    // -----------------------------------------
    private readonly int TargetSourceId = 1;
    private readonly Vector2I TargetAtlasCoord = new Vector2I(0, 0);

    // -----------------------------------------
    // STATE TRACKING
    // -----------------------------------------
    private int currentSelectedIndex = 0;

    // 0 = Down-Right, 1 = Down-Left, 2 = Up-Left, 3 = Up-Right
    private int buildingRotation = 0;

    // -----------------------------------------
    // THE TILE DATA CLASS
    // -----------------------------------------
    public class CityTile
    {
        public Vector2I GridPosition;
        public bool IsOccupied;
        public List<CityTile> Neighbors;
        public Node2D BuildingNode;

        public CityTile(Vector2I gridPosition)
        {
            GridPosition = gridPosition;
            IsOccupied = false;
            Neighbors = new List<CityTile>();
            BuildingNode = null;
        }
    }

    private Dictionary<Vector2I, CityTile> buildableTiles = new Dictionary<Vector2I, CityTile>();

    // -----------------------------------------
    // INITIALIZATION
    // -----------------------------------------
    public override void _Ready()
    {
        if (GroundTileMap == null)
        {
            GD.PrintErr("CRITICAL: Missing GroundTileMap in Inspector!");
            return;
        }

        if (BuildingScenes.Count != BuildingFootprints.Count)
        {
            GD.PrintErr("CRITICAL: BuildingScenes and BuildingFootprints array sizes must match!");
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

            if (actualSourceId == TargetSourceId && actualAtlasCoord == TargetAtlasCoord)
            {
                buildableTiles.Add(cell, new CityTile(cell));
            }
        }

        GD.Print($"System initialized. Found {buildableTiles.Count} valid building spaces.");
    }

    private void ConnectNeighbors()
    {
        Vector2I[] directions = { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right };

        foreach (var kvp in buildableTiles)
        {
            CityTile currentTile = kvp.Value;

            foreach (Vector2I dir in directions)
            {
                Vector2I neighborPos = currentTile.GridPosition + dir;
                if (buildableTiles.ContainsKey(neighborPos))
                {
                    currentTile.Neighbors.Add(buildableTiles[neighborPos]);
                }
            }
        }
    }

    // -----------------------------------------
    // INPUT & ROUTING
    // -----------------------------------------
    public override void _Input(InputEvent @event)
    {
        // 1. Detect Key Press: Numbers (0-9) to select building, 'R' to rotate
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            int pressedNumber = (int)keyEvent.Keycode - (int)Key.Key0;

            if (pressedNumber >= 0 && pressedNumber <= 9)
            {
                SelectBuilding(pressedNumber);
            }
            else if (keyEvent.Keycode == Key.R)
            {
                // Rotate placement orientation (0 -> 1 -> 2 -> 3 -> 0)
                buildingRotation = (buildingRotation + 1) % 4;
                GD.Print($"Building rotation changed to index: {buildingRotation}");
            }
        }

        // 2. Detect Left Mouse Click to place building
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            Vector2 localMousePos = GroundTileMap.ToLocal(GetGlobalMousePosition());
            Vector2I clickGridPos = GroundTileMap.LocalToMap(localMousePos);

            if (!buildableTiles.ContainsKey(clickGridPos)) return;

            FootprintType activeFootprint = BuildingFootprints[currentSelectedIndex];

            if (activeFootprint == FootprintType.Single1x1)
            {
                TryPlaceSingleTile(clickGridPos);
            }
            else if (activeFootprint == FootprintType.DynamicHorizontal2x1)
            {
                TryPlaceRotatedTwoTile(clickGridPos);
            }
        }
    }

    private void SelectBuilding(int index)
    {
        if (index < BuildingScenes.Count && index < BuildingFootprints.Count && BuildingScenes[index] != null)
        {
            currentSelectedIndex = index;
            GD.Print($"Switched building tool to index {index} ({BuildingFootprints[index]})");
        }
        else
        {
            GD.Print($"Slot {index} is empty or unassigned in Inspector!");
        }
    }

    // -----------------------------------------
    // PLACEMENT LOGIC
    // -----------------------------------------
    private void TryPlaceSingleTile(Vector2I clickGridPos)
    {
        CityTile clickedTile = buildableTiles[clickGridPos];
        if (!clickedTile.IsOccupied)
        {
            CommitBuilding(new List<CityTile> { clickedTile }, clickedTile);
        }
        else
        {
            GD.Print("Cannot build: Tile is already occupied!");
        }
    }

    private void TryPlaceRotatedTwoTile(Vector2I pivotGridPos)
    {
        CityTile pivotTile = buildableTiles[pivotGridPos];
        if (pivotTile.IsOccupied)
        {
            GD.Print("Cannot build: Pivot tile is already occupied!");
            return;
        }

        // SWAPPED TO SIDES:
        // 'Corner' refers to straight vertical/horizontal in Staggered Iso.
        // 'Side' refers to the 4 diagonal axes of the isometric diamond!
        TileSet.CellNeighbor targetNeighborEnum = buildingRotation switch
        {
            0 => TileSet.CellNeighbor.BottomRightSide, // Isometric Down-Right
            1 => TileSet.CellNeighbor.BottomLeftSide,  // Isometric Down-Left
            2 => TileSet.CellNeighbor.TopLeftSide,     // Isometric Up-Left
            3 => TileSet.CellNeighbor.TopRightSide,    // Isometric Up-Right
            _ => TileSet.CellNeighbor.BottomRightSide
        };

        // Query TileMapLayer directly for the exact isometric neighbor
        Vector2I neighborGridPos = GroundTileMap.GetNeighborCell(pivotGridPos, targetNeighborEnum);

        if (!buildableTiles.TryGetValue(neighborGridPos, out CityTile secondTile) || secondTile.IsOccupied)
        {
            GD.Print($"Cannot place: Tile at {targetNeighborEnum} is occupied or out of bounds!");
            return;
        }

        // Flip sprite horizontally when oriented towards the left (rotations 1 and 2)
        bool shouldFlipSprite = (buildingRotation == 1 || buildingRotation == 2);

        CommitBuilding(new List<CityTile> { pivotTile, secondTile }, pivotTile, shouldFlipSprite);
    }

    // -----------------------------------------
    // COMMIT & SPAWN
    // -----------------------------------------
    private void CommitBuilding(List<CityTile> tilesToOccupy, CityTile pivotTile, bool flipHorizontal = false)
    {
        // 1. Mark tiles as occupied in the data grid
        foreach (CityTile tile in tilesToOccupy)
        {
            tile.IsOccupied = true;
        }

        // 2. Instantiate building scene
        Node2D newBuilding = BuildingScenes[currentSelectedIndex].Instantiate<Node2D>();
        AddChild(newBuilding);

        // 3. Mirror sprite horizontally if placing towards the left
        if (flipHorizontal)
        {
            newBuilding.Scale = new Vector2(-1, 1);
        }

        // 4. Position and depth-sort relative to the pivot tile
        Vector2 tileCenter = GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(pivotTile.GridPosition));
        newBuilding.GlobalPosition = tileCenter + BuildingVisualOffset;
        newBuilding.ZIndex = Mathf.RoundToInt(newBuilding.GlobalPosition.Y) + 10;

        // 5. Save building node reference in each occupied tile
        foreach (CityTile tile in tilesToOccupy)
        {
            tile.BuildingNode = newBuilding;
        }

        GD.Print($"Built {newBuilding.Name} occupying {tilesToOccupy.Count} tile(s). Rotation index: {buildingRotation}");
    }
}