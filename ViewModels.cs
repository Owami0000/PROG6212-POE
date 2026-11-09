using System.ComponentModel.DataAnnotations;
using RaceDay.Contracts;

namespace RaceDay.Mvc.ViewModels;

// ---------------- Auth ----------------
public class LoginVm
{
    [Required(ErrorMessage = "Please enter your email address."), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class RegisterVm
{
    [Required(ErrorMessage = "First name is required."), StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required."), StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required."), EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    [Display(Name = "Phone number (optional)")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Password is required."), DataType(DataType.Password)]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password."), DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a role.")]
    public string Role { get; set; } = RoleNames.Participant;

    [Display(Name = "Organiser code (only if your organisation requires one)")]
    public string? OrganiserCode { get; set; }
}

// ---------------- Organiser ----------------
public class OrganiserDashboardVm
{
    public List<EventDto> Events { get; set; } = new();
    public List<EventDto> UpcomingEvents { get; set; } = new();
    public int TotalEnrolments { get; set; }
}

public class EventFormVm
{
    public int EventId { get; set; }

    [Required(ErrorMessage = "Event name is required."), StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please describe the event."), StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Event date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Event date")]
    public DateTime EventDate { get; set; } = DateTime.Today.AddDays(30);

    [Required(ErrorMessage = "Location is required."), StringLength(200)]
    public string Location { get; set; } = string.Empty;

    [Range(0.1, 1000, ErrorMessage = "Distance must be between 0.1 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal DistanceKm { get; set; } = 10;

    [Required(ErrorMessage = "Please choose an event type.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = EventTypes.Run;

    public string? ExistingBannerUrl { get; set; }

    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }
}

public class EventSelectorVm
{
    public List<EventDto> Events { get; set; } = new();
    public int? SelectedId { get; set; }
    public string Action { get; set; } = "Index";
}

public class CategoriesPageVm
{
    public List<EventDto> Events { get; set; } = new();
    public EventDto? SelectedEvent { get; set; }
    public List<CategoryDto> Categories { get; set; } = new();
}

public class EnrolmentsPageVm
{
    public List<EventDto> Events { get; set; } = new();
    public EventDto? SelectedEvent { get; set; }
    public List<EnrolmentDto> Enrolments { get; set; } = new();
}

public class ResultsPageVm
{
    public List<EventDto> Events { get; set; } = new();
    public EventDto? SelectedEvent { get; set; }
    public List<EnrolmentDto> Enrolments { get; set; } = new();
    public List<ResultDto> Results { get; set; } = new();
}

// ---------------- Participant ----------------
public class ParticipantHomeVm
{
    public List<EventDto> Events { get; set; } = new();
    public string? Type { get; set; }
    public string? Search { get; set; }
}

public class EventDetailsVm
{
    public EventDto Event { get; set; } = new();
    public List<CategoryDto> Categories { get; set; } = new();
    public EnrolmentDto? MyEnrolment { get; set; }
    public bool IsParticipant { get; set; }
}

public class ProfileVm
{
    [Required(ErrorMessage = "First name is required."), StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required."), StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    // Display-only (nullable so they are never treated as required form fields)
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? ProfileImageUrl { get; set; }
}
