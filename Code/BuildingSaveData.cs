using Godot;

[GlobalClass]
public partial class BuildingSaveData : Resource
{
    [Export] public int BuildingTypeIndex { get; set; } = 0;
    [Export] public Vector2I GridPosition { get; set; } = Vector2I.Zero;
    [Export] public int RotationIndex { get; set; } = 0;
    [Export] public int CurrentLevel { get; set; } = 0;
}