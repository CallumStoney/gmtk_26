using Godot;
using System;

public partial class BuyButton : TextureButton
{
    // -----------------------------------------
    // INSPECTOR VARIABLES
    // -----------------------------------------
    [Export] public BuildingManager TargetBuildingManager;

    [Export] public int BuildingTypeIndex = 0;
    [Export] public int AmountToAdd = 1;

    // Cache the price (-1 means not cached yet)
    private int _buyPrice = -1;

    // -----------------------------------------
    // INITIALIZATION & CLEANUP
    // -----------------------------------------
    public override void _Ready()
    {
        // Hook into the GameManager's signal so the button updates automatically!
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StatsChanged += UpdateButtonState;
        }

        // Wait until all nodes finish loading to run initial check
        CallDeferred(nameof(UpdateButtonState));
    }

    public override void _EnterTree()
    {
        Pressed -= OnButtonPressed;
        Pressed += OnButtonPressed;
    }

    public override void _ExitTree()
    {
        Pressed -= OnButtonPressed;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StatsChanged -= UpdateButtonState;
        }
    }

    // -----------------------------------------
    // HELPER METHODS
    // -----------------------------------------
    private int GetBuyPrice()
    {
        // Return cached price if valid
        if (_buyPrice > 0) return _buyPrice;

        if (TargetBuildingManager != null &&
            TargetBuildingManager.BuildingInfo != null &&
            TargetBuildingManager.BuildingInfo.TryGetValue(BuildingTypeIndex, out string[] infoArray) &&
            infoArray.Length >= 3)
        {
            // Index 2 is the Buy Price
            int parsedPrice = TargetBuildingManager.ParsePrice(infoArray[2]);

            // Only cache if parsing was successful (> 0)
            if (parsedPrice > 0)
            {
                _buyPrice = parsedPrice;
            }
            return parsedPrice;
        }

        return 0; // Don't cache invalid results!
    }

    public void UpdateButtonState()
    {
        if (TargetBuildingManager == null || GameManager.Instance == null) return;

        int price = GetBuyPrice();

        // If price couldn't be loaded yet, don't update state
        if (price <= 0) return;

        bool canAfford = GameManager.Instance.Money >= price;

        // 1. Tell Godot UI if button is disabled
        Disabled = !canAfford;

        // 2. Visually grey it out
        Modulate = canAfford ? Colors.White : new Color(0.5f, 0.5f, 0.5f, 1.0f);
    }

    // -----------------------------------------
    // SIGNAL HANDLER
    // -----------------------------------------
    private void OnButtonPressed()
    {
        ReleaseFocus();

        if (TargetBuildingManager == null || GameManager.Instance == null)
        {
            GD.PrintErr($"BuyButton on '{Name}' has a missing manager reference!");
            return;
        }

        int price = GetBuyPrice();

        if (price <= 0)
        {
            GD.PrintErr($"BuyButton on '{Name}': Invalid buy price parsed!");
            return;
        }

        // Safety check: Affordability
        if (GameManager.Instance.Money < price)
        {
            GD.Print("Cannot afford this building!");
            return;
        }

        if (BuildingTypeIndex >= 0 && BuildingTypeIndex < TargetBuildingManager.BuildingCounts.Count)
        {
            // DEDUCT MONEY
            GameManager.Instance.Money -= price;

            // Add stock
            TargetBuildingManager.BuildingCounts[BuildingTypeIndex] += AmountToAdd;

            // Automatically select building for placement
            TargetBuildingManager.CurrentSelectedIndex = BuildingTypeIndex;

            int currentStock = TargetBuildingManager.BuildingCounts[BuildingTypeIndex];
            GD.Print($"Bought {AmountToAdd} building(s) at index {BuildingTypeIndex} for ${price}. New stock: {currentStock}.");

            UpdateButtonState();
        }
        else
        {
            GD.PrintErr($"Invalid BuildingTypeIndex ({BuildingTypeIndex}) on button '{Name}'.");
        }
    }
}