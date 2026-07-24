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

	// Teammate's inventory system: Quantity available for each building type
	[Export]
	public Godot.Collections.Array<int> BuildingCounts = new Godot.Collections.Array<int>();

	[Export] public Vector2 BuildingVisualOffset = new Vector2(0, -16);
	[Export] public float PreviewLerpSpeed = 25.0f;

	[Export] public Color ValidColor = new Color(0.2f, 1.0f, 0.2f, 0.6f);
	[Export] public Color InvalidColor = new Color(1.0f, 0.2f, 0.2f, 0.6f);

	// Teammate's exported particle colors
	[Export] public Color ConstructionParticleColor = new Color(0.85f, 0.8f, 0.7f, 0.9f);
	[Export] public Color DeletionParticleColor = new Color(0.9f, 0.25f, 0.25f, 0.9f);
	[Export] public int DefaultParticlesPerLayer = 16;

	// -----------------------------------------
	// TILE IDENTIFIERS
	// -----------------------------------------
	private readonly int TargetSourceId = 1;
	private readonly Vector2I TargetAtlasCoord = new Vector2I(0, 0);

	// -----------------------------------------
	// STATE TRACKING & UI PROPERTIES (FIXED!)
	// -----------------------------------------
	private int currentSelectedIndex = 0;
	
	// Public property required by Teammate's UI scripts (BuyButton, ShopUI, etc.)
	public int CurrentSelectedIndex
	{
		get => currentSelectedIndex;
		set => SelectBuilding(value);
	}

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
	// UI & INVENTORY BRIDGE METHODS (FIXED!)
	// -----------------------------------------
	// Required by BuildingInInventory.cs and InventoryUI.cs
	public bool HasAvailableBuilding(int index)
	{
		if (index >= 0 && index < BuildingCounts.Count)
		{
			return BuildingCounts[index] > 0;
		}
		return false;
	}

	public bool HasAvailableBuilding()
	{
		return HasAvailableBuilding(currentSelectedIndex);
	}

	public int GetBuildingCount(int index)
	{
		if (index >= 0 && index < BuildingCounts.Count)
		{
			return BuildingCounts[index];
		}
		return 0;
	}

	public void AddBuildingCount(int index, int amount = 1)
	{
		if (index >= 0 && index < BuildingCounts.Count)
		{
			BuildingCounts[index] += amount;
		}
	}

	// Made public so SelectionButton.cs and ShopUI.cs can call it directly
	public void SelectBuilding(int index)
	{
		if (index >= 0 && index < BuildingScenes.Count && index < BuildingFootprints.Count && BuildingScenes[index] != null)
		{
			currentSelectedIndex = index;
			buildingRotation = 0;
			RefreshPreviewNode();
		}
	}

	// -----------------------------------------
	// INPUT & ROUTING
	// -----------------------------------------
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			int pressedNumber = (int)keyEvent.Keycode - (int)Key.Key0;

			if (pressedNumber >= 1 && pressedNumber <= 9)
			{
				SelectBuilding(pressedNumber - 1);
			}
			else if (keyEvent.Keycode == Key.R)
			{
				RotateBuilding();
			}
		}

		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
		{
			Vector2 localMousePos = GroundTileMap.ToLocal(GetGlobalMousePosition());
			Vector2I clickGridPos = GroundTileMap.LocalToMap(localMousePos);

			if (!buildableTiles.ContainsKey(clickGridPos)) return;

			CityTile clickedTile = buildableTiles[clickGridPos];
			if (mouseEvent.ButtonIndex == MouseButton.Left)
			{
				if (clickedTile.IsOccupied && clickedTile.BuildingNode != null)
				{
					if (clickedTile.BuildingNode is UpgradableBuilding upgradable)
					{
						if (upgradable.TryUpgrade())
						{
							List<CityTile> tiles = GetTilesForBuilding(upgradable);
							SpawnFootprintParticles(tiles, upgradable, new Color(1.0f, 0.85f, 0.3f, 0.9f), false);
							GD.Print("Building upgraded!");
						}
						else
						{
							GD.Print("Building is already at max level.");
						}
					}
					return;
				}

				// Inventory Check: Prevent building if out of stock
				if (!HasAvailableBuilding())
				{
					GD.Print("No more buildings of this type available!");
					return;
				}

				FootprintType activeFootprint = BuildingFootprints[currentSelectedIndex];
				if (activeFootprint == FootprintType.Single1x1) TryPlaceSingleTile(clickGridPos);
				else if (activeFootprint == FootprintType.DynamicHorizontal2x1) TryPlaceTwoTile(clickGridPos);
				else if (activeFootprint == FootprintType.Square2x2) TryPlaceFourTile(clickGridPos);
			}
			else if (mouseEvent.ButtonIndex == MouseButton.Right)
			{
				if (clickedTile.IsOccupied && clickedTile.BuildingNode != null)
				{
					if (clickedTile.BuildingNode is UpgradableBuilding upgradable)
					{
						if (!upgradable.TryDowngrade())
						{
							DeleteBuilding(clickedTile.BuildingNode);
						}
						else
						{
							List<CityTile> tiles = GetTilesForBuilding(upgradable);
							SpawnFootprintParticles(tiles, upgradable, DeletionParticleColor, false);
							GD.Print("Building downgraded!");
						}
					}
					else
					{
						DeleteBuilding(clickedTile.BuildingNode);
					}
				}
			}
		}
	}

	private void RotateBuilding()
	{
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
		Vector2 finalWorldPos = CalculateBuildingWorldPosition(pivotGridPos, secondTile, matchedNeighbor);
		int finalDepth = CalculateDepth(pivotGridPos, secondTile);

		foreach (CityTile tile in tilesToOccupy)
		{
			tile.IsOccupied = true;
		}

		// Decrement inventory when built!
		if (BuildingCounts.Count > currentSelectedIndex)
		{
			BuildingCounts[currentSelectedIndex]--;
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

		newBuilding.ZIndex = finalDepth;

		Vector2 targetPos = finalWorldPos;
		Vector2 originalScale = newBuilding.Scale;

		BuildingSizeCategory cat = GetBuildingCategory(newBuilding, tilesToOccupy);
		float dropY = -4f;
		float dropTime = 0.06f;
		Vector2 landScale = new Vector2(originalScale.X * 1.03f, originalScale.Y * 0.97f);
		float reboundTime = 0.06f;

		switch (cat)
		{
			case BuildingSizeCategory.House1x1:
				dropY = -4f; dropTime = 0.06f;
				landScale = new Vector2(originalScale.X * 1.03f, originalScale.Y * 0.97f);
				reboundTime = 0.06f;
				break;
			case BuildingSizeCategory.Tall1x1:
				dropY = -6f; dropTime = 0.07f;
				landScale = new Vector2(originalScale.X * 1.04f, originalScale.Y * 0.96f);
				reboundTime = 0.07f;
				break;
			case BuildingSizeCategory.Rectangle2x1:
				dropY = -5f; dropTime = 0.07f;
				landScale = new Vector2(originalScale.X * 1.03f, originalScale.Y * 0.97f);
				reboundTime = 0.07f;
				break;
			case BuildingSizeCategory.Apartment2x2:
				dropY = -8f; dropTime = 0.09f;
				landScale = new Vector2(originalScale.X * 1.05f, originalScale.Y * 0.95f);
				reboundTime = 0.08f;
				break;
		}

		newBuilding.GlobalPosition = targetPos + new Vector2(0, dropY);
		newBuilding.Scale = new Vector2(originalScale.X * 0.98f, originalScale.Y * 1.02f);

		Tween tween = newBuilding.CreateTween();
		tween.TweenProperty(newBuilding, "global_position", targetPos, dropTime)
			 .SetTrans(Tween.TransitionType.Quad)
			 .SetEase(Tween.EaseType.In);
		
		tween.TweenCallback(Callable.From(() => SpawnFootprintParticles(tilesToOccupy, newBuilding, ConstructionParticleColor, false)));
		
		tween.TweenProperty(newBuilding, "scale", landScale, 0.04f);
		tween.TweenProperty(newBuilding, "scale", originalScale, reboundTime)
			 .SetTrans(Tween.TransitionType.Sine)
			 .SetEase(Tween.EaseType.Out);

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
		
		List<CityTile> occupiedTiles = GetTilesForBuilding(buildingToRemove);
		foreach (CityTile tile in occupiedTiles)
		{
			tile.IsOccupied = false;
			tile.BuildingNode = null;
			freedTiles++;
		}

		// Restore inventory count when deleted!
		for (int i = 0; i < BuildingScenes.Count; i++)
		{
			if (BuildingScenes[i] != null && buildingToRemove.SceneFilePath == BuildingScenes[i].ResourcePath)
			{
				if (BuildingCounts.Count > i)
				{
					BuildingCounts[i]++;
					break;
				}
			}
		}

		SpawnFootprintParticles(occupiedTiles, buildingToRemove, DeletionParticleColor, true);

		Vector2 origPos = buildingToRemove.Position;
		BuildingSizeCategory cat = GetBuildingCategory(buildingToRemove, occupiedTiles);

		int shakeSteps = 4;
		if (cat == BuildingSizeCategory.Tall1x1 || cat == BuildingSizeCategory.Rectangle2x1) shakeSteps = 5;
		if (cat == BuildingSizeCategory.Apartment2x2) shakeSteps = 6;

		float shakeDir = GD.RandRange(-1.0, 1.0) >= 0 ? 1.0f : -1.0f;
		float randomMult = (float)GD.RandRange(0.85f, 1.15f);

		Tween shakeTween = buildingToRemove.CreateTween();
		float totalShakeTime = 0f;

		for (int i = 0; i < shakeSteps; i++)
		{
			float dist = Mathf.Lerp(2.2f, 0.4f, (float)i / shakeSteps) * randomMult * shakeDir;
			float stepTime = (float)GD.RandRange(0.035f, 0.055f);
			
			shakeTween.TweenProperty(buildingToRemove, "position:x", origPos.X + dist, stepTime);
			totalShakeTime += stepTime;
			shakeDir *= -1.0f;
		}
		shakeTween.TweenProperty(buildingToRemove, "position:x", origPos.X, 0.03f);
		totalShakeTime += 0.03f;

		Tween fadeTween = buildingToRemove.CreateTween();
		fadeTween.TweenInterval(totalShakeTime * 0.4f);
		fadeTween.TweenProperty(buildingToRemove, "modulate:a", 0.0f, totalShakeTime * 0.6f);
		
		fadeTween.Chain().TweenCallback(Callable.From(() => buildingToRemove.QueueFree()));

		GD.Print($"Deleted building, cleanly freed {freedTiles} tile(s).");
	}

	private List<CityTile> GetTilesForBuilding(Node2D building)
	{
		List<CityTile> tiles = new List<CityTile>();
		foreach (var kvp in buildableTiles)
		{
			if (kvp.Value.BuildingNode == building)
				tiles.Add(kvp.Value);
		}
		return tiles;
	}

	private BuildingSizeCategory GetBuildingCategory(Node2D building, List<CityTile> tiles)
	{
		if (tiles == null || tiles.Count == 0) return BuildingSizeCategory.House1x1;
		if (tiles.Count >= 4) return BuildingSizeCategory.Apartment2x2;
		if (tiles.Count == 2) return BuildingSizeCategory.Rectangle2x1;

		string nameLower = building != null ? building.Name.ToString().ToLower() : "";
		if (nameLower.Contains("tall") || nameLower.Contains("tower") || nameLower.Contains("office") || (!nameLower.Contains("house") && nameLower.Contains("2")))
		{
			return BuildingSizeCategory.Tall1x1;
		}
		return BuildingSizeCategory.House1x1;
	}

	// -----------------------------------------
	// PERIMETER GENERATOR
	// -----------------------------------------
	private void GetFilteredPerimeterPoints(List<CityTile> tiles, BuildingSizeCategory cat, out List<Vector2> backPoints, out List<Vector2> frontPoints)
	{
		backPoints = new List<Vector2>();
		frontPoints = new List<Vector2>();

		if (tiles.Count == 0) return;

		Vector2 footprintCenter = Vector2.Zero;
		foreach (CityTile tile in tiles)
		{
			footprintCenter += GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(tile.GridPosition));
		}
		footprintCenter /= tiles.Count;

		float sizeMultiplier = 1.0f;
		switch (cat)
		{
			case BuildingSizeCategory.House1x1: 
				sizeMultiplier = 1.45f;
				break;
			case BuildingSizeCategory.Tall1x1: 
				sizeMultiplier = 0.70f;
				break;
			case BuildingSizeCategory.Rectangle2x1: 
				sizeMultiplier = 0.88f;
				break;
			case BuildingSizeCategory.Apartment2x2: 
				sizeMultiplier = 0.70f;
				break;
		}

		float halfW = 14f;
		float halfH = 7f;
		int pointsPerEdge = 4;

		foreach (CityTile tile in tiles)
		{
			Vector2 center = GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(tile.GridPosition));
			Vector2 top = center + new Vector2(0, -halfH);
			Vector2 right = center + new Vector2(halfW, 0);
			Vector2 bottom = center + new Vector2(0, halfH);
			Vector2 left = center + new Vector2(-halfW, 0);

			if (!HasSameBuildingNeighbor(tile, TileSet.CellNeighbor.TopLeftSide))
			{
				for (int i = 0; i < pointsPerEdge; i++)
				{
					Vector2 pt = left.Lerp(top, (float)i / pointsPerEdge);
					Vector2 offset = pt - footprintCenter;
					backPoints.Add(footprintCenter + (offset * sizeMultiplier));
				}
			}

			if (!HasSameBuildingNeighbor(tile, TileSet.CellNeighbor.TopRightSide))
			{
				for (int i = 0; i < pointsPerEdge; i++)
				{
					Vector2 pt = top.Lerp(right, (float)i / pointsPerEdge);
					Vector2 offset = pt - footprintCenter;
					backPoints.Add(footprintCenter + (offset * sizeMultiplier));
				}
			}

			if (!HasSameBuildingNeighbor(tile, TileSet.CellNeighbor.BottomRightSide))
			{
				for (int i = 0; i < pointsPerEdge; i++)
				{
					Vector2 pt = right.Lerp(bottom, (float)i / pointsPerEdge);
					Vector2 offset = pt - footprintCenter;
					frontPoints.Add(footprintCenter + (offset * sizeMultiplier));
				}
			}

			if (!HasSameBuildingNeighbor(tile, TileSet.CellNeighbor.BottomLeftSide))
			{
				for (int i = 0; i < pointsPerEdge; i++)
				{
					Vector2 pt = bottom.Lerp(left, (float)i / pointsPerEdge);
					Vector2 offset = pt - footprintCenter;
					frontPoints.Add(footprintCenter + (offset * sizeMultiplier));
				}
			}
		}
	}

	private bool HasSameBuildingNeighbor(CityTile tile, TileSet.CellNeighbor neighborDir)
	{
		Vector2I neighborPos = GroundTileMap.GetNeighborCell(tile.GridPosition, neighborDir);
		if (buildableTiles.TryGetValue(neighborPos, out CityTile neighbor))
		{
			return neighbor.BuildingNode != null && neighbor.BuildingNode == tile.BuildingNode;
		}
		return false;
	}

	// -----------------------------------------
	// PARTICLE GENERATOR
	// -----------------------------------------
	private void SpawnFootprintParticles(List<CityTile> tiles, Node2D building, Color color, bool isDeleting = false)
	{
		if (tiles == null || tiles.Count == 0) return;

		BuildingSizeCategory cat = GetBuildingCategory(building, tiles);
		GetFilteredPerimeterPoints(tiles, cat, out List<Vector2> backPoints, out List<Vector2> frontPoints);

		int totalParticles = DefaultParticlesPerLayer * 2;
		switch (cat)
		{
			case BuildingSizeCategory.House1x1: totalParticles = 16; break;      
			case BuildingSizeCategory.Tall1x1: totalParticles = 36; break;       
			case BuildingSizeCategory.Rectangle2x1: totalParticles = 28; break;  
			case BuildingSizeCategory.Apartment2x2: totalParticles = 40; break;  
		}

		int amountPerLayer = Mathf.Max(4, totalParticles / 2);

		for (int i = 0; i < 2; i++)
		{
			bool isBehind = (i == 0);
			List<Vector2> targetPoints = isBehind ? backPoints : frontPoints;
			if (targetPoints.Count == 0) continue;

			CpuParticles2D particles = new CpuParticles2D();
			GetTree().CurrentScene.AddChild(particles);
			
			particles.GlobalPosition = Vector2.Zero;
			particles.ZIndex = isBehind ? (building.ZIndex - 1) : (building.ZIndex + 1);

			var img = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
			img.Fill(color);
			particles.Texture = ImageTexture.CreateFromImage(img);
			particles.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

			particles.Emitting = false;
			particles.OneShot = true;
			particles.Explosiveness = isDeleting ? 0.95f : 0.85f;
			particles.Amount = amountPerLayer; 

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
			particles.EmissionPoints = targetPoints.ToArray();

			particles.Direction = new Vector2(0, -1);
			particles.Spread = isDeleting ? 40f : 30f;
			particles.Gravity = new Vector2(0, -15f);

			Gradient fadeGradient = new Gradient();
			fadeGradient.SetColor(0, new Color(1, 1, 1, 1));
			fadeGradient.SetColor(1, new Color(1, 1, 1, 0));
			particles.ColorRamp = fadeGradient;

			particles.Emitting = true;
			GetTree().CreateTimer(0.6f).Timeout += () => particles.QueueFree();
		}
	}
}
