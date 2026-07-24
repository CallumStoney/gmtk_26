using Godot;
using System;

public partial class UIShopPriceLabel : Label
{
    // 1. Drag your BuildingManager node here in the Inspector
    [Export] public BuildingManager Manager;

    // 2. Type the ID of the building (0 for House, 1 for Office, etc.)
    [Export] public int BuildingIndex = 0;

    public override void _Ready()
    {
        CallDeferred(nameof(RefreshPrice));
    }

    public void RefreshPrice()
    {
        // Safety check
        if (Manager == null)
        {
            Text = "Price: Error (Manager missing)";
            return;
        }

        // Look up the array based on the exported BuildingIndex
        if (Manager.BuildingInfo.TryGetValue(BuildingIndex, out string[] infoArray))
        {
            // Ensure the array has at least 3 items (Index 0: Name, Index 1: Stats, Index 2: Buy Price)
            if (infoArray.Length >= 3)
            {
                string price = infoArray[2];
                Text = $"Price: {price}";
            }
            else
            {
                Text = "Price: Error (Missing data)";
            }
        }
        else
        {
            Text = "Price: N/A";
        }
    }
}