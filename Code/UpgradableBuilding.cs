using Godot;

public partial class UpgradableBuilding : Sprite2D
{
    [Export] public Sprite2D BuildingSprite;
    // Define the sprite regions for each level. 
    // Index 0 is the base level, Index 1 is the first upgrade, etc.
    [Export] public Godot.Collections.Array<Rect2> UpgradeRegions = new Godot.Collections.Array<Rect2>();

    [Export] public int MaxLevel = 3; // Change 3 to whatever your max upgrade limit is

    public int CurrentLevel { get; private set; } = 0;
    private Tween currentTween;

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

    // Inside UpgradableBuilding.cs

    // Returns true if we have not reached the max level
    public bool CanUpgrade()
    {
        return CurrentLevel < MaxLevel; // (Change these variable names to match your script)
    }

    // Returns true if the building currently has upgrades applied to it (Level 2 or higher)
    public bool CanDowngrade()
    {
        return CurrentLevel > 0; // (Change this variable name to match your script)
    }

    // Called on Left Click. Returns true if it successfully upgraded.
    public bool TryUpgrade()
    {
        if (CurrentLevel < UpgradeRegions.Count - 1)
        {
            CurrentLevel++;
            if (BuildingSprite != null)
                BuildingSprite.RegionRect = UpgradeRegions[CurrentLevel];
            else
                RegionRect = UpgradeRegions[CurrentLevel];

            Sprite2D spriteToAnimate = BuildingSprite != null ? BuildingSprite : this;
            Vector2 origPos = spriteToAnimate.Position;

            // --- SIZE-ADAPTED UPGRADE JUICE ---
            bool isLarge = RegionRect.Size.X > 48 || Name.ToString().ToLower().Contains("apartment");
            float hopY = isLarge ? -3.0f : -1.5f;
            float hopTime = isLarge ? 0.08f : 0.06f;

            currentTween?.Kill();
            currentTween = CreateTween();

            spriteToAnimate.Modulate = new Color(1.1f, 1.1f, 1.05f, 1.0f);
            currentTween.TweenProperty(spriteToAnimate, "position:y", origPos.Y + hopY, hopTime)
                        .SetTrans(Tween.TransitionType.Quad)
                        .SetEase(Tween.EaseType.Out);
            currentTween.Parallel().TweenProperty(spriteToAnimate, "modulate", Colors.White, hopTime * 2f);

            currentTween.Chain().TweenProperty(spriteToAnimate, "position:y", origPos.Y, hopTime * 1.2f)
                        .SetTrans(Tween.TransitionType.Sine)
                        .SetEase(Tween.EaseType.Out);

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
            if (BuildingSprite != null)
                BuildingSprite.RegionRect = UpgradeRegions[CurrentLevel];
            else
                RegionRect = UpgradeRegions[CurrentLevel];

            Sprite2D spriteToAnimate = BuildingSprite != null ? BuildingSprite : this;
            Vector2 origPos = spriteToAnimate.Position;

            // --- SIZE-ADAPTED DOWNGRADE SHAKE ---
            bool isLarge = RegionRect.Size.X > 48 || Name.ToString().ToLower().Contains("apartment");
            float shakeX = isLarge ? 1.0f : 0.5f;
            float stepTime = isLarge ? 0.035f : 0.025f;

            currentTween?.Kill();
            currentTween = CreateTween();
            currentTween.TweenProperty(spriteToAnimate, "position:x", origPos.X + (shakeX * 2f), stepTime);
            currentTween.TweenProperty(spriteToAnimate, "position:x", origPos.X - (shakeX * 1.5f), stepTime);
            currentTween.TweenProperty(spriteToAnimate, "position:x", origPos.X + shakeX, stepTime);
            currentTween.TweenProperty(spriteToAnimate, "position:x", origPos.X, stepTime);

            return true;
        }
        return false; // At base level, ready to be deleted
    }
}