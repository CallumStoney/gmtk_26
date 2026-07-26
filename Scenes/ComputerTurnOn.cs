using Godot;
using System;

public partial class ComputerTurnOn : AnimatedSprite2D
{
    [Export] public Node NormalUI;
    [Export] public Node SpecialEventUI;

    private enum ScreenPhase
    {
        TurningOnNormal,
        NormalActive,
        TurningOffForEvent,
        TurningOnForEvent,
        EventActive,
        TurningOffToEndEvent
    }

    private ScreenPhase _currentPhase = ScreenPhase.TurningOnNormal;

    public override void _EnterTree()
    {
        base._EnterTree();
        // Hide EVERYTHING immediately when entering the tree before frame 1 renders
        SetUIState(showNormal: false, showSpecial: false);
    }

    public override void _Ready()
    {
        AnimationFinished += OnAnimationFinished;
        VisibilityChanged += OnVisibilityChanged;

        // Start the turn-on sequence immediately on launch
        StartNormalTurnOn();
    }

    private void OnVisibilityChanged()
    {
        if (Visible && IsInsideTree())
        {
            StartNormalTurnOn();
        }
    }

    private void StartNormalTurnOn()
    {
        _currentPhase = ScreenPhase.TurningOnNormal;
        SetUIState(showNormal: false, showSpecial: false);

        Frame = 0;
        Play();
    }

    public void StartSpecialEvent()
    {
        if (_currentPhase != ScreenPhase.NormalActive) return;

        _currentPhase = ScreenPhase.TurningOffForEvent;
        SetUIState(showNormal: false, showSpecial: false);

        Frame = SpriteFrames.GetFrameCount(Animation) - 1;
        PlayBackwards();
    }

    public void EndSpecialEvent()
    {
        if (_currentPhase != ScreenPhase.EventActive) return;

        _currentPhase = ScreenPhase.TurningOffToEndEvent;
        SetUIState(showNormal: false, showSpecial: false);

        Frame = SpriteFrames.GetFrameCount(Animation) - 1;
        PlayBackwards();
    }

    private void OnAnimationFinished()
    {
        switch (_currentPhase)
        {
            case ScreenPhase.TurningOnNormal:
                _currentPhase = ScreenPhase.NormalActive;
                SetUIState(showNormal: true, showSpecial: false);
                break;

            case ScreenPhase.TurningOffForEvent:
                _currentPhase = ScreenPhase.TurningOnForEvent;
                Frame = 0;
                Play();
                break;

            case ScreenPhase.TurningOnForEvent:
                _currentPhase = ScreenPhase.EventActive;
                SetUIState(showNormal: false, showSpecial: true);
                break;

            case ScreenPhase.TurningOffToEndEvent:
                _currentPhase = ScreenPhase.TurningOnNormal;
                Frame = 0;
                Play();
                break;
        }
    }

    private void SetUIState(bool showNormal, bool showSpecial)
    {
        SetContainerVisible(NormalUI, showNormal);
        SetContainerVisible(SpecialEventUI, showSpecial);
    }

    private void SetContainerVisible(Node container, bool isVisible)
    {
        if (container == null) return;

        container.ProcessMode = isVisible ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        ToggleChildrenVisibility(container, isVisible);
    }

    private void ToggleChildrenVisibility(Node parent, bool isVisible)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is CanvasItem canvasItem)
            {
                canvasItem.Visible = isVisible;
            }
            else if (child is CanvasLayer canvasLayer)
            {
                canvasLayer.Visible = isVisible;
            }

            if (child.GetChildCount() > 0)
            {
                ToggleChildrenVisibility(child, isVisible);
            }
        }
    }
}