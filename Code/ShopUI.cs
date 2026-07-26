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
    // INITIALIZATION (Runs ONLY ONCE on initial boot)
    // -----------------------------------------
    public override void _Ready()
    {
        // Ensure the popup is hidden when the game initially starts/boots up
        if (ShopPopup != null)
        {
            ShopPopup.Visible = false;
        }
    }

    // -----------------------------------------
    // TREE ENTRY & EXIT (Runs every time scenes swap)
    // -----------------------------------------
    public override void _EnterTree()
    {
        // Connect the button every time the scene enters the active game tree
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
        AudioManager.Instance?.PlayClick();

        // Drop focus from the button so it doesn't block keyboard/mouse inputs
        if (ToggleButton != null)
        {
            ToggleButton.ReleaseFocus();
        }

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