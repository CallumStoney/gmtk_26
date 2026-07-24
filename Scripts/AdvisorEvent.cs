using System.Collections.Generic;

public class AdvisorEvent
{
	//id shii
	public string Id              { get; set; } = "";       
	public bool   IsOneShot       { get; set; } = false;    
	public string PrerequisiteId  { get; set; } = null;     
	public bool?  RequiresAccepted { get; set; } = null;    
	public int    MinTurnDelay    { get; set; } = 0;        

	//visable
	public string AdvisorName   { get; set; } = "Advisor";
	public string DialogueText  { get; set; } = "";

	public string AcceptText    { get; set; } = "Accept";
	public string DeclineText   { get; set; } = "Decline";

	public string AcceptResponse  { get; set; } = "Done.";
	public string DeclineResponse { get; set; } = "Very well.";

	// stat impact
	public int AcceptPopulation  { get; set; }
	public int AcceptPollution   { get; set; }
	public int AcceptPopularity  { get; set; }
	public int AcceptMoney       { get; set; }

	public int DeclinePopulation  { get; set; }
	public int DeclinePollution   { get; set; }
	public int DeclinePopularity  { get; set; }
	public int DeclineMoney       { get; set; }

	// empty stubs to be hooked in later
	public List<string> AcceptTags  { get; set; } = new();
	public List<string> DeclineTags { get; set; } = new();
}
