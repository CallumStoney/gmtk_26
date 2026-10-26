using Godot;
using System;

public partial class LabelJump : Label
{
    // [Export] allows you to change these values directly in the Godot Inspector
    [Export] public float JumpHeight = 20.0f;
    [Export] public float JumpSpeed = 5.0f;

    private float _timePassed = 0.0f;
    private float _startY;

    public override void _Ready()
    {
        // Record the starting Y position so it jumps relative to where you placed it
        _startY = Position.Y;
    }

    public override void _Process(double delta)
    {
        // 1. If it is the first time, do the jumping animation
        if (GameManager._firstTime)
        {
            // Keep track of time passing
            _timePassed += (float)delta;

            // Calculate the offset using a Sine wave
            float yOffset = (float)Math.Sin(_timePassed * JumpSpeed) * JumpHeight;

            // Apply the new position
            Position = new Vector2(Position.X, _startY + yOffset);
        }
        // 2. If it is NOT the first time, put it back to normal
        else
        {
            // Check if it's not already at the starting position to avoid doing this every single frame unnecessarily
            if (Position.Y != _startY)
            {
                Position = new Vector2(Position.X, _startY);

                // Reset the timer so that if it ever turns 'true' again, the animation starts smoothly from the beginning
                _timePassed = 0.0f;
            }
        }
    }
}