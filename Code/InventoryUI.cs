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
    // INITIALIZATION & CLEANUP
    // -----------------------------------------
    public override void _EnterTree()
    {
        // Ensure the popup is hidden every time the scene enters the active game tree
        if (InventoryPopup != null)
        {
            InventoryPopup.Visible = false;
        }

        // Connect the button
        if (ToggleButton != null)
        {
            // Good practice: remove it first just in case, so it never double-connects
            ToggleButton.Pressed -= OnToggleButtonPressed;
            ToggleButton.Pressed += OnToggleButtonPressed;
        }
    }

    public override void _ExitTree()
    {
        // Clean up signal connection when the scene is stashed by SceneManager
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
        // --- NEW: Drop focus from the button so it doesn't block keyboard/mouse inputs! ---
        if (ToggleButton != null)
        {
            ToggleButton.ReleaseFocus();
        }

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