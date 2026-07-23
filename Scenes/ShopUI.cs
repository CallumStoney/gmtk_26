using Godot;

public partial class ShopUI : Control
{
    [Export] public TextureButton ToggleButton;
    [Export] public Control ShopPopup;

    public override void _Ready()
    {
        // Ensure the popup is hidden when the game starts
        ShopPopup.Visible = false;

        // Connect the button
        if (ToggleButton != null)
        {
            ToggleButton.Pressed += OnToggleButtonPressed;
        }
    }

    private void OnToggleButtonPressed()
    {
        // Flips the visibility back and forth
        ShopPopup.Visible = !ShopPopup.Visible;
    }
}