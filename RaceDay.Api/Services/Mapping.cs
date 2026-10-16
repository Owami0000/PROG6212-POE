using RaceDay.Api.Models;
using RaceDay.Contracts;

namespace RaceDay.Api.Services;

public static class Mapping
{
    public static string FullName(this AppUser? u) => u is null ? string.Empty : $"{u.FirstName} {u.LastName}";

    //Case-insensitive match against allowed values; returns the canonical value or null
    public static string? MatchAllowed(IEnumerable<string> allowed, string? input) =>
        allowed.FirstOrDefault(a => string.Equals(a, input?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static AuthResponse ToAuthResponse(this AppUser u) => new()
    {
        UserId = u.UserId, FirstName = u.FirstName, LastName = u.LastName,
        Email = u.Email, Role = u.Role.Name
    };

    public static ProfileDto ToProfileDto(this AppUser u) => new()
    {
        UserId = u.UserId, FirstName = u.FirstName, LastName = u.LastName,
        Email = u.Email, PhoneNumber = u.PhoneNumber, Role = u.Role.Name
    };

    public static EventDto ToDto(this Event e) => new()
    {
        EventId = e.EventId, Name = e.Name, Description = e.Description, EventDate = e.EventDate,
        Location = e.Location, DistanceKm = e.DistanceKm, EventType = e.EventType,
        OrganiserId = e.OrganiserId, OrganiserName = e.Organiser.FullName()
    };

    public static CategoryDto ToDto(this Category c) => new()
    {
        CategoryId = c.CategoryId, EventId = c.EventId, Name = c.Name, Description = c.Description
    };

    public static EnrolmentDto ToDto(this Enrolment e) => new()
    {
        EnrolmentId = e.EnrolmentId, EventId = e.EventId, EventName = e.Event.Name,
        CategoryId = e.CategoryId, CategoryName = e.Category.Name,
        ParticipantId = e.ParticipantId, ParticipantName = e.Participant.FullName(),
        Status = e.Status, EnrolledAt = e.EnrolledAt
    };

    public static ResultDto ToDto(this Result r) => new()
    {
        ResultId = r.ResultId, EnrolmentId = r.EnrolmentId,
        EventId = r.Enrolment.EventId, EventName = r.Enrolment.Event.Name,
        ParticipantId = r.Enrolment.ParticipantId, ParticipantName = r.Enrolment.Participant.FullName(),
        CategoryName = r.Enrolment.Category.Name,
        FinishTime = r.FinishTime, FinishPosition = r.FinishPosition
    };
}
