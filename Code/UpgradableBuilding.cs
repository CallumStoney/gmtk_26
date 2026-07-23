using Godot;

public partial class UpgradableBuilding : Sprite2D
{
    [Export] public Sprite2D BuildingSprite;

    // Define the sprite regions for each level. 
    // Index 0 is the base level, Index 1 is the first upgrade, etc.
    [Export] public Godot.Collections.Array<Rect2> UpgradeRegions = new Godot.Collections.Array<Rect2>();

    public int CurrentLevel { get; private set; } = 0;

    public override void _Ready()
    {
        if (BuildingSprite != null && UpgradeRegions.Count > 0)
        {
            BuildingSprite.RegionEnabled = true;
            BuildingSprite.RegionRect = UpgradeRegions[0];
        }
        else
        {
            GD.PrintErr($"UpgradableBuilding '{Name}' is missing its Sprite2D or UpgradeRegions!");
        }
    }

    // Called on Left Click. Returns true if it successfully upgraded.
    public bool TryUpgrade()
    {
        if (CurrentLevel < UpgradeRegions.Count - 1)
        {
            CurrentLevel++;
            BuildingSprite.RegionRect = UpgradeRegions[CurrentLevel];
            return true;
        }
        return false; // Already at max level
    }

    // Called on Right Click. Returns true if downgraded, false if it's at the base level.
    public bool TryDowngrade()
    {
        if (CurrentLevel > 0)
        {
            CurrentLevel--;
            BuildingSprite.RegionRect = UpgradeRegions[CurrentLevel];
            return true;
        }
        return false; // At base level, ready to be deleted
    }
}