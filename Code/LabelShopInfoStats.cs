using Godot;
using System;

public partial class LabelShopInfoStats : Label
{
    // 1. Drag your BuildingManager node here in the Inspector
    [Export]
    public BuildingManager Manager;

    // 2. Type the ID of the building (0 for House, 1 for Office, etc.)
    [Export]
    public int BuildingIndex = 0;

    public override void _Ready()
    {
        // Automatically format and display the text as soon as the node loads
        RefreshStats();
    }

    public void RefreshStats()
    {
        // Safety check
        if (Manager == null)
        {
            Text = "Error: Manager not assigned.";
            return;
        }

        // Look up the array based on the exported BuildingIndex
        if (Manager.BuildingInfo.TryGetValue(BuildingIndex, out string[] infoArray))
        {
            // Ensure the array has at least 2 items (Index 0 is Name, Index 1 is Stats)
            if (infoArray.Length >= 2)
            {
                string rawStats = infoArray[1];

                // Replace comma+space, then replace any remaining stray commas with a newline
                Text = rawStats.Replace(", ", "\n").Replace(",", "\n");
            }
            else
            {
                Text = "Error: Stats missing in array.";
            }
        }
        else
        {
            Text = "Error: Building index not found.";
        }
    }
}