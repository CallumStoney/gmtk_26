using Godot;
using System;

public partial class ShakingLabel : Label
{
	private float timePassed = 0f;
	private Vector2 startPosition;

	// These will show up in the Godot Inspector!
	[Export] public float BobSpeed = 3.0f; 
	[Export] public float BobHeight = 5.0f; // How many pixels up and down it moves
	[Export] public bool ReverseStart = false; // Check this to make it start by going down instead of up!

	public override void _Ready()
	{
		// Remember exactly where we placed the text in the editor
		startPosition = Position;
	}

	public override void _Process(double delta)
	{
		timePassed += (float)delta;
		
		// Use Sin to create a buttery smooth up-and-down wave
		float offsetY = Mathf.Sin(timePassed * BobSpeed) * BobHeight;

		// Flip the direction if the box is checked
		if (ReverseStart)
		{
			offsetY = -offsetY;
		}

		// Apply the vertical movement while locking X in place
		Position = new Vector2(startPosition.X, startPosition.Y + offsetY);
	}
}
