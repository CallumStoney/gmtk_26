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
    [Export] public float DragSmoothingSpeed = 10f;

    // --- Camera Bounds ---
    [ExportGroup("Camera Bounds")]
    [Export] public bool UseBounds = true;
    [Export] public Vector2 MinBounds = new Vector2(-2000f, -2000f); // Top-Left Edge Limit
    [Export] public Vector2 MaxBounds = new Vector2(2000f, 2000f);  // Bottom-Right Edge Limit

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
        PositionSmoothingEnabled = false;
        Zoom = Zoom.Clamp(ZoomMin, ZoomMax);
        _targetPosition = GlobalPosition; // Use GlobalPosition just to be safe
        ClampTargetPosition();
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
                ClampTargetPosition(); // Re-clamp when viewport size changes
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
            {
                if (isHoveringUI) return;
                Zoom -= ZoomSpeed;
                Zoom = Zoom.Clamp(ZoomMin, ZoomMax);
                ClampTargetPosition(); // Re-clamp when viewport size changes
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
            ClampTargetPosition();
        }

        // 2. Trackpad Specific Interactions
        if (@event is InputEventMagnifyGesture magnifyGesture)
        {
            Zoom *= magnifyGesture.Factor;
            Zoom = Zoom.Clamp(ZoomMin, ZoomMax);
            ClampTargetPosition(); // Re-clamp when viewport size changes
        }

        if (@event is InputEventPanGesture panGesture)
        {
            _targetPosition += (panGesture.Delta * TrackpadPanSpeed) / Zoom;
            ClampTargetPosition();
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        float weight = 1.0f - Mathf.Exp(-DragSmoothingSpeed * dt);
        GlobalPosition = GlobalPosition.Lerp(_targetPosition, weight);

        if (GlobalPosition.DistanceSquaredTo(_targetPosition) < 0.01f)
        {
            GlobalPosition = _targetPosition;
        }
    }

    // --- Dynamic Clamping Logic ---

    private void ClampTargetPosition()
    {
        if (!UseBounds) return;

        // 1. Calculate the actual viewable size of the screen adjusted for Zoom
        Vector2 viewportSize = GetViewportRect().Size / Zoom;
        Vector2 halfSize = viewportSize / 2.0f;

        // 2. Shrink the allowable center position bounds based on the screen size
        float minAllowedX = MinBounds.X + halfSize.X;
        float maxAllowedX = MaxBounds.X - halfSize.X;
        float minAllowedY = MinBounds.Y + halfSize.Y;
        float maxAllowedY = MaxBounds.Y - halfSize.Y;

        // 3. Edge Case: If zoomed out so far that the screen is wider/taller than the bounds, 
        // lock the camera to the exact center of the map so it doesn't freak out.
        if (minAllowedX > maxAllowedX)
        {
            _targetPosition.X = (MinBounds.X + MaxBounds.X) / 2.0f;
        }
        else
        {
            _targetPosition.X = Mathf.Clamp(_targetPosition.X, minAllowedX, maxAllowedX);
        }

        if (minAllowedY > maxAllowedY)
        {
            _targetPosition.Y = (MinBounds.Y + MaxBounds.Y) / 2.0f;
        }
        else
        {
            _targetPosition.Y = Mathf.Clamp(_targetPosition.Y, minAllowedY, maxAllowedY);
        }
    }
}