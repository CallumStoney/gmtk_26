using Godot;
using System;

public partial class PlaceButton : TextureButton
{
    // 1. Drag your BuildingManager node here in the Inspector
    [Export] public BuildingManager Manager;

    // 2. Type the ID of the building this button selects (0 for House, 1 for Office, etc.)
    [Export] public int BuildingIndex = 0;

    public override void _Ready()
    {
        Pressed += OnButtonPressed;
    }

    private void OnButtonPressed()
    {
        if (Manager == null)
        {
            GD.PrintErr("PlaceButton: BuildingManager reference is missing in the Inspector!");
            return;
        }

        // Uses your existing selection method in BuildingManager
        Manager.SelectBuilding(BuildingIndex);

        GD.Print($"Selected building index updated to: {BuildingIndex}");
    }

    public override void _ExitTree()
    {
        Pressed -= OnButtonPressed;
    }
}