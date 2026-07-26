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

    // Smoothness: Lower = floatier/softer, Higher = snappier/faster.
    // Recommended value range: 8f to 15f
    [Export] public float DragSmoothingSpeed = 10f;

    // --- Cursor Manager Reference ---
    [Export] public CursorManager CursorMgr;

    private bool _isDragging = false;
    private Vector2 _targetPosition;

    public override void _EnterTree()
    {
        MakeCurrent();
    }

    public override void _Ready()
    {
        // 1. CRITICAL: Force Godot's built-in smoothing OFF so it doesn't fight script lerping
        PositionSmoothingEnabled = false;

        Zoom = Zoom.Clamp(ZoomMin, ZoomMax);
        _targetPosition = Position;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // 1. Standard Mouse Interactions
        if (@event is InputEventMouseButton mouseButton)
        {
            bool isHoveringUI = GetViewport().GuiGetHoveredControl() != null;

            if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
            {
                if (isHoveringUI) return;

                Zoom += ZoomSpeed;
                Zoom = Zoom.Clamp(ZoomMin, ZoomMax);
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
            {
                if (isHoveringUI) return;

                Zoom -= ZoomSpeed;
                Zoom = Zoom.Clamp(ZoomMin, ZoomMax);
            }
            else if (mouseButton.ButtonIndex == MouseButton.Left ||
                     mouseButton.ButtonIndex == MouseButton.Right ||
                     mouseButton.ButtonIndex == MouseButton.Middle)
            {
                _isDragging = mouseButton.Pressed;

                if (_isDragging)
                {
                    CursorMgr?.SetDragCursor();
                }
                else
                {
                    CursorMgr?.SetDefaultCursor();
                }
            }
        }

        // Standard Mouse Dragging
        if (@event is InputEventMouseMotion mouseMotion && _isDragging)
        {
            _targetPosition -= mouseMotion.Relative / Zoom;
        }

        // 2. Trackpad Specific Interactions
        if (@event is InputEventMagnifyGesture magnifyGesture)
        {
            Zoom *= magnifyGesture.Factor;
            Zoom = Zoom.Clamp(ZoomMin, ZoomMax);
        }

        if (@event is InputEventPanGesture panGesture)
        {
            _targetPosition += (panGesture.Delta * TrackpadPanSpeed) / Zoom;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // 2. Framerate-Independent Exponential Decay Lerp Formula
        // This guarantees smooth movement regardless of FPS spikes or input polling rates.
        float weight = 1.0f - Mathf.Exp(-DragSmoothingSpeed * dt);
        Position = Position.Lerp(_targetPosition, weight);

        // 3. Snap when extremely close to prevent sub-pixel micro-jitter
        if (Position.DistanceSquaredTo(_targetPosition) < 0.01f)
        {
            Position = _targetPosition;
        }
    }
}