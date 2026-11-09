namespace RaceDay.Api.Models;

/// <summary>Links a Participant, an Event and the selected Category.</summary>
public class Enrolment
{
    public int EnrolmentId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    public int ParticipantId { get; set; }
    public AppUser Participant { get; set; } = null!;

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public Result? Result { get; set; }
}
