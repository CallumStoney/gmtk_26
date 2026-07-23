using Godot;
using System;

public partial class Camera2d : Camera2D
{
    // Zoom Settings
    [Export] public Vector2 ZoomMin = new Vector2(0.5f, 0.5f);
    [Export] public Vector2 ZoomMax = new Vector2(3.0f, 3.0f);
    [Export] public Vector2 ZoomSpeed = new Vector2(0.1f, 0.1f);

    // Trackpad Settings
    [Export] public float TrackpadPanSpeed = 35f;

    // How softly the camera tracks your mouse. Lower = softer, Higher = snappier.
    [Export] public float DragSmoothingSpeed = 20f;

    // ---> THIS IS THE VARIABLE YOU ASSIGN IN THE INSPECTOR <---
    [Export] public CanvasLayer GameUI;

    private bool _isDragging = false;
    private Vector2 _targetPosition;

    public override void _Ready()
    {
        Zoom = Zoom.Clamp(ZoomMin, ZoomMax);
        _targetPosition = Position;

        // Sync the UI scale as soon as the game starts
        UpdateUIScale();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // 1. Standard Mouse Interactions
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
            {
                Zoom += ZoomSpeed;
                Zoom = Zoom.Clamp(ZoomMin, ZoomMax);
                UpdateUIScale();
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
            {
                Zoom -= ZoomSpeed;
                Zoom = Zoom.Clamp(ZoomMin, ZoomMax);
                UpdateUIScale();
            }
            else if (mouseButton.ButtonIndex == MouseButton.Middle)
            {
                _isDragging = mouseButton.Pressed;

                if (_isDragging)
                {
                    _targetPosition = Position;
                }
            }
        }

        // Standard Middle-Mouse Dragging
        if (@event is InputEventMouseMotion mouseMotion && _isDragging)
        {
            _targetPosition -= mouseMotion.Relative / Zoom;
        }

        // 2. Trackpad Specific Interactions
        // Handles pinch-to-zoom on supported trackpads/touchscreens
        if (@event is InputEventMagnifyGesture magnifyGesture)
        {
            Zoom *= magnifyGesture.Factor;
            Zoom = Zoom.Clamp(ZoomMin, ZoomMax);
            UpdateUIScale();
        }

        // Handles two-finger panning on supported trackpads
        if (@event is InputEventPanGesture panGesture)
        {
            _targetPosition += (panGesture.Delta * TrackpadPanSpeed) / Zoom;
        }
    }

    public override void _Process(double delta)
    {
        Position = Position.Lerp(_targetPosition, (float)delta * DragSmoothingSpeed);
    }

    private void UpdateUIScale()
    {
        if (GameUI != null)
        {
            // This applies the exact camera zoom level to the CanvasLayer
            GameUI.Scale = Zoom;
        }
    }
}