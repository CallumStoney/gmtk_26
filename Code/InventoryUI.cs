using Godot;

public partial class InventoryUI : Control
{
	// -----------------------------------------
	// INSPECTOR VARIABLES
	// -----------------------------------------
	[Export] public TextureButton ToggleButton;
	[Export] public Control InventoryPopup;

	// Reference to your scene's BuildingManager node
	[Export] public BuildingManager TargetBuildingManager;

	// -----------------------------------------
	// INITIALIZATION
	// -----------------------------------------
	public override void _Ready()
	{
		// Ensure the popup is hidden when the game starts
		if (InventoryPopup != null)
		{
			InventoryPopup.Visible = false;
		}

		// Connect the button
		if (ToggleButton != null)
		{
			ToggleButton.Pressed += OnToggleButtonPressed;
		}
	}

	public override void _ExitTree()
	{
		// Clean up signal connection when node is removed
		if (ToggleButton != null)
		{
			ToggleButton.Pressed -= OnToggleButtonPressed;
		}
	}

	// -----------------------------------------
	// SIGNAL HANDLER
	// -----------------------------------------
	private void OnToggleButtonPressed()
	{
		// Flips the visibility back and forth
		if (InventoryPopup != null)
		{
			InventoryPopup.Visible = !InventoryPopup.Visible;
		}

		// Reset building selection to nothing
		if (TargetBuildingManager != null)
		{
			TargetBuildingManager.CurrentSelectedIndex = -1;
		}
		else
		{
			GD.PrintErr($"InventoryUI on '{Name}' has no TargetBuildingManager assigned in the Inspector!");
		}
	}
}
