using Godot;
using System;
using System.Collections.Generic;

public partial class TrafficManager : Node2D
{
	[ExportGroup("Traffic Setup")]
	[Export] public int InitialCarCount = 8;
	[Export] public float BaseCarSpeed = 22.0f;          
	[Export] public float CarRadius = 3.5f;
	[Export] public float LaneOffsetDistance = 2.8f; // Halved to prevent tight inner loops!

	[ExportGroup("Collision Avoidance & Rules")]
	[Export] public float SafeFollowingDistance = 18.0f; 
	[Export] public float EmergencyStopDistance = 8.0f;  
	[Export] public float BrakingDeceleration = 65.0f;   

	[ExportGroup("Sprite Setup")]
	[Export] public Texture2D CarSpriteSheet;
	[Export] public int SpriteColumns = 2; 
	[Export] public int SpriteRows = 2;    
	[Export] public float SpriteScale = 1.0f;
	[Export(PropertyHint.None, "Turn ON if your sprite sheet is plain grayscale/white. Keep OFF if artist colored!")] 
	public bool TintSpritesWithCarColor = false;

	[ExportGroup("Debug Toggles")]
	[Export] public bool DebugDrawBlockedWalls = false;
	[Export] public bool DebugDrawIntersections = false;

	[ExportGroup("Algorithm Tuning")]
	[Export] public float ShortEdgeOutlierRatio = 0.75f;
	[Export] public float DirectionToleranceDegrees = 40.0f;

	private Node2D roadTileMapNode;
	private Dictionary<Vector2I, Vector2> tileLocalPositions = new Dictionary<Vector2I, Vector2>();
	private Dictionary<Vector2I, List<Vector2I>> strictRoadGraph = new Dictionary<Vector2I, List<Vector2I>>();
	private Dictionary<Vector2I, List<Vector2I>> validRoadGraph = new Dictionary<Vector2I, List<Vector2I>>();

	private HashSet<Vector2I> intersectionTiles = new HashSet<Vector2I>();
	private List<Vector2I> allRoadTiles = new List<Vector2I>();
	private List<SimpleCar> activeCars = new List<SimpleCar>();
	private Random rng = new Random();

	private List<(Vector2I A, Vector2I B)> debugBlockedEdges = new List<(Vector2I, Vector2I)>();
	private float lastMedianEdgeLength = 0f;
	private static int carIdCounter = 0;

	private class SimpleCar
	{
		public int ID;
		public Vector2 CurrentPosition;
		public Vector2I CurrentTile;
		public Vector2I LastTile;
		public Vector2I NextTile;
		public Color CarColor;
		public Queue<Vector2I> RecentTiles = new Queue<Vector2I>();
		public bool IsStopped = false;

		// Unique Driver Personality Profiles
		public float TopSpeed = 20.0f;
		public float AccelerationRate = 30.0f;
		public float TurnSpeedMultiplier = 0.4f;

		// Real-time Movement Data
		public float CurveLength = 1.0f;
		public float Progress = 0.0f;
		public float CurrentSpeed = 20.0f;
		public bool IsTurning = false;

		// Rendering vectors
		public Vector2 ForwardDir = Vector2.Right;
		public Vector2 RightDir = Vector2.Down;
	}

	public override void _Ready()
	{
		roadTileMapNode = GetNodeOrNull<Node2D>("../TileSets/RoadTileMap");
		if (roadTileMapNode == null)
		{
			roadTileMapNode = GetTree().CurrentScene.FindChild("RoadTileMap", recursive: true, owned: false) as Node2D;
		}

		if (roadTileMapNode == null)
		{
			GD.PrintErr("CRITICAL: TrafficManager could not find 'RoadTileMap'!");
			return;
		}

		BuildMapAndIdentifyIntersections();

		for (int i = 0; i < InitialCarCount; i++)
		{
			SpawnCarAtRandomRoad();
		}
	}

	private Vector2 GetTileLocalPos(Vector2I cellPos)
	{
		if (!tileLocalPositions.TryGetValue(cellPos, out Vector2 mapLocalPos))
			return Vector2.Zero;

		if (roadTileMapNode != null)
		{
			return ToLocal(roadTileMapNode.ToGlobal(mapLocalPos));
		}

		return mapLocalPos;
	}

	private Vector2 GetMidpoint(Vector2I tileA, Vector2I tileB)
	{
		return (GetTileLocalPos(tileA) + GetTileLocalPos(tileB)) * 0.5f;
	}

	private void BuildMapAndIdentifyIntersections()
	{
		Godot.Collections.Array<Vector2I> usedCells = new Godot.Collections.Array<Vector2I>();

		if (roadTileMapNode is TileMapLayer tileMapLayer)
		{
			usedCells = tileMapLayer.GetUsedCells();
			foreach (Vector2I cellPos in usedCells)
			{
				tileLocalPositions[cellPos] = tileMapLayer.MapToLocal(cellPos);
				strictRoadGraph[cellPos] = new List<Vector2I>();
				allRoadTiles.Add(cellPos);
			}

			TileSet.CellNeighbor[] isoAxes = {
				TileSet.CellNeighbor.TopRightSide,
				TileSet.CellNeighbor.BottomRightSide,
				TileSet.CellNeighbor.BottomLeftSide,
				TileSet.CellNeighbor.TopLeftSide
			};

			foreach (Vector2I cell in allRoadTiles)
			{
				foreach (var dir in isoAxes)
				{
					Vector2I neighbor = tileMapLayer.GetNeighborCell(cell, dir);
					if (tileLocalPositions.ContainsKey(neighbor))
					{
						strictRoadGraph[cell].Add(neighbor);
					}
				}
			}
		}
		else if (roadTileMapNode is TileMap tileMap)
		{
			usedCells = tileMap.GetUsedCells(0);
			foreach (Vector2I cellPos in usedCells)
			{
				tileLocalPositions[cellPos] = tileMap.MapToLocal(cellPos);
				strictRoadGraph[cellPos] = new List<Vector2I>();
				allRoadTiles.Add(cellPos);
			}

			TileSet.CellNeighbor[] isoAxes = {
				TileSet.CellNeighbor.TopRightSide,
				TileSet.CellNeighbor.BottomRightSide,
				TileSet.CellNeighbor.BottomLeftSide,
				TileSet.CellNeighbor.TopLeftSide
			};

			foreach (Vector2I cell in allRoadTiles)
			{
				foreach (var dir in isoAxes)
				{
					Vector2I neighbor = tileMap.GetNeighborCell(cell, dir);
					if (tileLocalPositions.ContainsKey(neighbor))
					{
						strictRoadGraph[cell].Add(neighbor);
					}
				}
			}
		}

		FilterParallelWallConnections();
		ApplyManualOverrides();

		intersectionTiles.Clear();
		foreach (Vector2I cell in allRoadTiles)
		{
			if (validRoadGraph[cell].Count >= 3)
			{
				intersectionTiles.Add(cell);
			}
		}

		GD.Print($"Map Built: {allRoadTiles.Count} road tiles, {intersectionTiles.Count} real intersections, {debugBlockedEdges.Count} walls blocked.");
	}

	private void ApplyManualOverrides()
	{
		ManualDisconnect(new Vector2I(0, 8), new Vector2I(-1, 9));
		ManualDisconnect(new Vector2I(-1, 6), new Vector2I(-2, 7));
		ManualDisconnect(new Vector2I(4, 1), new Vector2I(4, 0));
		ManualDisconnect(new Vector2I(4, 7), new Vector2I(5, 6));
		ManualDisconnect(new Vector2I(6, 12), new Vector2I(6, 11));
		ManualDisconnect(new Vector2I(1, 13), new Vector2I(2, 12));
		ManualDisconnect(new Vector2I(4, -3), new Vector2I(5, -2));
	}

	private void ManualDisconnect(Vector2I tileA, Vector2I tileB)
	{
		bool cutMade = false;

		if (validRoadGraph.ContainsKey(tileA) && validRoadGraph[tileA].Contains(tileB))
		{
			validRoadGraph[tileA].Remove(tileB);
			cutMade = true;
		}

		if (validRoadGraph.ContainsKey(tileB) && validRoadGraph[tileB].Contains(tileA))
		{
			validRoadGraph[tileB].Remove(tileA);
			cutMade = true;
		}

		if (cutMade && tileLocalPositions.ContainsKey(tileA) && tileLocalPositions.ContainsKey(tileB))
		{
			debugBlockedEdges.Add((tileA, tileB));
		}
	}

	private bool ContinuesInDirection(Vector2I tile, Vector2I excludeNeighbor, Vector2 direction)
	{
		foreach (Vector2I n in strictRoadGraph[tile])
		{
			if (n == excludeNeighbor) continue;
			Vector2 d = (tileLocalPositions[n] - tileLocalPositions[tile]).Normalized();
			float angleDeg = Mathf.RadToDeg(Mathf.Abs(d.AngleTo(direction)));
			if (angleDeg <= DirectionToleranceDegrees) return true;
		}
		return false;
	}

	private void FilterParallelWallConnections()
	{
		foreach (Vector2I tile in allRoadTiles)
		{
			validRoadGraph[tile] = new List<Vector2I>();
		}

		List<(Vector2I A, Vector2I B, float Length)> rawEdges = new List<(Vector2I, Vector2I, float)>();
		HashSet<(Vector2I, Vector2I)> seen = new HashSet<(Vector2I, Vector2I)>();

		foreach (Vector2I a in allRoadTiles)
		{
			foreach (Vector2I b in strictRoadGraph[a])
			{
				if (seen.Contains((a, b)) || seen.Contains((b, a))) continue;
				seen.Add((a, b));
				float len = tileLocalPositions[a].DistanceTo(tileLocalPositions[b]);
				rawEdges.Add((a, b, len));
			}
		}

		if (rawEdges.Count == 0) return;

		List<float> lengths = rawEdges.ConvertAll(e => e.Length);
		lengths.Sort();
		float medianLength = lengths[lengths.Count / 2];
		lastMedianEdgeLength = medianLength;
		float shortOutlierThreshold = medianLength * ShortEdgeOutlierRatio;

		foreach (var edge in rawEdges)
		{
			Vector2I a = edge.A;
			Vector2I b = edge.B;

			Vector2 dirAB = (tileLocalPositions[b] - tileLocalPositions[a]).Normalized();
			Vector2 dirBA = -dirAB;

			bool isShortOutlier = edge.Length < shortOutlierThreshold;
			bool bContinues = ContinuesInDirection(b, a, dirAB);
			bool aContinues = ContinuesInDirection(a, b, dirBA);

			bool isReal = !isShortOutlier && (aContinues || bContinues);

			if (isReal)
			{
				validRoadGraph[a].Add(b);
				validRoadGraph[b].Add(a);
			}
			else
			{
				debugBlockedEdges.Add((a, b));
			}
		}
	}

	/// <summary>
	/// Calculates curve control points shifted into the right-hand lane.
	/// Includes automatic clamping to prevent Bezier self-intersections (loop-de-loops)!
	/// </summary>
	private void GetLaneControlPoints(SimpleCar car, out Vector2 start, out Vector2 control, out Vector2 end)
	{
		Vector2 rawStart = GetMidpoint(car.LastTile, car.CurrentTile);
		Vector2 rawControl = GetTileLocalPos(car.CurrentTile);
		Vector2 rawEnd = GetMidpoint(car.CurrentTile, car.NextTile);

		Vector2 dirIn = (rawControl - rawStart);
		dirIn = (dirIn.LengthSquared() > 0.0001f) ? dirIn.Normalized() : car.ForwardDir;

		Vector2 dirOut = (rawEnd - rawControl);
		dirOut = (dirOut.LengthSquared() > 0.0001f) ? dirOut.Normalized() : dirIn;

		// In Godot 2D (+X Right, +Y Down), 90 deg perpendicular to the right is (-Y, X)
		Vector2 rightIn = new Vector2(-dirIn.Y, dirIn.X);
		Vector2 rightOut = new Vector2(-dirOut.Y, dirOut.X);
		
		Vector2 rightControl = (rightIn + rightOut);
		rightControl = (rightControl.LengthSquared() > 0.0001f) ? rightControl.Normalized() : rightIn;

		// SAFETY CLAMP: Never offset more than 35% of the tile chord length!
		// This mathematically prevents inner turns from crossing over and spinning 360 degrees!
		float chordDist = rawStart.DistanceTo(rawEnd);
		float safeOffset = Mathf.Min(LaneOffsetDistance, chordDist * 0.35f);

		start = rawStart + rightIn * safeOffset;
		end = rawEnd + rightOut * safeOffset;
		control = rawControl + rightControl * safeOffset;
	}

	private void UpdateSegmentMetrics(SimpleCar car)
	{
		GetLaneControlPoints(car, out Vector2 start, out Vector2 control, out Vector2 end);

		float d1 = start.DistanceTo(control);
		float d2 = control.DistanceTo(end);
		float d3 = start.DistanceTo(end);
		
		car.CurveLength = Mathf.Max((d1 + d2 + d3) * 0.5f, 0.1f);

		Vector2 dirIn = (control - start).Normalized();
		Vector2 dirOut = (end - control).Normalized();
		float angleRad = Mathf.Abs(dirIn.AngleTo(dirOut));
		car.IsTurning = angleRad > Mathf.DegToRad(15.0f);
	}

	private void SpawnCarAtRandomRoad()
	{
		if (allRoadTiles.Count < 2) return;

		Vector2I startTile = allRoadTiles[rng.Next(allRoadTiles.Count)];

		// TONED DOWN AUTOMOTIVE PALETTE (Desaturated, matte, realistic tones)
		Color[] realisticCarColors = { 
			new Color(0.92f, 0.92f, 0.90f), // Pearl White
			new Color(0.70f, 0.72f, 0.75f), // Steel Silver
			new Color(0.25f, 0.27f, 0.30f), // Charcoal Gray
			new Color(0.15f, 0.15f, 0.18f), // Pitch Black
			new Color(0.65f, 0.20f, 0.22f), // Muted Burgundy Red
			new Color(0.25f, 0.38f, 0.55f), // Slate Navy Blue
			new Color(0.22f, 0.40f, 0.30f), // Hunter Forest Green
			new Color(0.78f, 0.72f, 0.62f)  // Warm Sand / Beige
		};

		float topSpeed = BaseCarSpeed * (float)(0.75 + rng.NextDouble() * 0.6);
		float acceleration = (float)(25.0 + rng.NextDouble() * 35.0);
		float turnMultiplier = (float)(0.35 + rng.NextDouble() * 0.1);

		SimpleCar car = new SimpleCar
		{
			ID = carIdCounter++,
			CurrentTile = startTile,
			LastTile = startTile,
			NextTile = startTile,
			TopSpeed = topSpeed,
			CurrentSpeed = topSpeed,
			AccelerationRate = acceleration,
			TurnSpeedMultiplier = turnMultiplier,
			CarColor = realisticCarColors[rng.Next(realisticCarColors.Length)]
		};

		car.RecentTiles.Enqueue(startTile);

		Vector2I firstDest = ChooseNextTile(car, startTile, startTile);
		car.NextTile = firstDest;

		UpdateSegmentMetrics(car);
		activeCars.Add(car);
	}

	private Vector2I ChooseNextTile(SimpleCar car, Vector2I currentTile, Vector2I lastTile)
	{
		if (!validRoadGraph.ContainsKey(currentTile) || validRoadGraph[currentTile].Count == 0)
		{
			car.IsStopped = true;
			return currentTile;
		}

		List<Vector2I> connections = validRoadGraph[currentTile];
		Vector2I chosenTile = currentTile;
		bool isIntersection = intersectionTiles.Contains(currentTile);

		if (!isIntersection)
		{
			Vector2I forwardTile = currentTile;
			foreach (Vector2I n in connections)
			{
				if (n != lastTile)
				{
					forwardTile = n;
					break;
				}
			}
			chosenTile = (forwardTile != currentTile) ? forwardTile : lastTile;
		}
		else
		{
			List<Vector2I> validBranches = new List<Vector2I>();
			foreach (Vector2I n in connections)
			{
				if (n != lastTile && !car.RecentTiles.Contains(n))
				{
					validBranches.Add(n);
				}
			}

			if (validBranches.Count > 0)
			{
				chosenTile = validBranches[rng.Next(validBranches.Count)];
			}
			else
			{
				foreach (Vector2I n in connections)
				{
					if (n != lastTile)
					{
						validBranches.Add(n);
					}
				}
				chosenTile = (validBranches.Count > 0) ? validBranches[rng.Next(validBranches.Count)] : lastTile;
			}
		}

		car.RecentTiles.Enqueue(chosenTile);
		if (car.RecentTiles.Count > 8)
		{
			car.RecentTiles.Dequeue();
		}

		return chosenTile;
	}

	/// <summary>
	/// TRAFFIC RULES & RIGHT-OF-WAY PRIORITY:
	/// Prevents intersection standoffs and deadlocks!
	/// </summary>
	private bool HasRightOfWay(SimpleCar me, SimpleCar other)
	{
		// Rule 1: "Up has priority" (In Godot 2D, -Y is Up, so smaller Y means heading more upwards!)
		if (Mathf.Abs(me.ForwardDir.Y - other.ForwardDir.Y) > 0.15f)
		{
			return me.ForwardDir.Y < other.ForwardDir.Y;
		}

		// Rule 2: If vertical direction is tied, "Right has priority" (+X is Right)
		if (Mathf.Abs(me.ForwardDir.X - other.ForwardDir.X) > 0.15f)
		{
			return me.ForwardDir.X > other.ForwardDir.X;
		}

		// Rule 3: Tie-breaker using unique ID numbers so deadlocks are mathematically impossible!
		return me.ID > other.ID;
	}

	public override void _Process(double delta)
	{
		foreach (var car in activeCars)
		{
			if (car.IsStopped) continue;

			float targetSpeed = car.IsTurning ? (car.TopSpeed * car.TurnSpeedMultiplier) : car.TopSpeed;
			bool isBrakingForCar = false;
			float speedLimitFromCarAhead = targetSpeed;

			foreach (var otherCar in activeCars)
			{
				if (otherCar == car) continue;

				Vector2 toOther = otherCar.CurrentPosition - car.CurrentPosition;
				float dist = toOther.Length();

				if (dist < SafeFollowingDistance)
				{
					// If the other car is strictly within our forward vision cone...
					if (car.ForwardDir.Dot(toOther.Normalized()) > 0.65f)
					{
						// Check if we are traveling roughly the same direction (Trailing / Rear-end danger)
						bool isSameDirection = car.ForwardDir.Dot(otherCar.ForwardDir) > 0.4f;

						if (isSameDirection)
						{
							// Standard Trailing: ALWAYS brake/yield to prevent rear-ending!
							isBrakingForCar = true;
							if (dist < EmergencyStopDistance)
							{
								speedLimitFromCarAhead = 0.0f;
							}
							else
							{
								float factor = (dist - EmergencyStopDistance) / (SafeFollowingDistance - EmergencyStopDistance);
								speedLimitFromCarAhead = Mathf.Min(speedLimitFromCarAhead, otherCar.CurrentSpeed * factor);
							}
						}
						else
						{
							// CROSSING / HEAD-ON CONFLICT (Intersections):
							// Apply Right-of-Way Rules! If I don't have priority, I yield and stop!
							if (!HasRightOfWay(car, otherCar))
							{
								isBrakingForCar = true;
								speedLimitFromCarAhead = 0.0f; 
							}
						}
					}
				}
			}

			if (isBrakingForCar)
			{
				targetSpeed = Mathf.Min(targetSpeed, speedLimitFromCarAhead);
			}

			float accelRate = (targetSpeed < car.CurrentSpeed) ? BrakingDeceleration : car.AccelerationRate;
			car.CurrentSpeed = Mathf.MoveToward(car.CurrentSpeed, targetSpeed, accelRate * (float)delta);

			float deltaProgress = (car.CurrentSpeed * (float)delta) / car.CurveLength;
			car.Progress += deltaProgress;

			int safetyCounter = 0;
			while (car.Progress >= 1.0f && safetyCounter < 5)
			{
				safetyCounter++;
				float leftoverDist = (car.Progress - 1.0f) * car.CurveLength;

				car.LastTile = car.CurrentTile;
				car.CurrentTile = car.NextTile;
				car.NextTile = ChooseNextTile(car, car.CurrentTile, car.LastTile);

				if (car.IsStopped) break;

				UpdateSegmentMetrics(car);
				car.Progress = leftoverDist / car.CurveLength;
			}

			if (!car.IsStopped)
			{
				GetLaneControlPoints(car, out Vector2 start, out Vector2 control, out Vector2 end);

				float t = Mathf.Clamp(car.Progress, 0.0f, 1.0f);
				float u = 1.0f - t;
				car.CurrentPosition = (u * u) * start + (2.0f * u * t) * control + (t * t) * end;

				Vector2 tangent = (1.0f - t) * (control - start) + t * (end - control);
				if (tangent.LengthSquared() < 0.0001f) tangent = end - start;
				
				if (tangent.LengthSquared() > 0.0001f)
				{
					car.ForwardDir = tangent.Normalized();
					car.RightDir = new Vector2(-car.ForwardDir.Y, car.ForwardDir.X);
				}
			}
		}

		QueueRedraw();
	}

	private (int frameIndex, float visualRotationRad) GetSpriteFrameAndRotation(Vector2 dir)
	{
		float angleDeg = Mathf.RadToDeg(Mathf.Atan2(dir.Y, dir.X));
		if (angleDeg < 0) angleDeg += 360.0f;

		int frame = 0;
		float baseAngleDeg = 0f;

		if (angleDeg >= 0f && angleDeg < 90f)
		{
			frame = 3; // Bottom-Right
			baseAngleDeg = 45.0f;
		}
		else if (angleDeg >= 90f && angleDeg < 180f)
		{
			frame = 2; // Bottom-Left
			baseAngleDeg = 135.0f;
		}
		else if (angleDeg >= 180f && angleDeg < 270f)
		{
			frame = 0; // Top-Left
			baseAngleDeg = 225.0f;
		}
		else
		{
			frame = 1; // Top-Right
			baseAngleDeg = 315.0f;
		}

		float angleDifference = angleDeg - baseAngleDeg;
		
		// CRITICAL FIX: Wrap the angle difference between -180 and 180 degrees.
		// This guarantees the sprite never rotates the long way around!
		angleDifference = Mathf.Wrap(angleDifference, -180.0f, 180.0f);

		float smoothTiltDeg = angleDifference * 0.5f; 
		float visualRotationRad = Mathf.DegToRad(smoothTiltDeg);

		return (frame, visualRotationRad);
	}

	public override void _Draw()
	{
		if (DebugDrawBlockedWalls && debugBlockedEdges.Count > 0)
		{
			foreach (var edge in debugBlockedEdges)
			{
				DrawLine(GetTileLocalPos(edge.A), GetTileLocalPos(edge.B), Colors.Red, 2.0f);
			}
		}

		if (DebugDrawIntersections && intersectionTiles.Count > 0)
		{
			foreach (Vector2I tile in intersectionTiles)
			{
				DrawCircle(GetTileLocalPos(tile), CarRadius * 1.5f, Colors.LimeGreen);
			}
		}

		foreach (var car in activeCars)
		{
			var (frameIndex, visualRotationRad) = GetSpriteFrameAndRotation(car.ForwardDir);

			DrawSetTransform(car.CurrentPosition, visualRotationRad, new Vector2(SpriteScale, SpriteScale));

			if (CarSpriteSheet != null && SpriteColumns > 0 && SpriteRows > 0)
			{
				float frameWidth = CarSpriteSheet.GetWidth() / (float)SpriteColumns;
				float frameHeight = CarSpriteSheet.GetHeight() / (float)SpriteRows;

				int col = frameIndex % SpriteColumns;
				int row = frameIndex / SpriteColumns;

				Rect2 srcRect = new Rect2(col * frameWidth, row * frameHeight, frameWidth, frameHeight);
				Rect2 dstRect = new Rect2(-frameWidth * 0.5f, -frameHeight * 0.5f, frameWidth, frameHeight);

				Color drawColor = TintSpritesWithCarColor ? car.CarColor : Colors.White;
				DrawTextureRectRegion(CarSpriteSheet, dstRect, srcRect, drawColor);
			}
			else
			{
				DrawCircle(Vector2.Zero, CarRadius, car.CarColor);
				DrawArc(Vector2.Zero, CarRadius, 0, Mathf.Tau, 10, Colors.Black, 1.0f);

				Vector2 frontPos = Vector2.Right * (CarRadius * 0.65f);
				Vector2 leftLight = frontPos - Vector2.Down * (CarRadius * 0.45f);
				Vector2 rightLight = frontPos + Vector2.Down * (CarRadius * 0.45f);

				DrawCircle(leftLight, CarRadius * 0.35f, Colors.White);
				DrawCircle(rightLight, CarRadius * 0.35f, Colors.White);
			}

			DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
		}
	}
}
