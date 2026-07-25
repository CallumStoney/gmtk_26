using Godot;
using System;

public partial class SellButtonInventory : TextureButton
{
    // 1. Drag your BuildingManager node here in the Inspector
    [Export] public BuildingManager Manager;

    // 2. Type the ID of the building this button sells (0 for House, 1 for Office, etc.)
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
        // Drop focus so it doesn't block inputs after clicking
        ReleaseFocus();

        if (Manager == null)
        {
            GD.PrintErr("SellButtonInventory: BuildingManager reference is missing in the Inspector!");
            return;
        }

        // Check if there is actual stock in the inventory to sell
        if (Manager.HasAvailableBuilding(BuildingIndex))
        {
            // 1. Deduct 1 from the stored building count
            Manager.BuildingCounts[BuildingIndex]--;

            // 2. Read the sell price from the dictionary array (Index 3)
            string sellPriceString = "0";
            if (Manager.BuildingInfo.TryGetValue(BuildingIndex, out string[] infoArray) && infoArray.Length >= 4)
            {
                sellPriceString = infoArray[3];
            }

            // 3. Parse the price using the manager's public helper method
            int parsedReward = Manager.ParsePrice(sellPriceString);

            // 4. Give the player the money!
            if (GameManager.Instance != null)
            {
                GameManager.Instance.Money += parsedReward;
            }

            GD.Print($"Sold 1 stock of building index {BuildingIndex} for {parsedReward}. Remaining stock: {Manager.BuildingCounts[BuildingIndex]}");
        }
        else
        {
            GD.Print($"Cannot sell: No inventory stock remaining for building index {BuildingIndex}.");
        }
    }
}