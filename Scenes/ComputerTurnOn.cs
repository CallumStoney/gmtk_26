using Godot;
using System;

public partial class ComputerTurnOn : AnimatedSprite2D
{
    // _EnterTree fires every time the scene is swapped back into the active game tree
    public override void _EnterTree()
    {
        base._EnterTree();
        ResetAndPlay();
    }

    public override void _Ready()
    {
        VisibilityChanged += OnVisibilityChanged;

        // Connect to the built-in signal that fires when the animation completes
        AnimationFinished += OnAnimationFinished;
    }

    private void OnVisibilityChanged()
    {
        if (Visible)
        {
            ResetAndPlay();
        }
    }

    private void ResetAndPlay()
    {
        // Hide all children immediately
        SetChildrenVisibility(false);

        // Force the animation back to the very first frame
        Frame = 0;

        // Start playing the default animation
        Play();
    }

    private void OnAnimationFinished()
    {
        // Reveal all children once the animation is done
        SetChildrenVisibility(true);
    }

    // Helper method to loop through and toggle visibility
    // Helper method to loop through and toggle visibility
    private void SetChildrenVisibility(bool isVisible)
    {
        foreach (Node child in GetChildren())
        {
            if (child is CanvasItem canvasItem)
            {
                // 1. Toggle opacity (Alpha) so we don't fight with RoomScene's visibility logic
                Color currentColor = canvasItem.Modulate;
                currentColor.A = isVisible ? 1.0f : 0.0f;
                canvasItem.Modulate = currentColor;
            }

            // 2. Disable the node entirely while hidden so the player can't click invisible buttons!
            child.ProcessMode = isVisible ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        }
    }
}