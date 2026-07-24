using Godot;

public partial class CursorManager : Node
{
    [Export] public Texture2D DefaultCursor;
    [Export] public Texture2D DragCursor;

    // Hotspot is the pixel offset where clicks actually register
    [Export] public Vector2 DefaultHotspot = Vector2.Zero;
    [Export] public Vector2 DragHotspot = new Vector2(16, 16);

    // --- NEW: Global Scale Multiplier ---
    [Export] public float CursorScale = 2.0f; // 2.0 = 200% size, 1.5 = 150% size, etc.

    public override void _Ready()
    {
        SetDefaultCursor();
    }

    public void SetDefaultCursor()
    {
        SetScaledCursor(DefaultCursor, DefaultHotspot);
    }

    public void SetDragCursor()
    {
        SetScaledCursor(DragCursor, DragHotspot);
    }

    public void ResetToHardwareCursor()
    {
        Input.SetCustomMouseCursor(null);
    }

    // Helper method to resize the texture and scale the hotspot automatically
    private void SetScaledCursor(Texture2D texture, Vector2 hotspot)
    {
        if (texture == null) return;

        Texture2D finalTexture = texture;
        Vector2 finalHotspot = hotspot;

        // Only process resizing if scale is different from 1.0
        if (!Mathf.IsEqualApprox(CursorScale, 1.0f) && CursorScale > 0)
        {
            // 1. Get image data from texture
            Image image = texture.GetImage();

            // 2. Calculate new size
            int newWidth = Mathf.RoundToInt(image.GetWidth() * CursorScale);
            int newHeight = Mathf.RoundToInt(image.GetHeight() * CursorScale);

            // 3. Resize image (Use Interpolation.Nearest for pixel art, or Interpolation.Bilinear for smooth graphics)
            image.Resize(newWidth, newHeight, Image.Interpolation.Nearest);

            // 4. Create an ImageTexture from the resized image
            finalTexture = ImageTexture.CreateFromImage(image);

            // 5. Scale the hotspot coordinate proportionally!
            finalHotspot = hotspot * CursorScale;
        }

        Input.SetCustomMouseCursor(finalTexture, Input.CursorShape.Arrow, finalHotspot);
    }
}