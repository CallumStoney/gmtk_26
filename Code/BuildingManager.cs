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

    public enum BuildingSizeCategory
    {
        House1x1,
        Tall1x1,
        Rectangle2x1,
        Apartment2x2
    }

    // -----------------------------------------
    // INSPECTOR VARIABLES
    // -----------------------------------------
    [Export] public TileMapLayer GroundTileMap;

    [Export]
    public Godot.Collections.Array<PackedScene> BuildingScenes = new Godot.Collections.Array<PackedScene>();

    [Export]
    public Godot.Collections.Array<FootprintType> BuildingFootprints = new Godot.Collections.Array<FootprintType>();

    // Quantity available for each building type (matches BuildingScenes array index)
    [Export]
    public Godot.Collections.Array<int> BuildingCounts = new Godot.Collections.Array<int>();

    [Export] public Vector2 BuildingVisualOffset = new Vector2(0, -16);
    [Export] public float PreviewLerpSpeed = 25.0f;

    [Export] public Color ValidColor = new Color(0.2f, 1.0f, 0.2f, 0.6f);
    [Export] public Color InvalidColor = new Color(1.0f, 0.2f, 0.2f, 0.6f);

    // --- PARTICLE INSPECTOR SETTINGS ---
    [ExportGroup("Particle Settings")]
    [Export] public Color ConstructionParticleColor = new Color(0.8f, 0.75f, 0.65f, 1.0f);
    [Export] public Color DeletionParticleColor = new Color(0.5f, 0.5f, 0.5f, 1.0f);
    [Export] public int DefaultParticlesPerLayer = 16;

    // -----------------------------------------
    // TILE IDENTIFIERS
    // -----------------------------------------
    private readonly int TargetSourceId = 1;
    private readonly Vector2I TargetAtlasCoord = new Vector2I(0, 0);

    // -----------------------------------------
    // STATE TRACKING
    // -----------------------------------------
    private int currentSelectedIndex = -1; // -1 means no building selected

    // Public property to read or change the selected building index from external scripts
    public int CurrentSelectedIndex
    {
        get => currentSelectedIndex;
        set => SelectBuilding(value);
    }

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
        public int BuildingTypeIndex; // Tracks which building index occupies this tile for refunds

        public CityTile(Vector2I gridPosition)
        {
            GridPosition = gridPosition;
            IsOccupied = false;
            Neighbors = new List<CityTile>();
            BuildingNode = null;
            BuildingTypeIndex = -1;
        }
    }

    private Dictionary<Vector2I, CityTile> buildableTiles = new Dictionary<Vector2I, CityTile>();

    // -----------------------------------------
    // INITIALIZATION & PROCESS
    // -----------------------------------------
    public override void _Ready()
    {
        if (GroundTileMap == null ||
            BuildingScenes.Count != BuildingFootprints.Count ||
            BuildingScenes.Count != BuildingCounts.Count)
        {
            GD.PrintErr("CRITICAL: Check GroundTileMap reference and array sizes (Scenes, Footprints, Counts) in Inspector!");
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
    // HELPER: INVENTORY / ACCESS CHECK
    // -----------------------------------------
    public bool HasAvailableBuilding(int index)
    {
        if (index < 0 || index >= BuildingCounts.Count) return false;
        return BuildingCounts[index] > 0;
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
            else if (keyEvent.Keycode == Key.Escape)
            {
                SelectBuilding(-1); // Deselect on Escape
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
                    if (clickedTile.BuildingNode is UpgradableBuilding upgradable)
                    {
                        int requiredIndex = (currentSelectedIndex >= 0) ? currentSelectedIndex : clickedTile.BuildingTypeIndex;

                        if (HasAvailableBuilding(requiredIndex))
                        {
                            if (upgradable.TryUpgrade())
                            {
                                BuildingCounts[requiredIndex]--;
                                GD.Print($"Building upgraded! Remaining stock for index {requiredIndex}: {BuildingCounts[requiredIndex]}");

                                // Trigger placement/upgrade particles
                                SpawnBuildingParticles(clickedTile.BuildingNode, isDeleting: false);

                                if (requiredIndex == currentSelectedIndex && !HasAvailableBuilding(currentSelectedIndex))
                                {
                                    SelectBuilding(-1);
                                }
                            }
                            else
                            {
                                GD.Print("Building is already at max level.");
                            }
                        }
                        else
                        {
                            GD.Print($"Cannot upgrade: No remaining stock in inventory for index {requiredIndex}!");
                        }
                    }
                    return;
                }

                if (currentSelectedIndex < 0) return;

                if (!HasAvailableBuilding(currentSelectedIndex))
                {
                    GD.Print($"Cannot place: No remaining stock for building index {currentSelectedIndex}!");
                    return;
                }

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
                        if (upgradable.TryDowngrade())
                        {
                            int refundIndex = clickedTile.BuildingTypeIndex;
                            if (refundIndex >= 0 && refundIndex < BuildingCounts.Count)
                            {
                                BuildingCounts[refundIndex]++;
                                GD.Print($"Building downgraded! Refunded 1 stock for type {refundIndex}. New stock: {BuildingCounts[refundIndex]}");
                            }

                            // Trigger particles on downgrade
                            SpawnBuildingParticles(clickedTile.BuildingNode, isDeleting: true);
                        }
                        else
                        {
                            DeleteBuilding(clickedTile.BuildingNode);
                        }
                    }
                    else
                    {
                        DeleteBuilding(clickedTile.BuildingNode);
                    }
                }
                else
                {
                    SelectBuilding(-1);
                }
            }
        }
    }

    public void SelectBuilding(int index)
    {
        if (index >= 0 && index < BuildingScenes.Count && index < BuildingFootprints.Count && BuildingScenes[index] != null)
        {
            currentSelectedIndex = index;
            buildingRotation = 0;
        }
        else
        {
            currentSelectedIndex = -1;
        }

        RefreshPreviewNode();
    }

    private void RotateBuilding()
    {
        if (currentSelectedIndex < 0) return;

        FootprintType activeFootprint = BuildingFootprints[currentSelectedIndex];

        if (activeFootprint == FootprintType.Single1x1)
        {
            buildingRotation = (buildingRotation + 1) % 2;
        }
        else
        {
            buildingRotation = (buildingRotation + 1) % 4;
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

        if (currentSelectedIndex < 0) return null;

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
            case 0:
                posTop = pivotGridPos;
                posRight = GroundTileMap.GetNeighborCell(posTop, TileSet.CellNeighbor.BottomRightSide);
                posLeft = GroundTileMap.GetNeighborCell(posTop, TileSet.CellNeighbor.BottomLeftSide);
                posBottom = GroundTileMap.GetNeighborCell(posRight, TileSet.CellNeighbor.BottomLeftSide);
                break;
            case 1:
                posRight = pivotGridPos;
                posBottom = GroundTileMap.GetNeighborCell(posRight, TileSet.CellNeighbor.BottomLeftSide);
                posTop = GroundTileMap.GetNeighborCell(posRight, TileSet.CellNeighbor.TopLeftSide);
                posLeft = GroundTileMap.GetNeighborCell(posBottom, TileSet.CellNeighbor.TopLeftSide);
                break;
            case 2:
                posBottom = pivotGridPos;
                posLeft = GroundTileMap.GetNeighborCell(posBottom, TileSet.CellNeighbor.TopLeftSide);
                posRight = GroundTileMap.GetNeighborCell(posBottom, TileSet.CellNeighbor.TopRightSide);
                posTop = GroundTileMap.GetNeighborCell(posLeft, TileSet.CellNeighbor.TopRightSide);
                break;
            case 3:
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
            return new List<CityTile> { tTop, tRight, tLeft, tBottom };
        }

        return null;
    }

    private List<CityTile> GetSquare2x2Tiles(Vector2I pivotGridPos)
    {
        for (int i = 0; i < 4; i++)
        {
            List<CityTile> tiles = TryGet2x2Config(pivotGridPos, i);
            if (tiles != null) return tiles;
        }
        return null;
    }

    private bool CheckCanPlaceAt(Vector2I pivotGridPos, CityTile secondTile)
    {
        if (currentSelectedIndex < 0) return false;

        if (!HasAvailableBuilding(currentSelectedIndex)) return false;

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
        if (currentSelectedIndex < 0) return 0;

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
            else
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

        if (currentSelectedIndex < 0) return pivotCenter;

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
                posTop = tiles[0].GridPosition;
                posBottom = tiles[3].GridPosition;
            }
            else
            {
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

        if (currentSelectedIndex < 0) return;

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
        if (previewNode == null || currentSelectedIndex < 0) return;

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
        if (previewNode == null || currentSelectedIndex < 0) return;

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
        if (!clickedTile.IsOccupied && HasAvailableBuilding(currentSelectedIndex))
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
            GD.Print("Cannot place: Target tile is occupied, out of bounds, or out of stock!");
            return;
        }

        CommitBuilding(new List<CityTile> { pivotTile, secondTile }, pivotGridPos, secondTile, matchedNeighbor);
    }

    private void TryPlaceFourTile(Vector2I pivotGridPos)
    {
        List<CityTile> tiles = GetSquare2x2Tiles(pivotGridPos);

        if (tiles == null || !HasAvailableBuilding(currentSelectedIndex))
        {
            GD.Print("Cannot place: 2x2 area is blocked, out of bounds, or out of stock!");
            return;
        }

        CommitBuilding(tiles, pivotGridPos, null, TileSet.CellNeighbor.BottomRightSide);
    }

    // -----------------------------------------
    // COMMIT & SPAWN
    // -----------------------------------------
    private void CommitBuilding(List<CityTile> tilesToOccupy, Vector2I pivotGridPos, CityTile secondTile, TileSet.CellNeighbor matchedNeighbor)
    {
        BuildingCounts[currentSelectedIndex]--;

        Vector2 finalWorldPos = CalculateBuildingWorldPosition(pivotGridPos, secondTile, matchedNeighbor);
        int finalDepth = CalculateDepth(pivotGridPos, secondTile);

        foreach (CityTile tile in tilesToOccupy)
        {
            tile.IsOccupied = true;
            tile.BuildingTypeIndex = currentSelectedIndex;
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

        newBuilding.GlobalPosition = finalWorldPos;
        newBuilding.ZIndex = finalDepth;

        foreach (CityTile tile in tilesToOccupy)
        {
            tile.BuildingNode = newBuilding;
        }

        // Trigger placement particle burst
        SpawnBuildingParticles(newBuilding, isDeleting: false);

        GD.Print($"Built {newBuilding.Name}. Remaining stock for type {currentSelectedIndex}: {BuildingCounts[currentSelectedIndex]}");

        if (!HasAvailableBuilding(currentSelectedIndex))
        {
            SelectBuilding(-1);
        }
    }

    // -----------------------------------------
    // DELETION LOGIC
    // -----------------------------------------
    private void DeleteBuilding(Node2D buildingToRemove)
    {
        if (buildingToRemove == null) return;

        int freedTiles = 0;
        int refundedIndex = -1;

        foreach (var kvp in buildableTiles)
        {
            if (kvp.Value.BuildingNode == buildingToRemove)
            {
                refundedIndex = kvp.Value.BuildingTypeIndex;
                kvp.Value.IsOccupied = false;
                kvp.Value.BuildingNode = null;
                kvp.Value.BuildingTypeIndex = -1;
                freedTiles++;
            }
        }

        if (refundedIndex >= 0 && refundedIndex < BuildingCounts.Count)
        {
            BuildingCounts[refundedIndex]++;
            GD.Print($"Refunded 1 stock for type {refundedIndex}. New stock: {BuildingCounts[refundedIndex]}");
        }

        // Trigger deletion particles before freeing the node
        SpawnBuildingParticles(buildingToRemove, isDeleting: true);

        buildingToRemove.QueueFree();

        GD.Print($"Deleted building, cleanly freed {freedTiles} tile(s).");
    }

    // -----------------------------------------
    // PARTICLE SYSTEM LOGIC
    // -----------------------------------------
    private void SpawnBuildingParticles(Node2D building, bool isDeleting)
    {
        if (building == null) return;

        // Spawn particles behind and in front of the building
        CreateParticleLayer(building, isBehind: true, isDeleting: isDeleting);
        CreateParticleLayer(building, isBehind: false, isDeleting: isDeleting);
    }

    private void CreateParticleLayer(Node2D building, bool isBehind, bool isDeleting)
    {
        CpuParticles2D particles = new CpuParticles2D();
        AddChild(particles);

        particles.GlobalPosition = building.GlobalPosition;
        particles.ZIndex = isBehind ? (building.ZIndex - 1) : (building.ZIndex + 1);

        Color particleColor = isDeleting ? DeletionParticleColor : ConstructionParticleColor;

        var img = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        img.Fill(particleColor);
        particles.Texture = ImageTexture.CreateFromImage(img);
        particles.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

        particles.Emitting = false;
        particles.OneShot = true;
        particles.Explosiveness = isDeleting ? 0.95f : 0.85f;
        particles.Amount = DefaultParticlesPerLayer;

        if (isDeleting)
        {
            particles.Lifetime = (float)GD.RandRange(0.20f, 0.25f);
            particles.InitialVelocityMin = (float)GD.RandRange(18f, 24f);
            particles.InitialVelocityMax = (float)GD.RandRange(30f, 40f);
        }
        else
        {
            particles.Lifetime = (float)GD.RandRange(0.40f, 0.48f);
            particles.InitialVelocityMin = (float)GD.RandRange(12f, 16f);
            particles.InitialVelocityMax = (float)GD.RandRange(22f, 28f);
        }

        particles.EmissionShape = CpuParticles2D.EmissionShapeEnum.Points;
        particles.Direction = new Vector2((float)GD.RandRange(-1f, 1f), (float)GD.RandRange(-1f, 1f)).Normalized();
        particles.Spread = 180f;
        particles.Gravity = new Vector2(0, 10f);

        // Clean up particle node automatically when emission finishes
        particles.Finished += () => particles.QueueFree();

        particles.Emitting = true;
    }
}