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
        DynamicHorizontal2x1,
        Square2x2
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
    public override void _UnhandledInput(InputEvent @event)
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
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            Vector2 localMousePos = GroundTileMap.ToLocal(GetGlobalMousePosition());
            Vector2I clickGridPos = GroundTileMap.LocalToMap(localMousePos);

            if (!buildableTiles.ContainsKey(clickGridPos)) return;

            CityTile clickedTile = buildableTiles[clickGridPos];

            // --- LEFT CLICK: PLACE OR UPGRADE ---
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                if (clickedTile.IsOccupied && clickedTile.BuildingNode != null)
                {
                    // Check if the node is an UpgradableBuilding
                    if (clickedTile.BuildingNode is UpgradableBuilding upgradable)
                    {
                        if (upgradable.TryUpgrade())
                            GD.Print("Building upgraded!");
                        else
                            GD.Print("Building is already at max level.");
                    }
                    return; // Stop here so we don't try to place a new building on top
                }

                // If empty, proceed with placement
                FootprintType activeFootprint = BuildingFootprints[currentSelectedIndex];

                if (activeFootprint == FootprintType.Single1x1) TryPlaceSingleTile(clickGridPos);
                else if (activeFootprint == FootprintType.DynamicHorizontal2x1) TryPlaceTwoTile(clickGridPos);
                else if (activeFootprint == FootprintType.Square2x2) TryPlaceFourTile(clickGridPos);
            }

            // --- RIGHT CLICK: DOWNGRADE OR DELETE ---
            else if (mouseEvent.ButtonIndex == MouseButton.Right)
            {
                if (clickedTile.IsOccupied && clickedTile.BuildingNode != null)
                {
                    if (clickedTile.BuildingNode is UpgradableBuilding upgradable)
                    {
                        // TryDowngrade returns false if it is at level 0
                        if (!upgradable.TryDowngrade())
                        {
                            DeleteBuilding(clickedTile.BuildingNode);
                        }
                        else
                        {
                            GD.Print("Building downgraded!");
                        }
                    }
                    else
                    {
                        // If it's a standard Node2D (not upgradable), just delete it immediately
                        DeleteBuilding(clickedTile.BuildingNode);
                    }
                }
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
            buildingRotation = (buildingRotation + 1) % 2; // Toggles 0 and 1
        }
        else
        {
            buildingRotation = (buildingRotation + 1) % 4; // Cycles 0..3 for 2x1 and 2x2
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
            0 => TileSet.CellNeighbor.BottomRightSide,
            1 => TileSet.CellNeighbor.BottomLeftSide,
            2 => TileSet.CellNeighbor.TopLeftSide,
            3 => TileSet.CellNeighbor.TopRightSide,
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

    private void GetTwoByTwoCustomTransform(int rotationState, out Vector2 customScale, out float customRotationDegrees)
    {
        customRotationDegrees = 0.0f;
        // Strictly visual flips, no positional offset jumping
        switch (rotationState)
        {
            case 0: customScale = new Vector2(1, 1); break;
            case 1: customScale = new Vector2(-1, 1); break;
            case 2: customScale = new Vector2(1, 1); break;
            case 3: customScale = new Vector2(-1, 1); break;
            default: customScale = new Vector2(1, 1); break;
        }
    }

    // -----------------------------------------
    // TILE RESOLVERS (2x1 and 2x2)
    // -----------------------------------------
    private CityTile GetTwoByOneSecondaryTile(Vector2I pivotGridPos, out TileSet.CellNeighbor matchedNeighbor)
    {
        matchedNeighbor = GetTargetNeighborEnum();

        FootprintType activeFootprint = BuildingFootprints[currentSelectedIndex];
        if (activeFootprint != FootprintType.DynamicHorizontal2x1)
            return null;

        if (!buildableTiles.ContainsKey(pivotGridPos) || buildableTiles[pivotGridPos].IsOccupied)
            return null;

        TileSet.CellNeighbor forwardNeighbor = matchedNeighbor;
        TileSet.CellNeighbor backwardNeighbor = buildingRotation switch
        {
            0 => TileSet.CellNeighbor.TopLeftSide,
            1 => TileSet.CellNeighbor.TopRightSide,
            2 => TileSet.CellNeighbor.BottomRightSide,
            3 => TileSet.CellNeighbor.BottomLeftSide,
            _ => TileSet.CellNeighbor.TopLeftSide
        };

        Vector2I forwardPos = GroundTileMap.GetNeighborCell(pivotGridPos, forwardNeighbor);
        if (buildableTiles.TryGetValue(forwardPos, out CityTile forwardTile) && !forwardTile.IsOccupied)
        {
            matchedNeighbor = forwardNeighbor;
            return forwardTile;
        }

        Vector2I backwardPos = GroundTileMap.GetNeighborCell(pivotGridPos, backwardNeighbor);
        if (buildableTiles.TryGetValue(backwardPos, out CityTile backwardTile) && !backwardTile.IsOccupied)
        {
            matchedNeighbor = backwardNeighbor;
            return backwardTile;
        }

        return null;
    }

    private List<CityTile> TryGet2x2Config(Vector2I pivotGridPos, int configIndex)
    {
        Vector2I posTop = Vector2I.Zero, posRight = Vector2I.Zero, posLeft = Vector2I.Zero, posBottom = Vector2I.Zero;

        switch (configIndex)
        {
            case 0: // Config 0: Mouse is the Top tile
                posTop = pivotGridPos;
                posRight = GroundTileMap.GetNeighborCell(posTop, TileSet.CellNeighbor.BottomRightSide);
                posLeft = GroundTileMap.GetNeighborCell(posTop, TileSet.CellNeighbor.BottomLeftSide);
                posBottom = GroundTileMap.GetNeighborCell(posRight, TileSet.CellNeighbor.BottomLeftSide);
                break;
            case 1: // Config 1: Mouse is the Right tile
                posRight = pivotGridPos;
                posBottom = GroundTileMap.GetNeighborCell(posRight, TileSet.CellNeighbor.BottomLeftSide);
                posTop = GroundTileMap.GetNeighborCell(posRight, TileSet.CellNeighbor.TopLeftSide);
                posLeft = GroundTileMap.GetNeighborCell(posBottom, TileSet.CellNeighbor.TopLeftSide);
                break;
            case 2: // Config 2: Mouse is the Bottom tile
                posBottom = pivotGridPos;
                posLeft = GroundTileMap.GetNeighborCell(posBottom, TileSet.CellNeighbor.TopLeftSide);
                posRight = GroundTileMap.GetNeighborCell(posBottom, TileSet.CellNeighbor.TopRightSide);
                posTop = GroundTileMap.GetNeighborCell(posLeft, TileSet.CellNeighbor.TopRightSide);
                break;
            case 3: // Config 3: Mouse is the Left tile
                posLeft = pivotGridPos;
                posTop = GroundTileMap.GetNeighborCell(posLeft, TileSet.CellNeighbor.TopRightSide);
                posBottom = GroundTileMap.GetNeighborCell(posLeft, TileSet.CellNeighbor.BottomRightSide);
                posRight = GroundTileMap.GetNeighborCell(posTop, TileSet.CellNeighbor.BottomRightSide);
                break;
        }

        if (buildableTiles.TryGetValue(posTop, out CityTile tTop) && !tTop.IsOccupied &&
            buildableTiles.TryGetValue(posRight, out CityTile tRight) && !tRight.IsOccupied &&
            buildableTiles.TryGetValue(posLeft, out CityTile tLeft) && !tLeft.IsOccupied &&
            buildableTiles.TryGetValue(posBottom, out CityTile tBottom) && !tBottom.IsOccupied)
        {
            // ALWAYS returns in standard orientation to preserve geometric calculations
            return new List<CityTile> { tTop, tRight, tLeft, tBottom };
        }

        return null;
    }

    private List<CityTile> GetSquare2x2Tiles(Vector2I pivotGridPos)
    {
        // Loop through all 4 grid arrangements to find an empty valid 2x2 spot around the mouse.
        // Independent from visual `buildingRotation`.
        for (int i = 0; i < 4; i++)
        {
            List<CityTile> tiles = TryGet2x2Config(pivotGridPos, i);
            if (tiles != null) return tiles;
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
            return secondTile != null && !secondTile.IsOccupied;

        if (activeFootprint == FootprintType.Square2x2)
        {
            List<CityTile> tiles = GetSquare2x2Tiles(pivotGridPos);
            return tiles != null;
        }

        return false;
    }

    // -----------------------------------------
    // DEPTH & POSITION CALCULATORS
    // -----------------------------------------
    private int CalculateDepth(Vector2I pivotGridPos, CityTile secondTile)
    {
        float maxDepthY = GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(pivotGridPos)).Y;
        FootprintType activeFootprint = BuildingFootprints[currentSelectedIndex];

        if (activeFootprint == FootprintType.DynamicHorizontal2x1 && secondTile != null)
        {
            float secondTileY = GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(secondTile.GridPosition)).Y;
            maxDepthY = Mathf.Max(maxDepthY, secondTileY);
        }
        else if (activeFootprint == FootprintType.Square2x2)
        {
            List<CityTile> tiles = GetSquare2x2Tiles(pivotGridPos);
            if (tiles != null)
            {
                foreach (CityTile t in tiles)
                {
                    float tileY = GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(t.GridPosition)).Y;
                    maxDepthY = Mathf.Max(maxDepthY, tileY);
                }
            }
            else // Failsafe if hovering over an invalid spot so the depth doesn't break
            {
                Vector2I posTop = pivotGridPos;
                Vector2I posRight = GroundTileMap.GetNeighborCell(posTop, TileSet.CellNeighbor.BottomRightSide);
                Vector2I posBottom = GroundTileMap.GetNeighborCell(posRight, TileSet.CellNeighbor.BottomLeftSide);
                maxDepthY = Mathf.Max(maxDepthY, GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(posBottom)).Y);
            }
        }

        return Mathf.RoundToInt(maxDepthY) + 10;
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
        else if (activeFootprint == FootprintType.Square2x2)
        {
            List<CityTile> tiles = GetSquare2x2Tiles(pivotGridPos);
            Vector2I posTop, posBottom;

            if (tiles != null && tiles.Count == 4)
            {
                // Tiles[0] is guaranteed to be Top, Tiles[3] is Bottom. Averages to exact center.
                posTop = tiles[0].GridPosition;
                posBottom = tiles[3].GridPosition;
            }
            else
            {
                // FAILSAFE: Maintain the smooth 2x2 offset even if hovered spot is temporarily invalid (red)
                posTop = pivotGridPos;
                Vector2I posRight = GroundTileMap.GetNeighborCell(posTop, TileSet.CellNeighbor.BottomRightSide);
                posBottom = GroundTileMap.GetNeighborCell(posRight, TileSet.CellNeighbor.BottomLeftSide);
            }

            Vector2 topCenter = GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(posTop));
            Vector2 bottomCenter = GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(posBottom));

            return ((topCenter + bottomCenter) * 0.5f) + BuildingVisualOffset;
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
        else if (activeFootprint == FootprintType.Square2x2)
        {
            GetTwoByTwoCustomTransform(buildingRotation, out Vector2 customScale, out float customRotation);
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

    private void TryPlaceFourTile(Vector2I pivotGridPos)
    {
        List<CityTile> tiles = GetSquare2x2Tiles(pivotGridPos);

        if (tiles == null)
        {
            GD.Print("Cannot place: 2x2 area is blocked or out of bounds!");
            return;
        }

        CommitBuilding(tiles, pivotGridPos, null, TileSet.CellNeighbor.BottomRightSide);
    }

    // -----------------------------------------
    // COMMIT & SPAWN
    // -----------------------------------------
    private void CommitBuilding(List<CityTile> tilesToOccupy, Vector2I pivotGridPos, CityTile secondTile, TileSet.CellNeighbor matchedNeighbor)
    {
        // 1. Calculate the final position and depth BEFORE setting IsOccupied = true
        // If we set IsOccupied first, CalculateBuildingWorldPosition will assume the spot 
        // is invalid and forcefully fall back to a 1x1 position, causing the jump!
        Vector2 finalWorldPos = CalculateBuildingWorldPosition(pivotGridPos, secondTile, matchedNeighbor);
        int finalDepth = CalculateDepth(pivotGridPos, secondTile);

        // 2. Now it's safe to mark the tiles as occupied
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
        else if (activeFootprint == FootprintType.Square2x2)
        {
            GetTwoByTwoCustomTransform(buildingRotation, out Vector2 customScale, out float customRotation);
            newBuilding.Scale = customScale;
            newBuilding.RotationDegrees = customRotation;
        }

        // 3. Apply the properly cached transform and depth
        newBuilding.GlobalPosition = finalWorldPos;
        newBuilding.ZIndex = finalDepth;

        foreach (CityTile tile in tilesToOccupy)
        {
            tile.BuildingNode = newBuilding;
        }

        GD.Print($"Built {newBuilding.Name} occupying {tilesToOccupy.Count} tile(s).");
    }

    // -----------------------------------------
    // DELETION LOGIC
    // -----------------------------------------
    private void DeleteBuilding(Node2D buildingToRemove)
    {
        if (buildingToRemove == null) return;

        int freedTiles = 0;

        // Find all tiles that reference this exact building and reset them
        foreach (var kvp in buildableTiles)
        {
            if (kvp.Value.BuildingNode == buildingToRemove)
            {
                kvp.Value.IsOccupied = false;
                kvp.Value.BuildingNode = null;
                freedTiles++;
            }
        }

        // Destroy the actual building node in the scene
        buildingToRemove.QueueFree();

        GD.Print($"Deleted building, cleanly freed {freedTiles} tile(s).");
    }
}