using Godot;

[GlobalClass]
public partial class MapSaveState : Resource
{
    [Export]
    public Godot.Collections.Array<BuildingSaveData> PlacedBuildings { get; set; } = new Godot.Collections.Array<BuildingSaveData>();
}