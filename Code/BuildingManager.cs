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

	private int maxBuildingLevel = 4; // Make sure this matches your highest possible upg

	// -----------------------------------------
	// INSPECTOR VARIABLES
	// -----------------------------------------
	[Export] public TileMapLayer GroundTileMap;

	[Export] public Camera2D MainCamera;

	// --- PRESET MAP SETUP ---
	[ExportGroup("Preset Map Setup")]
	[Export] public Resource InitialMapStateRaw { get; set; }

	// Explicitly defining Godot.Collections so C# doesn't get confused
	[Export]
	public Godot.Collections.Dictionary<int, string[]> BuildingInfo { get; set; } = new Godot.Collections.Dictionary<int, string[]>();

	[Export]
	public Godot.Collections.Array<PackedScene> BuildingScenes = new Godot.Collections.Array<PackedScene>();

	[Export]
	public Godot.Collections.Array<FootprintType> BuildingFootprints = new Godot.Collections.Array<FootprintType>();

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
	[Export] public Color SellParticleColor = new Color(0.2f, 1.0f, 0.2f, 1.0f); // GREEN
	[Export] public int DefaultParticlesPerLayer = 16;

	// --- NEW UI EXPORTS ---
	[ExportGroup("UI References")]
	[Export] public Control ActionMenu;
	[Export] public TextureButton BtnSell;      // <-- Changed to TextureButton
	[Export] public TextureButton BtnInfo;      // <-- Changed to TextureButton
	[Export] public TextureButton BtnUpgrade;   // <-- Changed to TextureButton
	[Export] public TextureButton BtnStore;     // <-- Changed to TextureButton

	[Export] public ConfirmationDialog SellDialog;
	[Export] public ConfirmationDialog UpgradeDialog;
	[Export] public Control InfoPanel;
	[Export] public Label InfoTitleLabel;
	[Export] public Label InfoBodyLabel;

	// -----------------------------------------
	// TILE IDENTIFIERS
	// -----------------------------------------
	private readonly int TargetSourceId = 1;
	private readonly Vector2I TargetAtlasCoord = new Vector2I(0, 0);

	// -----------------------------------------
	// STATE TRACKING
	// -----------------------------------------
	private int currentSelectedIndex = -1;
	public int CurrentSelectedIndex
	{
		get => currentSelectedIndex;
		set => SelectBuilding(value);
	}

	private int buildingRotation = 0;
	private Node2D previewNode = null;
	private Vector2 targetPreviewPosition = Vector2.Zero;
	private Label tooltipLabel;

	// State for the currently selected building for the Action Menu
	private CityTile targetActionTile = null;

	// -----------------------------------------
	// THE TILE DATA CLASS
	// -----------------------------------------
	public class CityTile
	{
		public Vector2I GridPosition;
		public bool IsOccupied;
		public List<CityTile> Neighbors;
		public Node2D BuildingNode;
		public int BuildingTypeIndex { get; set; } = -1;
		public int RotationIndex { get; set; } = 0; // <-- ADD THIS


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
		// Tell Godot to wait until the end of the frame when ALL nodes 
		// (including GameManager) are fully loaded and ready before running our setup.
		CallDeferred(MethodName.InitializeAfterGameManager);
	}

	private void InitializeAfterGameManager()
	{
		if (GameManager.Instance == null)
		{
			GD.PrintErr("CRITICAL: GameManager.Instance is STILL null!");
			return;
		}

		// 1. Pass Inspector data to GameManager
		GameManager.Instance.BuildingInfo = this.BuildingInfo;
		GameManager.Instance.BuildingInventory = this.BuildingCounts;

		// Parse all building stats ONCE
		GameManager.Instance.InitializeBuildingStats();

		// 2. Setup tracking arrays inside GameManager
		GameManager.Instance.PlacedBuildingCounts = new int[BuildingScenes.Count];
		GameManager.Instance.PlacedBuildingLevelCounts = new int[BuildingScenes.Count, maxBuildingLevel + 1];

		if (GroundTileMap == null ||
			BuildingScenes.Count != BuildingFootprints.Count ||
			BuildingScenes.Count != BuildingCounts.Count)
		{
			GD.PrintErr("CRITICAL: Check GroundTileMap reference and array sizes!");
			return;
		}

		// Setup tooltip
		tooltipLabel = new Label();
		tooltipLabel.ZIndex = 4090;
		tooltipLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1));
		tooltipLabel.AddThemeConstantOverride("outline_size", 4);
		tooltipLabel.AddThemeFontSizeOverride("font_size", 4);
		tooltipLabel.Visible = false;
		AddChild(tooltipLabel);

		ScanForBuildableTiles();
		ConnectNeighbors();

		// --- 3. SPAWN PRESET BUILDINGS ---
		LoadInitialSaveState();

		// --- 4. RECALCULATE CAPACITY IMMEDIATELY FOR DAY 1 ---
		// Now that buildings are placed on the map, sync GameManager stats right away!
		GameManager.Instance.RecalculateDailyYields();
		GameManager.Instance.SyncInitialCapacity();

		RefreshPreviewNode();

		// Connect UI Signals
		if (BtnSell != null) BtnSell.Pressed += OnSellButtonPressed;
		if (BtnInfo != null) BtnInfo.Pressed += OnInfoButtonPressed;
		if (BtnUpgrade != null) BtnUpgrade.Pressed += OnUpgradeButtonPressed;
		if (BtnStore != null) BtnStore.Pressed += OnStoreButtonPressed;

		if (SellDialog != null) SellDialog.Confirmed += OnSellConfirmed;
		if (UpgradeDialog != null) UpgradeDialog.Confirmed += OnUpgradeConfirmed;
	}

	public override void _Process(double delta)
	{
		UpdatePreviewPositionAndValidation((float)delta);

		// Update Tooltip Position & Visibility
		if (currentSelectedIndex >= 0 && tooltipLabel != null)
		{
			tooltipLabel.Visible = true;
			tooltipLabel.Text = "R to Rotate\nRight Click to Deselect";
			Vector2 targetTooltipPos = GetGlobalMousePosition() + new Vector2(-35, 10);
			tooltipLabel.GlobalPosition = tooltipLabel.GlobalPosition.Lerp(targetTooltipPos, (float)delta * 15.0f);
		}
		else if (tooltipLabel != null)
		{
			tooltipLabel.Visible = false;
		}

		// --- Lock Action Menu to the building (BULLETPROOF METHOD) ---
		if (ActionMenu != null && ActionMenu.Visible && targetActionTile != null && targetActionTile.BuildingNode != null)
		{
			// 1. Where is the building on the screen?
			Vector2 screenPosition = targetActionTile.BuildingNode.GetGlobalTransformWithCanvas().Origin;

			// 2. Your ideal offset (treated as WORLD coordinates)
			Vector2 worldOffset = new Vector2(0, -50);

			if (MainCamera != null)
			{
				// Apply zoom scale
				ActionMenu.Scale = MainCamera.Zoom;

				// Multiply the offset by zoom so the gap shrinks/grows proportionally 
				Vector2 screenOffset = worldOffset * MainCamera.Zoom;

				// The exact pixel on the screen we want the CENTER of the menu to be
				Vector2 targetCenterPixel = screenPosition + screenOffset;

				// Perfect centering: Shift the top-left position by exactly half of its scaled visual size
				ActionMenu.Position = targetCenterPixel - ((ActionMenu.Size * ActionMenu.Scale) / 2.0f);
			}
			else
			{
				// Fallback if MainCamera is null
				Vector2 targetCenterPixel = screenPosition + worldOffset;
				ActionMenu.Position = targetCenterPixel - (ActionMenu.Size / 2.0f);
			}
		}
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

	// NEW: The method that updates the array when a building shouts that it leveled up
	private void HandleBuildingLevelChanged(int typeIndex, int oldLevel, int newLevel)
	{
		GameManager.Instance.PlacedBuildingLevelCounts[typeIndex, oldLevel]--;
		GameManager.Instance.PlacedBuildingLevelCounts[typeIndex, newLevel]++;

		GameManager.Instance.RecalculateDailyYields();
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
		if (index < 0 || index >= GameManager.Instance.BuildingInventory.Count) return false;
		return GameManager.Instance.BuildingInventory[index] > 0;
	}

	// -----------------------------------------
	// INPUT & ROUTING
	// -----------------------------------------
	// -----------------------------------------
	// INPUT & ROUTING
	// -----------------------------------------
	public override void _UnhandledInput(InputEvent @event)
	{

		// 1. Keyboard Inputs
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			int pressedNumber = (int)keyEvent.Keycode - (int)Key.Key0;

			if (pressedNumber >= 0 && pressedNumber <= 9) SelectBuilding(pressedNumber);
			else if (keyEvent.Keycode == Key.R) RotateBuilding();
			else if (keyEvent.Keycode == Key.Escape) SelectBuilding(-1);

			if (keyEvent.Keycode == Key.F9)
			{
			   // GenerateDevSaveFile();
				return;
			} 

			/*
			if (keyEvent.Keycode == Key.P)
			{
				string summary = "--- PLACED BUILDINGS TRACKER ---\n";
				int typeCount = GameManager.Instance.PlacedBuildingLevelCounts.GetLength(0);
				int levelCount = GameManager.Instance.PlacedBuildingLevelCounts.GetLength(1);

				for (int type = 0; type < typeCount; type++)
				{
					summary += $"Building Type {type} (Total: {GameManager.Instance.PlacedBuildingCounts[type]}):\n";
					for (int lvl = 0; lvl < levelCount; lvl++)
					{
						int countAtLevel = GameManager.Instance.PlacedBuildingLevelCounts[type, lvl];
						summary += $"  - Level {lvl}: {countAtLevel}\n";
					}
				}

				GD.Print(summary);
			} */
		}

		// 2. Mouse Click Input
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
		{
			// --- NEW: CLOSE MENU ON ANY UNHANDLED CLICK ---
			// If you click a UI button, it consumes the event and NEVER reaches this code.
			// So if this code runs while the menu is open, it means you clicked OUTSIDE the buttons.
			if (ActionMenu != null && ActionMenu.Visible)
			{
				CloseActionMenu();
				// We DO NOT "return" here. This allows for seamless switching! 
				// If you clicked another building, the code continues below and opens the new menu instantly.
				// If you clicked empty grass or off-screen, it hits the checks below and safely does nothing.
			}

			// --- RIGHT CLICK: CANCEL / DESELECT ---
			if (mouseEvent.ButtonIndex == MouseButton.Right)
			{
				if (currentSelectedIndex >= 0)
				{
					SelectBuilding(-1); // Drop the building you are holding
				}
				return;
			}

			Vector2 localMousePos = GroundTileMap.ToLocal(GetGlobalMousePosition());
			Vector2I clickGridPos = GroundTileMap.LocalToMap(localMousePos);

			// BOUNDS CHECK: If clicked outside the map, stop here. 
			// (Because the CloseActionMenu code is ABOVE this check, the menu successfully closes!)
			if (!buildableTiles.ContainsKey(clickGridPos)) return;

			CityTile clickedTile = buildableTiles[clickGridPos];

			// --- LEFT CLICK: PLACE OR SELECT MENU ---
			if (mouseEvent.ButtonIndex == MouseButton.Left)
			{
				if (clickedTile.IsOccupied && clickedTile.BuildingNode != null)
				{
					SelectBuilding(-1); // Automatically drop any building you are holding
					OpenActionMenu(clickedTile);
					return;
				}

				// If we are holding a building, place it
				if (currentSelectedIndex >= 0)
				{
					if (!HasAvailableBuilding(currentSelectedIndex))
					{
						GD.Print($"Cannot place: No remaining stock!");
						return;
					}

					FootprintType activeFootprint = BuildingFootprints[currentSelectedIndex];

					if (activeFootprint == FootprintType.Single1x1) TryPlaceSingleTile(clickGridPos);
					else if (activeFootprint == FootprintType.DynamicHorizontal2x1) TryPlaceTwoTile(clickGridPos);
					else if (activeFootprint == FootprintType.Square2x2) TryPlaceFourTile(clickGridPos);
				}
			}
		}
	}

	// -----------------------------------------
	// ACTION MENU LOGIC
	// -----------------------------------------
	private void OpenActionMenu(CityTile tile)
	{
		targetActionTile = tile;
		Node2D bld = tile.BuildingNode;
		List<CityTile> tiles = GetTilesForBuilding(bld);

		// 1. Play Jump Animation
		PlayBuildingJumpAnimation(bld, tiles);

		// 2. Position Menu above building
		if (ActionMenu != null)
		{
			Vector2 screenPosition = bld.GetGlobalTransformWithCanvas().Origin;
			Vector2 worldOffset = new Vector2(-60, -70);

			if (MainCamera != null)
			{
				ActionMenu.Scale = MainCamera.Zoom;
				Vector2 screenOffset = worldOffset * MainCamera.Zoom;
				Vector2 targetCenterPixel = screenPosition + screenOffset;
				ActionMenu.Position = targetCenterPixel - ((ActionMenu.Size * ActionMenu.Scale) / 2.0f);
			}
			else
			{
				Vector2 targetCenterPixel = screenPosition + worldOffset;
				ActionMenu.Position = targetCenterPixel - (ActionMenu.Size / 2.0f);
			}

			ActionMenu.Visible = true;

			// 3. Evaluate Buttons & Apply Alpha Greying
			UpgradableBuilding ub = bld as UpgradableBuilding;
			bool isUpgradable = (ub != null);

			if (BtnStore != null)
			{
				// Can only store if it's NOT an upgraded building.
				// If it HAS upgrades (CanDowngrade == true), they must sell upgrades first.
				bool canStore = !isUpgradable || !ub.CanDowngrade();
				UpdateButtonState(BtnStore, canStore);
			}

			if (BtnUpgrade != null)
			{
				// Can only upgrade if it's upgradable AND hasn't reached max level
				bool canUpgrade = isUpgradable && ub.CanUpgrade();
				UpdateButtonState(BtnUpgrade, canUpgrade);
			}

			// Sell and Info are always clickable!
			UpdateButtonState(BtnSell, true);
			UpdateButtonState(BtnInfo, true);
		}
	}

	private void CloseActionMenu()
	{
		targetActionTile = null;
		if (ActionMenu != null) ActionMenu.Visible = false;
		if (InfoPanel != null) InfoPanel.Visible = false;
	}

	// --- BUTTON BINDINGS ---
	private void OnSellButtonPressed()
	{
		if (targetActionTile == null) return;

		// 1. Set fallback values just in case the dictionary is missing data
		string buildingName = "this building";
		string sellPrice = "Unknown amount";

		// 2. Look up the building info using the tile's index
		if (BuildingInfo.TryGetValue(targetActionTile.BuildingTypeIndex, out string[] infoArray))
		{
			// 3. Make sure the array actually has the data
			if (infoArray.Length >= 4)
			{
				buildingName = infoArray[0]; // Grab the Name
				sellPrice = infoArray[3];    // Grab the Sell Price
			}
		}

		// 4. Inject BOTH the name and the price dynamically into the text
		if (targetActionTile.BuildingNode is UpgradableBuilding ub && ub.CanDowngrade())
		{
			SellDialog.DialogText = $"Sell one upgrade for the {buildingName} for {sellPrice}?";
		}
		else
		{
			SellDialog.DialogText = $"Sell {buildingName} for {sellPrice}?";
		}

		SellDialog.PopupCentered();
	}

	private void OnSellConfirmed()
	{
		if (targetActionTile != null)
		{
			Node2D bld = targetActionTile.BuildingNode;
			int typeIndex = targetActionTile.BuildingTypeIndex;

			// 1. Look up the sell price
			int sellReward = 0;
			if (BuildingInfo.TryGetValue(typeIndex, out string[] infoArray) && infoArray.Length >= 4)
			{
				sellReward = ParsePrice(infoArray[3]); // Index 3 is your Sell Price
			}

			// 2. If it's an upgraded building being downgraded:
			if (bld is UpgradableBuilding ub && ub.CanDowngrade())
			{
				if (ub.TryDowngrade())
				{
					// GIVE THE PLAYER THEIR MONEY
					if (GameManager.Instance != null) GameManager.Instance.Money += sellReward;

					SpawnFootprintParticles(GetTilesForBuilding(ub), ub, SellParticleColor, false);
					PlayBuildingJumpAnimation(ub, GetTilesForBuilding(ub));
					CloseActionMenu();
					return; // Stop here! Do NOT delete the base building.
				}
			}

			// 3. If it's a base building being completely sold:

			// GIVE THE PLAYER THEIR MONEY
			if (GameManager.Instance != null) GameManager.Instance.Money += sellReward;

			DeleteOrStoreBuilding(bld, refundToInventory: false, particleColor: SellParticleColor);
			CloseActionMenu();
		}
	}

	private void OnInfoButtonPressed()
	{
		if (targetActionTile == null) return;

		if (InfoTitleLabel == null || InfoBodyLabel == null)
		{
			GD.PrintErr("Info labels are missing in the BuildingManager Inspector!");
			return;
		}

		// TOGGLE CHECK: If panel is already open, hide it and close
		if (InfoPanel != null && InfoPanel.Visible)
		{
			InfoPanel.Visible = false;
			return;
		}

		int index = targetActionTile.BuildingTypeIndex;

		if (BuildingInfo.TryGetValue(index, out string[] infoArray))
		{
			if (infoArray.Length >= 4)
			{
				string buildingName = infoArray[0];
				string sellPrice = infoArray[3];
				string rawStats = infoArray[1];

				if (targetActionTile.BuildingNode is UpgradableBuilding ub && ub.CurrentLevel > 0)
				{
					int targetStatsIndex = 4 + ub.CurrentLevel;
					if (infoArray.Length > targetStatsIndex)
					{
						rawStats = infoArray[targetStatsIndex];
						buildingName = $"Lvl {ub.CurrentLevel + 1} {buildingName}";
					}
				}

				string stats = rawStats.Replace(", ", "\n").Replace(",", "\n");

				// Direct plain text into standard crisp labels
				InfoTitleLabel.Text = buildingName;
				InfoBodyLabel.Text = $"{stats}\nSell Price: {sellPrice}";
			}
		}

		InfoPanel.Visible = true;
	}

	private void OnUpgradeButtonPressed()
	{
		if (targetActionTile == null) return;

		// 1. Set fallback values just in case the dictionary is missing data
		string buildingName = "this building";
		string upgradeCost = "Unknown amount";

		// 2. Look up the building info using the tile's index
		if (BuildingInfo.TryGetValue(targetActionTile.BuildingTypeIndex, out string[] infoArray))
		{
			// 3. Make sure the array actually has the upgrade cost (Index 4 means Length must be >= 5)
			if (infoArray.Length >= 5)
			{
				buildingName = infoArray[0]; // Grab the Name
				upgradeCost = infoArray[4];  // Grab the Upgrade Cost
			}
			else
			{
				GD.PrintErr($"Building array for {buildingName} doesn't have an upgrade cost at index 4!");
			}
		}

		// 4. Inject the name, cost, and warning note dynamically into the text
		UpgradeDialog.DialogText = $"Upgrade {buildingName} for {upgradeCost}?\n\n(Note: Upgraded buildings cannot be stored in your inventory.)";

		UpgradeDialog.PopupCentered();
	}

	private void OnStoreButtonPressed()
	{
		if (targetActionTile == null) return;
		// Store puts it in inventory (refund = true), uses normal deletion particles
		DeleteOrStoreBuilding(targetActionTile.BuildingNode, refundToInventory: true, particleColor: DeletionParticleColor);
		CloseActionMenu();
	}

	// --- CONFIRMATION BINDINGS ---

	private void OnUpgradeConfirmed()
	{
		// Make sure we have a tile selected and that the building on it can actually be upgraded
		if (targetActionTile != null && targetActionTile.BuildingNode is UpgradableBuilding ub)
		{
			int typeIndex = targetActionTile.BuildingTypeIndex;

			// 1. Are they already at max level?
			if (!ub.CanUpgrade())
			{
				GD.Print("Building is already at max level!");
				return;
			}

			// 2. Figure out the upgrade cost
			int upgradeCost = 0;

			// Assuming Index 4 in your dictionary is the Upgrade Price (e.g., "$15k")
			if (BuildingInfo.TryGetValue(typeIndex, out string[] infoArray) && infoArray.Length >= 5)
			{
				upgradeCost = ParsePrice(infoArray[4]);
			}
			else
			{
				// Fallback: If you don't have it in the dictionary yet, make a default cost that scales with level
				upgradeCost = 5000 * (ub.CurrentLevel + 1);
			}

			// 3. Check if the player can afford it
			if (GameManager.Instance != null)
			{
				if (GameManager.Instance.Money >= upgradeCost)
				{
					// 4. Deduct the money!
					GameManager.Instance.Money -= upgradeCost;

					// 5. Trigger the upgrade
					if (ub.TryUpgrade())
					{
						GD.Print($"Upgraded building to Level {ub.CurrentLevel} for {upgradeCost}!");

						// Add some juice! Reusing your existing particle/animation methods looks great here.
						PlayBuildingJumpAnimation(ub, GetTilesForBuilding(ub));

						// You could make a new color variable for upgrades, like Colors.Cyan or Colors.Gold
						SpawnFootprintParticles(GetTilesForBuilding(ub), ub, Colors.Gold, false);
					}

					CloseActionMenu();
				}
				else
				{
					GD.Print("Not enough money to upgrade!");
					// Note: You could shake the UI or play an error buzzer sound here
				}
			}
		}
	}

	// -----------------------------------------
	// SELECTION & PREVIEW 
	// -----------------------------------------
	public void SelectBuilding(int index)
	{
		// Force the action menu to close if the user presses a number key to grab a new building
		CloseActionMenu();

		if (index >= 0 && index < BuildingScenes.Count && index < BuildingFootprints.Count && BuildingScenes[index] != null)
		{
			currentSelectedIndex = index;
			buildingRotation = 0;

			if (tooltipLabel != null)
			{
				tooltipLabel.GlobalPosition = GetGlobalMousePosition() + new Vector2(-35, 10);
			}
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
			buildingRotation = (buildingRotation + 1) % 2;
		else
			buildingRotation = (buildingRotation + 1) % 4;

		UpdatePreviewTransform(GetTargetNeighborEnum());
	}

	private void UpdateButtonState(TextureButton button, bool isEnabled)
	{
		if (button == null) return;
		button.Disabled = !isEnabled;
		// 1.0f is fully visible, 0.4f is transparent/greyed out
		button.Modulate = isEnabled ? new Color(1, 1, 1, 1.0f) : new Color(1, 1, 1, 0.4f);
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

		if (activeFootprint == FootprintType.Single1x1) return true;
		if (activeFootprint == FootprintType.DynamicHorizontal2x1) return secondTile != null && !secondTile.IsOccupied;

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
		float depthNudge = 0f;

		if (activeFootprint == FootprintType.DynamicHorizontal2x1 && secondTile != null)
		{
			float secondTileY = GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(secondTile.GridPosition)).Y;
			maxDepthY = Mathf.Max(maxDepthY, secondTileY);
			depthNudge = 1.0f;
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
			depthNudge = 2.0f;
		}

		return Mathf.RoundToInt(maxDepthY - depthNudge) + 10;
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
				secondCenter = GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(secondTile.GridPosition));
			else
			{
				Vector2I neighborPos = GroundTileMap.GetNeighborCell(pivotGridPos, matchedNeighbor);
				secondCenter = GroundTileMap.ToGlobal(GroundTileMap.MapToLocal(neighborPos));
			}

			return ((pivotCenter + secondCenter) * 0.5f) + BuildingVisualOffset + customOffset;
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

		if (!CheckCanPlaceAt(pivotGridPos, secondTile)) return;

		CommitBuilding(new List<CityTile> { pivotTile, secondTile }, pivotGridPos, secondTile, matchedNeighbor);
	}

	private void TryPlaceFourTile(Vector2I pivotGridPos)
	{
		List<CityTile> tiles = GetSquare2x2Tiles(pivotGridPos);

		if (tiles == null || !HasAvailableBuilding(currentSelectedIndex)) return;

		CommitBuilding(tiles, pivotGridPos, null, TileSet.CellNeighbor.BottomRightSide);
	}

	public void RemoveBuildingFromTracker(Node2D buildingToRemove, int typeIndex)
	{
		// 1. Subtract from GameManager total count
		GameManager.Instance.PlacedBuildingCounts[typeIndex]--;

		// 2. Subtract from the level tracker and unsubscribe
		if (buildingToRemove is UpgradableBuilding ub)
		{
			GameManager.Instance.PlacedBuildingLevelCounts[typeIndex, ub.CurrentLevel]--;
			ub.OnLevelChanged -= HandleBuildingLevelChanged;
		}
		else
		{
			// THE FIX: Remove basic buildings from the daily yield tracker when sold!
			GameManager.Instance.PlacedBuildingLevelCounts[typeIndex, 0]--;
		}
	}

	private void CommitBuilding(List<CityTile> tilesToOccupy, Vector2I pivotGridPos, CityTile secondTile, TileSet.CellNeighbor matchedNeighbor)
	{
		// 1. Deduct from GameManager inventory
		GameManager.Instance.BuildingInventory[currentSelectedIndex]--;


		Vector2 finalWorldPos = CalculateBuildingWorldPosition(pivotGridPos, secondTile, matchedNeighbor);
		int finalDepth = CalculateDepth(pivotGridPos, secondTile);

		foreach (CityTile tile in tilesToOccupy)
		{
			tile.IsOccupied = true;
			tile.BuildingTypeIndex = currentSelectedIndex;

			tile.RotationIndex = buildingRotation;
		}

		Node2D newBuilding = BuildingScenes[currentSelectedIndex].Instantiate<Node2D>();
		AddChild(newBuilding);

		// 2. Add 1 to the total count in GameManager
		GameManager.Instance.PlacedBuildingCounts[currentSelectedIndex]++;

		// 3. Track building levels in GameManager
		if (newBuilding is UpgradableBuilding ub)
		{
			ub.BuildingTypeIndex = currentSelectedIndex;
			ub.OnLevelChanged += HandleBuildingLevelChanged;
			GameManager.Instance.PlacedBuildingLevelCounts[currentSelectedIndex, 0]++; // <--- FIX HERE
		}
		else
		{
			// THE FIX: Basic buildings still need to be counted as Level 0!
			GameManager.Instance.PlacedBuildingLevelCounts[currentSelectedIndex, 0]++;
		}

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

		foreach (CityTile tile in tilesToOccupy)
		{
			tile.BuildingNode = newBuilding;
		}

		// Drop-in Animation
		Vector2 targetPos = finalWorldPos;
		Vector2 originalScale = newBuilding.Scale;

		BuildingSizeCategory cat = GetBuildingCategory(newBuilding, tilesToOccupy);
		float dropY = -4f;
		float dropTime = 0.06f;
		Vector2 landScale = new Vector2(originalScale.X * 1.03f, originalScale.Y * 0.97f);
		float reboundTime = 0.06f;

		switch (cat)
		{
			case BuildingSizeCategory.Tall1x1: dropY = -6f; dropTime = 0.07f; landScale = new Vector2(originalScale.X * 1.04f, originalScale.Y * 0.96f); reboundTime = 0.07f; break;
			case BuildingSizeCategory.Apartment2x2: dropY = -8f; dropTime = 0.09f; landScale = new Vector2(originalScale.X * 1.05f, originalScale.Y * 0.95f); reboundTime = 0.08f; break;
		}

		newBuilding.GlobalPosition = targetPos + new Vector2(0, dropY);
		newBuilding.Scale = new Vector2(originalScale.X * 0.98f, originalScale.Y * 1.02f);

		Tween tween = newBuilding.CreateTween();
		tween.TweenProperty(newBuilding, "global_position", targetPos, dropTime).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() => SpawnFootprintParticles(tilesToOccupy, newBuilding, ConstructionParticleColor, false)));
		tween.TweenProperty(newBuilding, "scale", landScale, 0.04f);
		tween.TweenProperty(newBuilding, "scale", originalScale, reboundTime).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

		if (!HasAvailableBuilding(currentSelectedIndex)) SelectBuilding(-1);

		GameManager.Instance.RecalculateDailyYields();
	}

	// -----------------------------------------
	// ANIMATIONS
	// -----------------------------------------
	private void PlayBuildingJumpAnimation(Node2D building, List<CityTile> tilesOccupied)
	{
		Vector2 originalScale = building.Scale;
		BuildingSizeCategory cat = GetBuildingCategory(building, tilesOccupied);

		float dropY = -4f;
		float dropTime = 0.06f;
		Vector2 landScale = new Vector2(originalScale.X * 1.03f, originalScale.Y * 0.97f);
		float reboundTime = 0.06f;

		switch (cat)
		{
			case BuildingSizeCategory.Tall1x1: dropY = -6f; dropTime = 0.07f; landScale = new Vector2(originalScale.X * 1.04f, originalScale.Y * 0.96f); reboundTime = 0.07f; break;
			case BuildingSizeCategory.Apartment2x2: dropY = -8f; dropTime = 0.09f; landScale = new Vector2(originalScale.X * 1.05f, originalScale.Y * 0.95f); reboundTime = 0.08f; break;
		}

		Vector2 startPos = building.GlobalPosition;
		building.GlobalPosition = startPos + new Vector2(0, dropY);
		building.Scale = new Vector2(originalScale.X * 0.98f, originalScale.Y * 1.02f);

		Tween tween = building.CreateTween();
		tween.TweenProperty(building, "global_position", startPos, dropTime).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		tween.TweenProperty(building, "scale", landScale, 0.04f);
		tween.TweenProperty(building, "scale", originalScale, reboundTime).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
	}

	// -----------------------------------------
	// DELETION / STORE LOGIC
	// -----------------------------------------
	private void DeleteOrStoreBuilding(Node2D buildingToRemove, bool refundToInventory, Color particleColor)
	{
		if (buildingToRemove == null) return;

		int refundedIndex = -1;
		List<CityTile> occupiedTiles = new List<CityTile>();

		foreach (var kvp in buildableTiles)
		{
			if (kvp.Value.BuildingNode == buildingToRemove)
			{
				refundedIndex = kvp.Value.BuildingTypeIndex;
				occupiedTiles.Add(kvp.Value);
				kvp.Value.IsOccupied = false;
				kvp.Value.BuildingNode = null;
				kvp.Value.BuildingTypeIndex = -1;
			}
		}

		// --- NEW: INVENTORY & TRACKER UPDATES ---
		if (refundedIndex >= 0)
		{
			// 1. Add it back to the player's UI inventory
			if (refundToInventory && refundedIndex < BuildingCounts.Count)
			{
				BuildingCounts[refundedIndex]++;
			}

			// 2. Remove it from our map tracking arrays!
			RemoveBuildingFromTracker(buildingToRemove, refundedIndex);
		}
		// ----------------------------------------

		SpawnFootprintParticles(occupiedTiles, buildingToRemove, particleColor, true);

		Vector2 origPos = buildingToRemove.Position;
		BuildingSizeCategory cat = GetBuildingCategory(buildingToRemove, occupiedTiles);

		int shakeSteps = (cat == BuildingSizeCategory.Apartment2x2) ? 6 : 4;
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

		GameManager.Instance.RecalculateDailyYields();
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
	// PERIMETER / PARTICLES GENERATOR
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
			case BuildingSizeCategory.House1x1: sizeMultiplier = 1.45f; break;
			case BuildingSizeCategory.Tall1x1: sizeMultiplier = 0.70f; break;
			case BuildingSizeCategory.Rectangle2x1: sizeMultiplier = 0.88f; break;
			case BuildingSizeCategory.Apartment2x2: sizeMultiplier = 0.70f; break;
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
					backPoints.Add(footprintCenter + ((pt - footprintCenter) * sizeMultiplier));
				}
			}
			if (!HasSameBuildingNeighbor(tile, TileSet.CellNeighbor.TopRightSide))
			{
				for (int i = 0; i < pointsPerEdge; i++)
				{
					Vector2 pt = top.Lerp(right, (float)i / pointsPerEdge);
					backPoints.Add(footprintCenter + ((pt - footprintCenter) * sizeMultiplier));
				}
			}
			if (!HasSameBuildingNeighbor(tile, TileSet.CellNeighbor.BottomRightSide))
			{
				for (int i = 0; i < pointsPerEdge; i++)
				{
					Vector2 pt = right.Lerp(bottom, (float)i / pointsPerEdge);
					frontPoints.Add(footprintCenter + ((pt - footprintCenter) * sizeMultiplier));
				}
			}
			if (!HasSameBuildingNeighbor(tile, TileSet.CellNeighbor.BottomLeftSide))
			{
				for (int i = 0; i < pointsPerEdge; i++)
				{
					Vector2 pt = bottom.Lerp(left, (float)i / pointsPerEdge);
					frontPoints.Add(footprintCenter + ((pt - footprintCenter) * sizeMultiplier));
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

	// -----------------------------------------
	// SAVE & LOAD SYSTEM
	// -----------------------------------------
	private void LoadInitialSaveState()
	{
		// Wipe the slate clean on GameManager before spawning the saved map
		if (GameManager.Instance.PlacedBuildingCounts != null)
			System.Array.Clear(GameManager.Instance.PlacedBuildingCounts, 0, GameManager.Instance.PlacedBuildingCounts.Length);

		if (GameManager.Instance.PlacedBuildingLevelCounts != null)
			System.Array.Clear(GameManager.Instance.PlacedBuildingLevelCounts, 0, GameManager.Instance.PlacedBuildingLevelCounts.Length);

		GD.Print("--- ATTEMPTING TO LOAD MAP PRESET ---");

		MapSaveState mapState = InitialMapStateRaw as MapSaveState;

		if (mapState == null || mapState.PlacedBuildings == null || mapState.PlacedBuildings.Count == 0)
		{
			GD.PrintErr("FAILED: Save file is missing or empty.");
			return;
		}

		GD.Print($"SUCCESS: Found save file with {mapState.PlacedBuildings.Count} buildings. Spawning now...");

		// 1. Cache the player's actual selection so we don't mess up their cursor
		int cachedSelectedIndex = currentSelectedIndex;
		int cachedRotation = buildingRotation;

		foreach (BuildingSaveData bldData in mapState.PlacedBuildings)
		{
			if (buildableTiles.TryGetValue(bldData.GridPosition, out CityTile pivotTile))
			{
				if (!pivotTile.IsOccupied)
				{
					// 2. SPOOF the selection state so your helper functions work for this specific building
					currentSelectedIndex = bldData.BuildingTypeIndex;
					buildingRotation = bldData.RotationIndex;
					FootprintType activeFootprint = BuildingFootprints[currentSelectedIndex];

					// 3. Figure out all the tiles this footprint needs to occupy
					List<CityTile> tilesToOccupy = new List<CityTile> { pivotTile };
					CityTile secondTile = null;
					TileSet.CellNeighbor matchedNeighbor = TileSet.CellNeighbor.BottomRightSide;

					if (activeFootprint == FootprintType.DynamicHorizontal2x1)
					{
						secondTile = GetTwoByOneSecondaryTile(bldData.GridPosition, out matchedNeighbor);
						if (secondTile != null) tilesToOccupy.Add(secondTile);
					}
					else if (activeFootprint == FootprintType.Square2x2)
					{
						List<CityTile> sqTiles = GetSquare2x2Tiles(bldData.GridPosition);
						if (sqTiles != null) tilesToOccupy = sqTiles;
					}

					// --- THE FIX ---
					// Calculate visual positions HERE, while the tiles are still marked as "empty" (IsOccupied = false).
					// This allows your calculators to correctly find the center of the 4x4 grid!
					Vector2 finalWorldPos = CalculateBuildingWorldPosition(bldData.GridPosition, secondTile, matchedNeighbor);
					int finalDepth = CalculateDepth(bldData.GridPosition, secondTile);

					// 4. Lock the grid (NOW we can safely mark them as occupied)
					foreach (CityTile tile in tilesToOccupy)
					{
						tile.IsOccupied = true;
						tile.BuildingTypeIndex = bldData.BuildingTypeIndex;
					}

					// 5. Instantiate & Attach
					Node2D newBuilding = BuildingScenes[bldData.BuildingTypeIndex].Instantiate<Node2D>();
					AddChild(newBuilding);

					GameManager.Instance.PlacedBuildingCounts[bldData.BuildingTypeIndex]++;

					foreach (CityTile tile in tilesToOccupy)
					{
						tile.BuildingNode = newBuilding;
					}

					// 6. Apply EXACT visual setup
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

					// Assign the proper depth and position that we calculated earlier
					newBuilding.ZIndex = finalDepth;
					newBuilding.GlobalPosition = finalWorldPos;

					// Apply the saved upgrade level and hook up the tracker in GameManager!
					if (newBuilding is UpgradableBuilding loadedUb)
					{
						loadedUb.BuildingTypeIndex = bldData.BuildingTypeIndex;
						loadedUb.OnLevelChanged += HandleBuildingLevelChanged; // Subscribe

						loadedUb.LoadSavedLevel(bldData.CurrentLevel);

						// Log it directly to its loaded level in GameManager
						GameManager.Instance.PlacedBuildingLevelCounts[bldData.BuildingTypeIndex, loadedUb.CurrentLevel]++;
					}
					else
					{
						// Basic buildings loaded from the save file still need to be counted!
						GameManager.Instance.PlacedBuildingLevelCounts[bldData.BuildingTypeIndex, 0]++;
					}
				}
				else
				{
					GD.PrintErr($"Tile {bldData.GridPosition} is already occupied! Skipping.");
				}
			}
		}

		// 7. Restore the player's cursor state to normal
		currentSelectedIndex = cachedSelectedIndex;
		buildingRotation = cachedRotation;

		// 8. Recalculate statutory yields for the entire newly loaded map!
		GameManager.Instance.RecalculateDailyYields();
	}

	private void GenerateDevSaveFile()
	{
		MapSaveState newSaveState = new MapSaveState();
		System.Collections.Generic.List<Node2D> processedBuildings = new System.Collections.Generic.List<Node2D>();

		foreach (var kvp in buildableTiles)
		{
			CityTile tile = kvp.Value;

			if (tile.IsOccupied && tile.BuildingNode != null && !processedBuildings.Contains(tile.BuildingNode))
			{
				processedBuildings.Add(tile.BuildingNode);

				BuildingSaveData bldData = new BuildingSaveData
				{
					GridPosition = tile.GridPosition,
					BuildingTypeIndex = tile.BuildingTypeIndex,
					// --- ADDED THIS LINE HERE ---
					RotationIndex = tile.RotationIndex
				};

				// Save the upgrade level if applicable
				if (tile.BuildingNode is UpgradableBuilding ub)
				{
					bldData.CurrentLevel = ub.CurrentLevel;
				}

				newSaveState.PlacedBuildings.Add(bldData);
			}
		}

		Error err = ResourceSaver.Save(newSaveState, "res://DevMapPreset.tres");

		if (err == Error.Ok)
			GD.Print("SUCCESS: Preset map saved to res://DevMapPreset.tres!");
		else
			GD.PrintErr("Failed to save map preset. Error code: " + err);
	}

	public int ParsePrice(string priceString)
	{
		if (string.IsNullOrWhiteSpace(priceString)) return 0;

		// Clean the string: remove '$', spaces, and make lowercase so 'K' and 'k' both work
		string cleanPrice = priceString.Replace("$", "").Trim().ToLower();

		if (cleanPrice.EndsWith("k"))
		{
			// Remove the 'k' and multiply by 1000
			cleanPrice = cleanPrice.Replace("k", "");
			if (float.TryParse(cleanPrice, out float floatValue))
			{
				return Mathf.RoundToInt(floatValue * 1000f);
			}
		}
		else
		{
			// Just standard numbers
			if (int.TryParse(cleanPrice, out int intValue))
			{
				return intValue;
			}
		}

		GD.PrintErr($"Failed to parse price string: {priceString}");
		return 0;
	}

	private void ParseStatValue(string statsString, ref int pop, ref int pol, ref int popu, ref int mon, ref int cap)
	{
		if (string.IsNullOrWhiteSpace(statsString)) return;

		// Split by comma to handle strings like: "Money +50, Pollution -10"
		string[] stats = statsString.Split(',', System.StringSplitOptions.RemoveEmptyEntries);

		foreach (string stat in stats)
		{
			string cleanStat = stat.Trim();

			// Split by space to separate the StatName and the Number
			string[] parts = cleanStat.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);

			if (parts.Length >= 2)
			{
				// Remove any colons just in case you wrote "Money: +50" instead of "Money +50"
				string statName = parts[0].ToLower().Replace(":", "");

				// Remove explicit plus signs because C# parsing can occasionally fail on them
				string numberString = parts[1].Replace("+", "");

				if (int.TryParse(numberString, out int value))
				{
					switch (statName)
					{
						case "population": pop += value; break;
						case "pollution": pol += value; break;
						case "approval": popu += value; break; 
						case "money": mon += value; break;
						case "capacity": cap += value; break;
					}
				}
			}
		}
	}


}
