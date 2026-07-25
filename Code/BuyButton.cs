using Godot;
using System;

public partial class BuyButton : TextureButton
{
    // -----------------------------------------
    // INSPECTOR VARIABLES
    // -----------------------------------------
    // Reference to your scene's BuildingManager node
    [Export] public BuildingManager TargetBuildingManager;

    // The index of the building in BuildingManager's array (0 = first building, 1 = second building, etc.)
    [Export] public int BuildingTypeIndex = 0;

    // How many of this building to add per click
    [Export] public int AmountToAdd = 1;

    // -----------------------------------------
    // INITIALIZATION & CLEANUP
    // -----------------------------------------
    public override void _EnterTree()
    {
        // Connect the TextureButton's built-in "Pressed" signal every time the scene enters the tree
        Pressed -= OnButtonPressed; // Good practice to prevent double-connections
        Pressed += OnButtonPressed;
    }

    public override void _ExitTree()
    {
        // Clean up signal connection when node is removed (stashed by SceneManager)
        Pressed -= OnButtonPressed;
    }

    // -----------------------------------------
    // SIGNAL HANDLER
    // -----------------------------------------
    private void OnButtonPressed()
    {
        // --- NEW: Drop focus so it doesn't block other UI or keyboard inputs! ---
        ReleaseFocus();

        if (TargetBuildingManager == null)
        {
            GD.PrintErr($"BuyButton on '{Name}' has no TargetBuildingManager assigned in the Inspector!");
            return;
        }

        // Verify index is valid before modifying inventory
        if (BuildingTypeIndex >= 0 && BuildingTypeIndex < TargetBuildingManager.BuildingCounts.Count)
        {
            // 1. Add stock
            TargetBuildingManager.BuildingCounts[BuildingTypeIndex] += AmountToAdd;

            // 2. Automatically select this building for active placement
            TargetBuildingManager.CurrentSelectedIndex = BuildingTypeIndex;

            int currentStock = TargetBuildingManager.BuildingCounts[BuildingTypeIndex];
            GD.Print($"Added {AmountToAdd} building(s) at index {BuildingTypeIndex}. New total stock: {currentStock}. Active selection updated!");
        }
        else
        {
            GD.PrintErr($"Invalid BuildingTypeIndex ({BuildingTypeIndex}) on button '{Name}'. Check BuildingManager array bounds.");
        }
    }
}