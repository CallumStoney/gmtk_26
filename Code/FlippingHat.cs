using Godot;
using System;

public partial class FlippingHat : Sprite2D
{
	[Export] public float JumpHeight = 100f; 
	[Export] public float JumpDuration = 0.6f; // Even snappier!
	
	[Export] public float BounceHeight = 8f; 
	[Export] public float BounceDuration = 0.2f; 
	
	private Vector2 startPosition;

	public override void _Ready()
	{
		startPosition = Position;
		GetTree().CreateTimer(1.0f).Timeout += PerformFlip;
	}

	private void PerformFlip()
	{
		// 1. POSITION TWEEN
		Tween posTween = CreateTween();
		float jumpHalfTime = JumpDuration / 2f;
		float bounceHalfTime = BounceDuration / 2f;
		
		// Using "Sine" transition creates a fluid, organic arc without a dead stop at the peak
		posTween.TweenProperty(this, "position:y", startPosition.Y - JumpHeight, jumpHalfTime)
			 .SetTrans(Tween.TransitionType.Sine)
			 .SetEase(Tween.EaseType.Out);
			 
		posTween.TweenProperty(this, "position:y", startPosition.Y, jumpHalfTime)
			 .SetTrans(Tween.TransitionType.Sine)
			 .SetEase(Tween.EaseType.In);
			 
		// Subtle Bounce Up
		posTween.TweenProperty(this, "position:y", startPosition.Y - BounceHeight, bounceHalfTime)
			 .SetTrans(Tween.TransitionType.Quad)
			 .SetEase(Tween.EaseType.Out);
			 
		// Subtle Bounce Down
		posTween.TweenProperty(this, "position:y", startPosition.Y, bounceHalfTime)
			 .SetTrans(Tween.TransitionType.Quad)
			 .SetEase(Tween.EaseType.In);
			 
			 
		// 2. ROTATION TWEEN (Unified into one smooth spin so it doesn't stutter in the middle)
		Tween rotTween = CreateTween();
		
		rotTween.TweenProperty(this, "rotation_degrees", RotationDegrees + 360f, JumpDuration * 0.9f)
			 .SetTrans(Tween.TransitionType.Sine)
			 .SetEase(Tween.EaseType.InOut);
		
		// 3. THE LOOP
		posTween.Finished += () => 
		{
			GetTree().CreateTimer(2.0f).Timeout += PerformFlip;
		};
	}
}
