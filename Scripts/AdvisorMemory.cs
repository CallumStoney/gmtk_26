using System.Collections.Generic;
using System.Linq;

public class AdvisorMemory
{
	private readonly List<DecisionRecord> _history = new();

	public void RecordDecision(string eventId, bool accepted, int turn)
	{
		_history.Add(new DecisionRecord
		{
			EventId  = eventId,
			Accepted = accepted,
			TurnMade = turn
		});
	}

	//decided?
	public bool HasDecided(string eventId)
	{
		return _history.Any(r => r.EventId == eventId);
	}

	//accepted?
	public bool WasAccepted(string eventId)
	{
		var record = _history.FindLast(r => r.EventId == eventId);
		return record != null && record.Accepted;
	}

	//turns since
	public int TurnsSinceDecision(string eventId, int currentTurn)
	{
		var record = _history.FindLast(r => r.EventId == eventId);
		if (record == null) return -1;
		return currentTurn - record.TurnMade;
	}

	// history
	public IReadOnlyList<DecisionRecord> GetAll() => _history.AsReadOnly();

	public void Reset()
	{
		_history.Clear();
	}
}

public class DecisionRecord
{
	public string EventId;
	public bool   Accepted;
	public int    TurnMade;
}
