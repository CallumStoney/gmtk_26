using Godot;
using System;

public partial class UIInventorySellLabel : Label
{
    // 1. Drag your BuildingManager node here in the Inspector
    [Export] public BuildingManager Manager;

    // 2. Type the ID of the building (0 for House, 1 for Office, etc.)
    [Export] public int BuildingIndex = 0;

    public override void _Ready()
    {
        CallDeferred(nameof(RefreshSellPrice));
    }

    public void RefreshSellPrice()
    {
        // Safety check
        if (Manager == null)
        {
            Text = "Sell Price: Error (Manager missing)";
            return;
        }

        // Look up the array based on the exported BuildingIndex
        if (Manager.BuildingInfo.TryGetValue(BuildingIndex, out string[] infoArray))
        {
            // Ensure array has at least 4 items (Index 0: Name, Index 1: Stats, Index 2: Buy, Index 3: Sell)
            if (infoArray.Length >= 4)
            {
                string sellPrice = infoArray[3];
                Text = $"Sell Price: {sellPrice}";
            }
            else
            {
                Text = "Sell Price: Error (Missing data)";
            }
        }
        else
        {
            Text = "Sell Price: N/A";
        }
    }
}