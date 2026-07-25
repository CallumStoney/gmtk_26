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

	// --- NEW: Cursor Manager Reference ---
	[Export] public CursorManager CursorMgr;

	private bool _isDragging = false;
	private Vector2 _targetPosition;

	public override void _EnterTree()
	{
		// Forces this camera to take over the screen again every time the scene loads
		MakeCurrent();
	}

	public override void _Ready()
	{
		Zoom = Zoom.Clamp(ZoomMin, ZoomMax);
		_targetPosition = Position;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// 1. Standard Mouse Interactions
		if (@event is InputEventMouseButton mouseButton)
		{
			// Ignore zoom wheel if the cursor is hovering over ANY UI control node
			bool isHoveringUI = GetViewport().GuiGetHoveredControl() != null;

			if (mouseButton.ButtonIndex == MouseButton.WheelUp && mouseButton.Pressed)
			{
				if (isHoveringUI) return; // Prevent zooming over UI

				Zoom += ZoomSpeed;
				Zoom = Zoom.Clamp(ZoomMin, ZoomMax);
			}
			else if (mouseButton.ButtonIndex == MouseButton.WheelDown && mouseButton.Pressed)
			{
				if (isHoveringUI) return; // Prevent zooming over UI

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
					_targetPosition = Position;

					// --- NEW: Switch to Drag Cursor on press ---
					CursorMgr?.SetDragCursor();
				}
				else
				{
					// --- NEW: Switch back to Default Cursor on release ---
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
		Position = Position.Lerp(_targetPosition, (float)delta * DragSmoothingSpeed);
	}
}
