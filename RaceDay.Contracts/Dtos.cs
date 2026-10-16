using System.ComponentModel.DataAnnotations;

namespace RaceDay.Contracts;

//Role names used across the system.
public static class RoleNames
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
    public static readonly string[] All = { Organiser, Participant };
}

public static class EventTypes
{
    public const string Run = "Run";
    public const string Walk = "Walk";
    public const string Cycle = "Cycle";
    public static readonly string[] All = { Run, Walk, Cycle };
}

public static class EnrolmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
    public static readonly string[] All = { Pending, Confirmed, Cancelled };
}

//Simple message wrapper used for error / info responses.
public class ApiMessage
{
    public ApiMessage() { }
    public ApiMessage(string message) => Message = message;
    public string Message { get; set; } = string.Empty;
}

//Auth
public class RegisterRequest
{
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8), StringLength(100)] public string Password { get; set; } = string.Empty;
    [Phone, StringLength(30)] public string? PhoneNumber { get; set; }
    //"Organiser" or "Participant"
    [Required] public string Role { get; set; } = string.Empty;
    
    public string? OrganiserCode { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

//Profile
public class ProfileDto
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
    [Phone, StringLength(30)] public string? PhoneNumber { get; set; }
}

// Events
public class CreateEventRequest
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Description { get; set; } = string.Empty;
    [Required] public DateTime EventDate { get; set; }
    [Required, StringLength(200)] public string Location { get; set; } = string.Empty;
    [Range(0.1, 1000)] public decimal DistanceKm { get; set; }
    /// <summary>Run, Walk or Cycle.</summary>
    [Required] public string EventType { get; set; } = string.Empty;
}

public class UpdateEventRequest : CreateEventRequest { }

public class EventDto
{
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int OrganiserId { get; set; }
    public string OrganiserName { get; set; } = string.Empty;
}

// Categories
public class CreateCategoryRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [StringLength(300)] public string? Description { get; set; }
}

public class CategoryDto
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

//Enrolments
public class CreateEnrolmentRequest
{
    [Range(1, int.MaxValue)] public int EventId { get; set; }
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
}

public class UpdateEnrolmentStatusRequest
{
    //Pending, Confirmed or Cancelled.
    [Required] public string Status { get; set; } = string.Empty;
}

public class EnrolmentDto
{
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
}

// Results 
public class CreateResultRequest
{
    [Range(1, int.MaxValue)] public int EnrolmentId { get; set; }
    /// <summary>Finish time in hh:mm:ss format, e.g. 01:45:30.</summary>
    public TimeSpan FinishTime { get; set; }
    [Range(1, int.MaxValue)] public int FinishPosition { get; set; }
}

public class ResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public TimeSpan FinishTime { get; set; }
    public int FinishPosition { get; set; }
}


public class UploadResponse
{
    public string FileUrl { get; set; } = string.Empty;
}
