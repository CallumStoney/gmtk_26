using Godot;
using System;
using System.Collections.Generic;

public partial class TrafficManager : Node2D
{
	[Export] public TileMap RoadTileMap; // Changed to TileMap for standard setup
	[Export] public int InitialCarCount = 5;
	[Export] public float CarSpeed = 100.0f;

	private AStar2D roadGraph = new AStar2D();
	private List<Vector2I> roadPositions = new List<Vector2I>();
	private Dictionary<Vector2I, long> tileToIdMap = new Dictionary<Vector2I, long>();
	private List<SimpleCar> activeCars = new List<SimpleCar>();
	private Random rng = new Random();

	private class SimpleCar
	{
		public Vector2 CurrentPosition;
		public Queue<Vector2> PathQueue = new Queue<Vector2>();
		public Color CarColor;
	}

	public override void _Ready()
	{
		if (RoadTileMap == null)
		{
			GD.PrintErr("CRITICAL ERROR: 'RoadTileMap' is NOT assigned in the TrafficManager Inspector!");
			return;
		}

		BuildRoadNetwork();

		if (roadPositions.Count == 0)
		{
			GD.PrintErr("WARNING: RoadTileMap has no tiles placed or script couldn't read cells!");
			return;
		}

		for (int i = 0; i < InitialCarCount; i++)
		{
			SpawnCarAtRandomRoad();
		}
	}

	private void BuildRoadNetwork()
	{
		// Gets used cells on Layer 0
		var usedCells = RoadTileMap.GetUsedCells(0); 
		long idCounter = 0;

		foreach (Vector2I cellPos in usedCells)
		{
			roadPositions.Add(cellPos);
			tileToIdMap[cellPos] = idCounter;

			Vector2 worldPos = RoadTileMap.ToGlobal(RoadTileMap.MapToLocal(cellPos));
			roadGraph.AddPoint(idCounter, worldPos);

			idCounter++;
		}

		Vector2I[] directions = new Vector2I[] 
		{ 
			Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right 
		};

		foreach (Vector2I cellPos in roadPositions)
		{
			long currentId = tileToIdMap[cellPos];

			foreach (Vector2I dir in directions)
			{
				Vector2I neighborPos = cellPos + dir;
				if (tileToIdMap.ContainsKey(neighborPos))
				{
					long neighborId = tileToIdMap[neighborPos];
					if (!roadGraph.ArePointsConnected(currentId, neighborId))
					{
						roadGraph.ConnectPoints(currentId, neighborId, true);
					}
				}
			}
		}

		GD.Print($"TrafficManager successfully mapped {roadPositions.Count} road tiles!");
	}

	private void SpawnCarAtRandomRoad()
	{
		if (roadPositions.Count < 2) return;

		Vector2I startCell = roadPositions[rng.Next(roadPositions.Count)];
		Vector2 startPos = RoadTileMap.ToGlobal(RoadTileMap.MapToLocal(startCell));

		SimpleCar car = new SimpleCar
		{
			CurrentPosition = startPos,
			CarColor = new Color((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble())
		};

		AssignRandomDestination(car, startCell);
		activeCars.Add(car);
	}

	private void AssignRandomDestination(SimpleCar car, Vector2I startCell)
	{
		Vector2I endCell = roadPositions[rng.Next(roadPositions.Count)];
		long startId = tileToIdMap[startCell];
		long endId = tileToIdMap[endCell];

		Vector2[] waypoints = roadGraph.GetPointPath(startId, endId);
		car.PathQueue.Clear();

		for (int i = 1; i < waypoints.Length; i++)
		{
			car.PathQueue.Enqueue(waypoints[i]);
		}
	}

	public override void _Process(double delta)
	{
		float step = CarSpeed * (float)delta;

		foreach (var car in activeCars)
		{
			if (car.PathQueue.Count > 0)
			{
				Vector2 target = car.PathQueue.Peek();
				car.CurrentPosition = car.CurrentPosition.MoveToward(target, step);

				if (car.CurrentPosition.DistanceTo(target) < 2.0f)
				{
					car.PathQueue.Dequeue();
				}
			}
			else
			{
				Vector2I currentCell = RoadTileMap.LocalToMap(RoadTileMap.ToLocal(car.CurrentPosition));
				if (tileToIdMap.ContainsKey(currentCell))
				{
					AssignRandomDestination(car, currentCell);
				}
			}
		}

		QueueRedraw();
	}

	public override void _Draw()
	{
		foreach (var car in activeCars)
		{
			// Convert car's global position to TrafficManager's local draw space
			Vector2 localPos = ToLocal(car.CurrentPosition);
			DrawCircle(localPos, 8.0f, car.CarColor);
			DrawArc(localPos, 8.0f, 0, Mathf.Tau, 12, Colors.White, 2.0f);
		}
	}
}
