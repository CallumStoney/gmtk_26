using Godot;
using System;

public partial class ButtonHighlight : TextureButton
{
    // Customize these in the Godot Inspector
    [Export] public string HoverText = "Action Description";
    [Export] public double HoverDelay = 0.5; // Delay in seconds
    [Export] public int FontSize = 12; // Easily change font size in Inspector

    private Timer _hoverTimer;
    private Label _tooltipLabel; // The script will create and manage this automatically

    public override void _Ready()
    {
        _hoverTimer = new Timer();
        _hoverTimer.OneShot = true;
        _hoverTimer.WaitTime = HoverDelay;
        _hoverTimer.Timeout += OnHoverTimerTimeout;
        AddChild(_hoverTimer);

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    private void OnMouseEntered()
    {
        _hoverTimer.Start();
    }

    private void OnMouseExited()
    {
        _hoverTimer.Stop();
        RemoveTooltip();
    }

    private void OnHoverTimerTimeout()
    {
        if (_tooltipLabel == null)
        {
            _tooltipLabel = new Label();
            _tooltipLabel.Text = HoverText;
            _tooltipLabel.ZIndex = 4090;

            _tooltipLabel.AddThemeFontSizeOverride("font_size", FontSize);

            StyleBoxFlat styleBox = new StyleBoxFlat();

            styleBox.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);

            styleBox.BorderWidthTop = 1;
            styleBox.BorderWidthBottom = 1;
            styleBox.BorderWidthLeft = 1;
            styleBox.BorderWidthRight = 1;

            styleBox.BorderColor = new Color(1f, 1f, 1f, 1f);

            // --- UPDATED: Shrunk the padding to make the box tighter! ---
            styleBox.ContentMarginLeft = 2;
            styleBox.ContentMarginRight = 2;
            styleBox.ContentMarginTop = 1;
            styleBox.ContentMarginBottom = 1;
            // ------------------------------------------------------------

            _tooltipLabel.AddThemeStyleboxOverride("normal", styleBox);

            AddChild(_tooltipLabel);

            _tooltipLabel.GlobalPosition = GetGlobalMousePosition() + new Vector2(0, 0);
        }
    }

    private void RemoveTooltip()
    {
        if (_tooltipLabel != null)
        {
            _tooltipLabel.QueueFree();
            _tooltipLabel = null;
        }
    }

    public override void _ExitTree()
    {
        RemoveTooltip();
    }
}