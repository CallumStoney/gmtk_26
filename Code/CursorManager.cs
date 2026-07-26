using Godot;

public partial class CursorManager : CanvasLayer
{
    [Export] public Texture2D DefaultCursor;
    [Export] public Texture2D DragCursor;

    [Export] public Vector2 DefaultHotspot = Vector2.Zero;
    [Export] public Vector2 DragHotspot = new Vector2(16, 16);

    [Export] public float CursorScale = 2.0f;

    private Sprite2D _cursorSprite;

    public override void _Ready()
    {
        // Hide standard hardware mouse
        Input.MouseMode = Input.MouseModeEnum.Hidden;

        // Create software cursor sprite
        _cursorSprite = new Sprite2D
        {
            Centered = false
        };
        AddChild(_cursorSprite);

        SetDefaultCursor();
    }

    public override void _Process(double delta)
    {
        if (_cursorSprite != null && _cursorSprite.Visible)
        {
            _cursorSprite.GlobalPosition = GetViewport().GetMousePosition();
        }
    }

    public void SetDefaultCursor()
    {
        ApplyCursor(DefaultCursor, DefaultHotspot, "Default");
    }

    public void SetDragCursor()
    {
        ApplyCursor(DragCursor, DragHotspot, "Drag");
    }

    public void ResetToHardwareCursor()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        if (_cursorSprite != null) _cursorSprite.Visible = false;
    }

    private void ApplyCursor(Texture2D texture, Vector2 hotspot, string cursorType)
    {
        if (_cursorSprite == null) return;

        if (texture == null)
        {
            GD.PrintErr($"[CursorManager] Missing texture! Set '{cursorType}Cursor' in the CursorManager Inspector.");
            return;
        }

        _cursorSprite.Visible = true;
        _cursorSprite.Texture = texture;
        _cursorSprite.Scale = new Vector2(CursorScale, CursorScale);
        _cursorSprite.Offset = -hotspot;
    }
}