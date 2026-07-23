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
    [Export] public TileMapLayer GroundTileMap;

    [Export]
    public Godot.Collections.Array<PackedScene> BuildingScenes = new Godot.Collections.Array<PackedScene>();

    [Export]
    public Godot.Collections.Array<FootprintType> BuildingFootprints = new Godot.Collections.Array<FootprintType>();

    [Export] public Vector2 BuildingVisualOffset = new Vector2(0, -16);
    [Export] public float PreviewLerpSpeed = 25.0f;

    [Export] public Color ValidColor = new Color(0.2f, 1.0f, 0.2f, 0.6f);
    [Export] public Color InvalidColor = new Color(1.0f, 0.2f, 0.2f, 0.6f);

    // -----------------------------------------
    // TILE IDENTIFIERS
    // -----------------------------------------
    private readonly int TargetSourceId = 1;
    private readonly Vector2I TargetAtlasCoord = new Vector2I(0, 0);

    // -----------------------------------------
    // STATE TRACKING
    // -----------------------------------------
    private int currentSelectedIndex = 0;

    // Manual rotation state: 0 = Down-Right, 1 = Down-Left, 2 = Up-Left, 3 = Up-Right
    private int buildingRotation = 0;

    private Node2D previewNode = null;
    private Vector2 targetPreviewPosition = Vector2.Zero;

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
    // INITIALIZATION & PROCESS
    // -----------------------------------------
    public override void _Ready()
    {
        if (GroundTileMap == null || BuildingScenes.Count != BuildingFootprints.Count)
        {
            GD.PrintErr("CRITICAL: Check GroundTileMap reference and array sizes in Inspector!");
            return;
        }

        ScanForBuildableTiles();
        ConnectNeighbors();
        RefreshPreviewNode();
    }

    public override void _Process(double delta)
    {
        UpdatePreviewPositionAndValidation((float)delta);
    }

    private void ScanForBuildableTiles()
    {
        Godot.Collections.Array<Vector2I> usedCells = GroundTileMap.GetUsedCells();

        foreach (Vector2I cell in usedCells)
        {
            if (GroundTileMap.GetCellSourceId(cell) == TargetSourceId && GroundTileMap.GetCellAtlasCoords(cell) == TargetAtlasCoord)
            {
                buildableTiles.Add(cell, new CityTile(cell));
            }
        }
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
        // 1. Keyboard Inputs
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            int pressedNumber = (int)keyEvent.Keycode - (int)Key.Key0;

            if (pressedNumber >= 0 && pressedNumber <= 9)
            {
                SelectBuilding(pressedNumber);
            }
            else if (keyEvent.Keycode == Key.R)
            {
                RotateBuilding();
            }
        }

        // 2. Mouse Click Input
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
                TryPlaceTwoTile(clickGridPos);
            }
        }
    }

    private void SelectBuilding(int index)
    {
        if (index < BuildingScenes.Count && index < BuildingFootprints.Count && BuildingScenes[index] != null)
        {
            currentSelectedIndex = index;
            buildingRotation = 0; // Reset rotation state on tool swap
            RefreshPreviewNode();
        }
    }

    private void RotateBuilding()
    {
        FootprintType activeFootprint = BuildingFootprints[currentSelectedIndex];

        if (activeFootprint == FootprintType.Single1x1)
        {
            buildingRotation = (buildingRotation + 1) % 2; // Toggles 0 and 1 for 1x1
        }
        else
        {
            buildingRotation = (buildingRotation + 1) % 4; // Cycles 0..3 for 2x1 manual rotation
        }

        UpdatePreviewTransform(GetTargetNeighborEnum());
    }

    // -----------------------------------------
    // ROTATION & TRANSFORM HELPERS
    // -----------------------------------------

    private TileSet.CellNeighbor GetTargetNeighborEnum()
    {
        return buildingRotation switch
        {
            0 => TileSet.CellNeighbor.BottomRightSide, // Down-Right
            1 => TileSet.CellNeighbor.BottomLeftSide,  // Down-Left
            2 => TileSet.CellNeighbor.TopLeftSide,     // Up-Left
            3 => TileSet.CellNeighbor.TopRightSide,    // Up-Right
            _ => TileSet.CellNeighbor.BottomRightSide
        };
    }

    private void GetTwoByOneCustomTransform(TileSet.CellNeighbor matchedNeighbor, out Vector2 customOffset, out Vector2 customScale, out float customRotationDegrees)
    {
        customRotationDegrees = 0.0f;

        switch (matchedNeighbor)
        {
            case TileSet.CellNeighbor.BottomRightSide:
                customOffset = new Vector2(0, 6);
                customScale = new Vector2(-1, 1);
                break;

            case TileSet.CellNeighbor.BottomLeftSide:
                customOffset = new Vector2(0, 6);
                customScale = new Vector2(1, 1);
                break;

            case TileSet.CellNeighbor.TopLeftSide:
                customOffset = new Vector2(0, 6);
                customScale = new Vector2(-1, 1);
                break;

            case TileSet.CellNeighbor.TopRightSide:
                customOffset = new Vector2(0, 6);
                customScale = new Vector2(1, 1);
                break;

            default:
                customOffset = Vector2.Zero;
                customScale = new Vector2(1, 1);
                break;
        }
    }

    // -----------------------------------------
    // TILE PAIR RESOLVER (FORWARD & BACKWARD)
    // -----------------------------------------

    private CityTile GetTwoByOneSecondaryTile(Vector2I pivotGridPos, out TileSet.CellNeighbor matchedNeighbor)
    {
        matchedNeighbor = GetTargetNeighborEnum();

        FootprintType activeFootprint = BuildingFootprints[currentSelectedIndex];
        if (activeFootprint != FootprintType.DynamicHorizontal2x1)
            return null;

        if (!buildableTiles.ContainsKey(pivotGridPos) || buildableTiles[pivotGridPos].IsOccupied)
            return null;

        // Determine forward and backward neighbors along the active rotation axis
        TileSet.CellNeighbor forwardNeighbor = matchedNeighbor;
        TileSet.CellNeighbor backwardNeighbor = buildingRotation switch
        {
            0 => TileSet.CellNeighbor.TopLeftSide,     // Opposite of BottomRightSide
            1 => TileSet.CellNeighbor.TopRightSide,    // Opposite of BottomLeftSide
            2 => TileSet.CellNeighbor.BottomRightSide, // Opposite of TopLeftSide
            3 => TileSet.CellNeighbor.BottomLeftSide,  // Opposite of TopRightSide
            _ => TileSet.CellNeighbor.TopLeftSide
        };

        // 1. Check Primary/Forward Neighbor
        Vector2I forwardPos = GroundTileMap.GetNeighborCell(pivotGridPos, forwardNeighbor);
        if (buildableTiles.TryGetValue(forwardPos, out CityTile forwardTile) && !forwardTile.IsOccupied)
        {
            matchedNeighbor = forwardNeighbor;
            return forwardTile;
        }

        // 2. Check Backward Neighbor (if forward is blocked or occupied)
        Vector2I backwardPos = GroundTileMap.GetNeighborCell(pivotGridPos, backwardNeighbor);
        if (buildableTiles.TryGetValue(backwardPos, out CityTile backwardTile) && !backwardTile.IsOccupied)
        {
            matchedNeighbor = backwardNeighbor;
            return backwardTile;
        }

        return null;
    }

    private bool CheckCanPlaceAt(Vector2I pivotGridPos, CityTile secondTile)
    {
        if (!buildableTiles.TryGetValue(pivotGridPos, out CityTile pivotTile) || pivotTile.IsOccupied)
            return false;

        FootprintType activeFootprint = BuildingFootprints[currentSelectedIndex];

        if (activeFootprint == FootprintType.Single1x1)
            return true;

        if (activeFootprint == FootprintType.DynamicHorizontal2x1)
        {
            return secondTile != null && !secondTile.IsOccupied;
        }

        return false;
    }

    private Vector2 CalculateBuildingWorldPosition(Vector2I pivotGridPos, CityTile secondTile, TileSet.CellNeighbor matchedNeighbor)
    {
        Vector2 pivotCenter = GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(pivotGridPos));

        FootprintType activeFootprint = BuildingFootprints[currentSelectedIndex];

        if (activeFootprint == FootprintType.DynamicHorizontal2x1)
        {
            GetTwoByOneCustomTransform(matchedNeighbor, out Vector2 customOffset, out _, out _);

            Vector2 secondCenter;
            if (secondTile != null)
            {
                secondCenter = GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(secondTile.GridPosition));
            }
            else
            {
                Vector2I neighborPos = GroundTileMap.GetNeighborCell(pivotGridPos, matchedNeighbor);
                secondCenter = GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(neighborPos));
            }

            Vector2 midpoint = (pivotCenter + secondCenter) * 0.5f;
            return midpoint + BuildingVisualOffset + customOffset;
        }

        return pivotCenter + BuildingVisualOffset;
    }

    // -----------------------------------------
    // PREVIEW & LERP SYSTEM
    // -----------------------------------------
    private void RefreshPreviewNode()
    {
        if (previewNode != null)
        {
            previewNode.QueueFree();
            previewNode = null;
        }

        if (BuildingScenes.Count > currentSelectedIndex && BuildingScenes[currentSelectedIndex] != null)
        {
            previewNode = BuildingScenes[currentSelectedIndex].Instantiate<Node2D>();
            AddChild(previewNode);

            Vector2 localMousePos = GroundTileMap.ToLocal(GetGlobalMousePosition());
            Vector2I hoverGridPos = GroundTileMap.LocalToMap(localMousePos);

            CityTile secondTile = GetTwoByOneSecondaryTile(hoverGridPos, out TileSet.CellNeighbor matchedNeighbor);
            previewNode.GlobalPosition = CalculateBuildingWorldPosition(hoverGridPos, secondTile, matchedNeighbor);
            targetPreviewPosition = previewNode.GlobalPosition;

            UpdatePreviewTransform(matchedNeighbor);
        }
    }

    private void UpdatePreviewTransform(TileSet.CellNeighbor matchedNeighbor)
    {
        if (previewNode == null) return;

        FootprintType activeFootprint = BuildingFootprints[currentSelectedIndex];

        if (activeFootprint == FootprintType.Single1x1)
        {
            previewNode.RotationDegrees = 0;
            previewNode.Scale = (buildingRotation == 1) ? new Vector2(-1, 1) : new Vector2(1, 1);
        }
        else if (activeFootprint == FootprintType.DynamicHorizontal2x1)
        {
            GetTwoByOneCustomTransform(matchedNeighbor, out _, out Vector2 customScale, out float customRotation);
            previewNode.Scale = customScale;
            previewNode.RotationDegrees = customRotation;
        }
    }

    private void UpdatePreviewPositionAndValidation(float delta)
    {
        if (previewNode == null) return;

        Vector2 localMousePos = GroundTileMap.ToLocal(GetGlobalMousePosition());
        Vector2I hoverGridPos = GroundTileMap.LocalToMap(localMousePos);

        CityTile secondTile = GetTwoByOneSecondaryTile(hoverGridPos, out TileSet.CellNeighbor matchedNeighbor);
        bool isValidSpot = CheckCanPlaceAt(hoverGridPos, secondTile);

        UpdatePreviewTransform(matchedNeighbor);

        targetPreviewPosition = CalculateBuildingWorldPosition(hoverGridPos, secondTile, matchedNeighbor);

        previewNode.GlobalPosition = previewNode.GlobalPosition.Lerp(targetPreviewPosition, delta * PreviewLerpSpeed);
        previewNode.ZIndex = CalculateDepth(hoverGridPos, secondTile);
        previewNode.Modulate = isValidSpot ? ValidColor : InvalidColor;
    }

    // -----------------------------------------
    // PLACEMENT LOGIC
    // -----------------------------------------
    private void TryPlaceSingleTile(Vector2I clickGridPos)
    {
        CityTile clickedTile = buildableTiles[clickGridPos];
        if (!clickedTile.IsOccupied)
        {
            CommitBuilding(new List<CityTile> { clickedTile }, clickGridPos, null, TileSet.CellNeighbor.BottomRightSide);
        }
    }

    private void TryPlaceTwoTile(Vector2I pivotGridPos)
    {
        CityTile pivotTile = buildableTiles[pivotGridPos];
        CityTile secondTile = GetTwoByOneSecondaryTile(pivotGridPos, out TileSet.CellNeighbor matchedNeighbor);

        if (!CheckCanPlaceAt(pivotGridPos, secondTile))
        {
            GD.Print("Cannot place: Target tile is occupied or out of bounds!");
            return;
        }

        CommitBuilding(new List<CityTile> { pivotTile, secondTile }, pivotGridPos, secondTile, matchedNeighbor);
    }

    // -----------------------------------------
    // COMMIT & SPAWN
    // -----------------------------------------
    private void CommitBuilding(List<CityTile> tilesToOccupy, Vector2I pivotGridPos, CityTile secondTile, TileSet.CellNeighbor matchedNeighbor)
    {
        foreach (CityTile tile in tilesToOccupy)
        {
            tile.IsOccupied = true;
        }

        Node2D newBuilding = BuildingScenes[currentSelectedIndex].Instantiate<Node2D>();
        AddChild(newBuilding);

        FootprintType activeFootprint = BuildingFootprints[currentSelectedIndex];

        if (activeFootprint == FootprintType.Single1x1)
        {
            newBuilding.RotationDegrees = 0;
            newBuilding.Scale = (buildingRotation == 1) ? new Vector2(-1, 1) : new Vector2(1, 1);
        }
        else if (activeFootprint == FootprintType.DynamicHorizontal2x1)
        {
            GetTwoByOneCustomTransform(matchedNeighbor, out _, out Vector2 customScale, out float customRotation);
            newBuilding.Scale = customScale;
            newBuilding.RotationDegrees = customRotation;
        }

        newBuilding.GlobalPosition = CalculateBuildingWorldPosition(pivotGridPos, secondTile, matchedNeighbor);
        newBuilding.ZIndex = CalculateDepth(pivotGridPos, secondTile);

        foreach (CityTile tile in tilesToOccupy)
        {
            tile.BuildingNode = newBuilding;
        }

        GD.Print($"Built {newBuilding.Name} occupying {tilesToOccupy.Count} tile(s).");
    }

    // -----------------------------------------
    // DEPTH SORTING HELPER
    // -----------------------------------------
    private int CalculateDepth(Vector2I pivotGridPos, CityTile secondTile)
    {
        // Start with the pivot tile's Y position
        float maxDepthY = GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(pivotGridPos)).Y;

        // If there's a second tile (for 2x1s), take the one that is lowest on the screen (Highest Y)
        if (secondTile != null)
        {
            float secondTileY = GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(secondTile.GridPosition)).Y;
            maxDepthY = Mathf.Max(maxDepthY, secondTileY);
        }

        return Mathf.RoundToInt(maxDepthY) + 10;
    }
}