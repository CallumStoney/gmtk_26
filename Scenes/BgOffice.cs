using Godot;
using System;

public partial class BgOffice : Sprite2D
{
    // The amount to decrease the alpha by (0.5 means 50% more transparent)
    [Export] public float DecreaseAmount = 0.9f;

    public override void _Ready()
    {
        // 1. Grab the current color of the Sprite
        Color currentColor = Modulate;

        // 2. Decrease the Alpha (A) value, using Mathf.Max so it never drops below 0 (invisible)
        currentColor.A = Mathf.Max(0.0f, currentColor.A - DecreaseAmount);

        // 3. Apply the updated color back to the Sprite
        Modulate = currentColor;
    }
}