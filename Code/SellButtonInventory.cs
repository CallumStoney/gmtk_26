using Godot;
using System;

public partial class SellButtonInventory : TextureButton
{
    // 1. Drag your BuildingManager node here in the Inspector
    [Export] public BuildingManager Manager;

    // 2. Type the ID of the building this button sells (0 for House, 1 for Office, etc.)
    [Export] public int BuildingIndex = 0;

    public override void _Ready()
    {
        Pressed += OnButtonPressed;
    }

    private void OnButtonPressed()
    {
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
            string sellPrice = "0";
            if (Manager.BuildingInfo.TryGetValue(BuildingIndex, out string[] infoArray) && infoArray.Length >= 4)
            {
                sellPrice = infoArray[3];
            }

            // ADD YOUR CURRENCY / MONEY REWARD LOGIC HERE
            // e.g. PlayerEconomy.AddCash(sellPrice);

            GD.Print($"Sold 1 stock of building index {BuildingIndex} for {sellPrice}. Remaining stock: {Manager.BuildingCounts[BuildingIndex]}");
        }
        else
        {
            GD.Print($"Cannot sell: No inventory stock remaining for building index {BuildingIndex}.");
        }
    }

    public override void _ExitTree()
    {
        Pressed -= OnButtonPressed;
    }
}