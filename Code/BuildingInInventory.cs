using Godot;
using System;

public partial class BuildingInInventory : Control
{
	// Reference to your BuildingManager instance
	[Export] public BuildingManager Manager;

	// The index of the building in BuildingManager's arrays (0, 1, 2, etc.)
	[Export] public int BuildingIndex = 0;

	public override void _Ready()
	{
		// Fallback: If not set in the Inspector, try finding it in the scene tree
		if (Manager == null)
		{
			Manager = GetTree().Root.FindChild("BuildingManager", true, false) as BuildingManager;
		}
	}

	public override void _Process(double delta)
	{
		UpdateVisibility();
	}

	/// <summary>
	/// Checks stock from the BuildingManager and updates node visibility.
	/// </summary>
	public void UpdateVisibility()
	{
		if (Manager == null) return;

		// Sets Visible to true if stock > 0, false if stock <= 0
		Visible = Manager.HasAvailableBuilding(BuildingIndex);
	}
}
