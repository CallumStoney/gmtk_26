using Godot;
using System;

public partial class AnimationBuilding : AnimatedSprite2D
{
    private UpgradableBuilding _parentBuilding;

    public override void _Ready()
    {
        // 1. Grab the parent node and check if it has the UpgradableBuilding script
        _parentBuilding = GetParent() as UpgradableBuilding;

        // Call Play() directly on this node
        Play("default");
    }

    public override void _Process(double delta)
    {
        // 2. If it DOES have the UpgradableBuilding script, tie visibility to the level
        if (_parentBuilding != null)
        {
            // It will be visible (true) at level 0, and hidden (false) at level 1, 2, 3...
            Visible = _parentBuilding.CurrentLevel == 0;
        }
    }

    public void StopAnimation()
    {
        Stop();
    }
}