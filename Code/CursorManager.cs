using Godot;

public partial class CursorManager : Node
{
    [Export] public Texture2D DefaultCursor;
    [Export] public Texture2D DragCursor;
    [Export] public Texture2D RoomCursor;

    [Export] public Vector2 DefaultHotspot = Vector2.Zero;
    [Export] public Vector2 DragHotspot = new Vector2(16, 16);
    [Export] public Vector2 RoomHotspot = Vector2.Zero;

    [Export] public float CursorScale = 2.0f;

    private Texture2D _scaledDefault;
    private Texture2D _scaledDrag;
    private Texture2D _scaledRoom;

    private Vector2 _scaledDefaultHotspot;
    private Vector2 _scaledDragHotspot;
    private Vector2 _scaledRoomHotspot;

    private string _lastScenePath = "";

    public override void _Ready()
    {
        _scaledDefault = ScaleTexture(DefaultCursor, CursorScale);
        _scaledDrag = ScaleTexture(DragCursor, CursorScale);
        _scaledRoom = ScaleTexture(RoomCursor, CursorScale);

        _scaledDefaultHotspot = DefaultHotspot * CursorScale;
        _scaledDragHotspot = DragHotspot * CursorScale;
        _scaledRoomHotspot = RoomHotspot * CursorScale;

        // The drag cursor never changes, so we just set it once here
        if (_scaledDrag != null)
        {
            Input.SetCustomMouseCursor(_scaledDrag, Input.CursorShape.Drag, _scaledDragHotspot);
        }
    }

    public override void _Process(double delta)
    {
        Node currentScene = GetTree().CurrentScene;

        if (currentScene == null) return;

        string currentSceneName = currentScene.Name;

        if (currentSceneName != _lastScenePath)
        {
            // THIS WILL PRINT EVERY TIME GODOT DETECTS A SCENE CHANGE
            GD.Print($"[CursorManager] Scene swapped! Old: {_lastScenePath} | New: {currentSceneName}");

            _lastScenePath = currentSceneName;
            SetDefaultCursor();
        }
    }

    public void SetDefaultCursor()
    {
        GD.Print($"[CursorManager] Updating cursor for scene: {_lastScenePath}");

        if (_lastScenePath.Contains("RoomScene"))
        {
            GD.Print("[CursorManager] Applied ROOM graphic.");
            EnableRoomCursor();
        }
        else
        {
            GD.Print("[CursorManager] Applied STANDARD graphic.");
            EnableStandardCursor();
        }
    }

    private void EnableRoomCursor()
    {
        if (_scaledRoom != null)
            Input.SetCustomMouseCursor(_scaledRoom, Input.CursorShape.Arrow, _scaledRoomHotspot);

        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
    }

    private void EnableStandardCursor()
    {
        if (_scaledDefault != null)
            Input.SetCustomMouseCursor(_scaledDefault, Input.CursorShape.Arrow, _scaledDefaultHotspot);

        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
    }

    public void SetDragCursor()
    {
        Input.SetDefaultCursorShape(Input.CursorShape.Drag);
    }

    private Texture2D ScaleTexture(Texture2D tex, float scale)
    {
        if (tex == null) return null;

        Image img = tex.GetImage();
        int newWidth = Mathf.RoundToInt(img.GetWidth() * scale);
        int newHeight = Mathf.RoundToInt(img.GetHeight() * scale);

        img.Resize(newWidth, newHeight, Image.Interpolation.Nearest);
        return ImageTexture.CreateFromImage(img);
    }

}