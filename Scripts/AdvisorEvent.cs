public class AdvisorEvent
{
	public string AdvisorName   { get; set; } = "Advisor";
	public string DialogueText  { get; set; } = "";

	public string AcceptText    { get; set; } = "Accept";
	public string DeclineText   { get; set; } = "Decline";

	public string AcceptResponse  { get; set; } = "Done.";
	public string DeclineResponse { get; set; } = "Very well.";

	public int AcceptPopulation  { get; set; }
	public int AcceptPollution   { get; set; }
	public int AcceptPopularity  { get; set; }
	public int AcceptMoney       { get; set; }

	public int DeclinePopulation  { get; set; }
	public int DeclinePollution   { get; set; }
	public int DeclinePopularity  { get; set; }
	public int DeclineMoney       { get; set; }
}
