using Godot;

public partial class ShopUI : Control
{
    // -----------------------------------------
    // INSPECTOR VARIABLES
    // -----------------------------------------
    [Export] public TextureButton ToggleButton;
    [Export] public Control ShopPopup;

    // Reference to your scene's BuildingManager node
    [Export] public BuildingManager TargetBuildingManager;

    // -----------------------------------------
    // INITIALIZATION
    // -----------------------------------------
    public override void _Ready()
    {
        // Ensure the popup is hidden when the game starts
        if (ShopPopup != null)
        {
            ShopPopup.Visible = false;
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
        if (ShopPopup != null)
        {
            ShopPopup.Visible = !ShopPopup.Visible;
        }

        // Reset building selection to nothing
        if (TargetBuildingManager != null)
        {
            TargetBuildingManager.CurrentSelectedIndex = -1;
        }
        else
        {
            GD.PrintErr($"ShopUI on '{Name}' has no TargetBuildingManager assigned in the Inspector!");
        }
    }
}