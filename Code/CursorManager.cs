using Godot;

public partial class CursorManager : Node
{
    [Export] public Texture2D DefaultCursor;
    [Export] public Texture2D DragCursor;

    [Export] public Vector2 DefaultHotspot = Vector2.Zero;
    [Export] public Vector2 DragHotspot = new Vector2(16, 16);

    // Gives you a slider in the Inspector!
    [Export] public float CursorScale = 2.0f;

    public override void _Ready()
    {
        // 1. Scale the textures before giving them to the OS
        Texture2D scaledDefault = ScaleTexture(DefaultCursor, CursorScale);
        Texture2D scaledDrag = ScaleTexture(DragCursor, CursorScale);

        // 2. We must also scale the hotspots so the click-point stays accurate!
        Vector2 scaledDefaultHotspot = DefaultHotspot * CursorScale;
        Vector2 scaledDragHotspot = DragHotspot * CursorScale;

        if (scaledDefault != null)
        {
            Input.SetCustomMouseCursor(scaledDefault, Input.CursorShape.Arrow, scaledDefaultHotspot);
        }

        if (scaledDrag != null)
        {
            Input.SetCustomMouseCursor(scaledDrag, Input.CursorShape.Drag, scaledDragHotspot);
        }

        SetDefaultCursor();
    }

    private Texture2D ScaleTexture(Texture2D tex, float scale)
    {
        if (tex == null) return null;

        // Extract the raw image data
        Image img = tex.GetImage();

        int newWidth = Mathf.RoundToInt(img.GetWidth() * scale);
        int newHeight = Mathf.RoundToInt(img.GetHeight() * scale);

        // Resize the image. 
        // NOTE: If you are making a pixel art game, use Image.Interpolation.Nearest. 
        // If it's a smooth HD graphic, change this to Image.Interpolation.Bilinear.
        img.Resize(newWidth, newHeight, Image.Interpolation.Nearest);

        // Package it back into a texture for the OS
        return ImageTexture.CreateFromImage(img);
    }

    public void SetDefaultCursor()
    {
        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
    }

    public void SetDragCursor()
    {
        Input.SetDefaultCursorShape(Input.CursorShape.Drag);
    }
}