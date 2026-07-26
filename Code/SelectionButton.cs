using Godot;
using System;

public partial class SelectionButton : TextureButton
{
    // -----------------------------------------
    // INSPECTOR VARIABLES
    // -----------------------------------------
    // Reference to your scene's BuildingManager node
    [Export] public BuildingManager TargetBuildingManager;

    // The building scene this button will select when clicked
    [Export] public PackedScene BuildingScene;

    // -----------------------------------------
    // INITIALIZATION
    // -----------------------------------------
    public override void _Ready()
    {
        // Connect the TextureButton's built-in "Pressed" signal
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

        if (TargetBuildingManager == null)
        {
            GD.PrintErr($"SelectionButton on '{Name}' has no TargetBuildingManager assigned in the Inspector!");
            return;
        }

        if (BuildingScene == null)
        {
            GD.PrintErr($"SelectionButton on '{Name}' has no BuildingScene assigned in the Inspector!");
            return;
        }

        // Find the position of this PackedScene in the BuildingManager's array
        int index = TargetBuildingManager.BuildingScenes.IndexOf(BuildingScene);

        if (index != -1)
        {
            // TOGGLE SELECTION:
            // If this building is already selected, deselect it (-1).
            if (TargetBuildingManager.CurrentSelectedIndex == index)
            {
                TargetBuildingManager.CurrentSelectedIndex = -1;
                GD.Print($"Deselected building '{BuildingScene.ResourcePath}'.");
            }
            else
            {
                TargetBuildingManager.CurrentSelectedIndex = index;
                GD.Print($"Selected building '{BuildingScene.ResourcePath}' at index {index}.");
            }
        }
        else
        {
            GD.PrintErr($"The assigned BuildingScene on '{Name}' does not exist in TargetBuildingManager's BuildingScenes array!");
        }
    }
}