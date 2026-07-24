using Godot;
using System;

public partial class BuildingAmount : Label
{
    // Reference to your BuildingManager instance
    [Export] public BuildingManager Manager;

    // The index of the building in BuildingManager's arrays (0, 1, 2, etc.)
    [Export] public int BuildingIndex = 0;

    public override void _Ready()
    {
        // Fallback: If not set in the Inspector, try finding it in the scene tree
        if (Manager == null)
        {
            Manager = GetTree().Root.FindChild("BuildingManager", true, false) as BuildingManager;
        }
    }

    public override void _Process(double delta)
    {
        UpdateAmountDisplay();
    }

    /// <summary>
    /// Fetches the count from BuildingManager and formats the label text to "x#".
    /// </summary>
    public void UpdateAmountDisplay()
    {
        if (Manager == null) return;

        // Safely check if the index exists in the array
        if (BuildingIndex >= 0 && BuildingIndex < Manager.BuildingCounts.Count)
        {
            int count = Manager.BuildingCounts[BuildingIndex];
            Text = $"x{count}";
        }
        else
        {
            Text = "x0";
        }
    }
}