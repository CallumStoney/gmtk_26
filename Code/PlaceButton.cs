using Godot;
using System;

public partial class PlaceButton : TextureButton
{
    // 1. Drag your BuildingManager node here in the Inspector
    [Export] public BuildingManager Manager;

    // 2. Type the ID of the building this button selects (0 for House, 1 for Office, etc.)
    [Export] public int BuildingIndex = 0;

    // -----------------------------------------
    // INITIALIZATION & CLEANUP
    // -----------------------------------------
    public override void _EnterTree()
    {
        // Connect the signal every time the scene enters the active tree
        Pressed -= OnButtonPressed;
        Pressed += OnButtonPressed;
    }

    public override void _ExitTree()
    {
        // Clean up signal connection when node is removed
        Pressed -= OnButtonPressed;
    }

    // -----------------------------------------
    // SIGNAL HANDLER
    // -----------------------------------------
    private void OnButtonPressed()
    {
        AudioManager.Instance?.PlayClick();
        // Drop focus so it doesn't block inputs after clicking
        ReleaseFocus();

        if (Manager == null)
        {
            GD.PrintErr("PlaceButton: BuildingManager reference is missing in the Inspector!");
            return;
        }

        // Uses your existing selection method in BuildingManager
        Manager.SelectBuilding(BuildingIndex);

        GD.Print($"Selected building index updated to: {BuildingIndex}");
    }
}