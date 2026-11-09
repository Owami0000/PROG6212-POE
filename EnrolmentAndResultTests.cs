using System.Net;
using System.Net.Http.Json;
using RaceDay.Contracts;

namespace RaceDay.Api.Tests;

public class EnrolmentAndResultTests : ApiTestBase
{
    public EnrolmentAndResultTests(RaceDayFactory factory) : base(factory) { }

    private async Task<(HttpClient Org, HttpClient Part, AuthResponse PartUser, EventDto Ev, CategoryDto Cat)> SetupAsync()
    {
        var (org, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var (part, partUser) = await CreateLoggedInUserAsync(RoleNames.Participant);
        var ev = await CreateEventAsync(org);
        var cat = await CreateCategoryAsync(org, ev.EventId);
        return (org, part, partUser, ev, cat);
    }

    // ---------------- Enrolments ----------------

    [Fact]
    public async Task Participant_CanEnrol_Returns201_AndEnrolmentIsRecorded()
    {
        var (_, part, partUser, ev, cat) = await SetupAsync();

        var response = await part.PostAsJsonAsync("/api/enrolments",
            new CreateEnrolmentRequest { EventId = ev.EventId, CategoryId = cat.CategoryId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var mine = await part.GetFromJsonAsync<List<EnrolmentDto>>("/api/enrolments/mine");
        var enrolment = Assert.Single(mine!);
        Assert.Equal(partUser.UserId, enrolment.ParticipantId);
        Assert.Equal(ev.EventId, enrolment.EventId);
        Assert.Equal(cat.CategoryId, enrolment.CategoryId);
        Assert.Equal(EnrolmentStatuses.Pending, enrolment.Status);
    }

    [Fact]
    public async Task Participant_CannotEnrolTwiceInSameEvent_Returns409()
    {
        var (_, part, _, ev, cat) = await SetupAsync();
        await EnrolAsync(part, ev.EventId, cat.CategoryId);

        var again = await part.PostAsJsonAsync("/api/enrolments",
            new CreateEnrolmentRequest { EventId = ev.EventId, CategoryId = cat.CategoryId });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithCategoryFromAnotherEvent_Returns400()
    {
        var (org, part, _, ev, _) = await SetupAsync();
        var otherEvent = await CreateEventAsync(org, "Other Event");
        var otherCat = await CreateCategoryAsync(org, otherEvent.EventId, "Other Cat");

        var response = await part.PostAsJsonAsync("/api/enrolments",
            new CreateEnrolmentRequest { EventId = ev.EventId, CategoryId = otherCat.CategoryId });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_InMissingEvent_Returns404()
    {
        var (part, _) = await CreateLoggedInUserAsync(RoleNames.Participant);
        var response = await part.PostAsJsonAsync("/api/enrolments",
            new CreateEnrolmentRequest { EventId = 999999, CategoryId = 1 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrol_Returns403()
    {
        var (org, _, _, ev, cat) = await SetupAsync();
        var response = await org.PostAsJsonAsync("/api/enrolments",
            new CreateEnrolmentRequest { EventId = ev.EventId, CategoryId = cat.CategoryId });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_CannotEnrol_Returns401()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/enrolments",
            new CreateEnrolmentRequest { EventId = 1, CategoryId = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewEnrolmentsForOwnEvent()
    {
        var (org, part, partUser, ev, cat) = await SetupAsync();
        await EnrolAsync(part, ev.EventId, cat.CategoryId);

        var list = await org.GetFromJsonAsync<List<EnrolmentDto>>($"/api/enrolments/event/{ev.EventId}");
        var enrolment = Assert.Single(list!);
        Assert.Equal(partUser.UserId, enrolment.ParticipantId);
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsForAnotherOrganisersEvent_Returns403()
    {
        var (_, _, _, ev, _) = await SetupAsync();
        var (otherOrg, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);

        var response = await otherOrg.GetAsync($"/api/enrolments/event/{ev.EventId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanConfirmEnrolment_AndInvalidStatusIsRejected()
    {
        var (org, part, _, ev, cat) = await SetupAsync();
        var enrolment = await EnrolAsync(part, ev.EventId, cat.CategoryId);

        var ok = await org.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status",
            new UpdateEnrolmentStatusRequest { Status = "Confirmed" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(EnrolmentStatuses.Confirmed, (await ok.Content.ReadFromJsonAsync<EnrolmentDto>())!.Status);

        var bad = await org.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status",
            new UpdateEnrolmentStatusRequest { Status = "Maybe" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotChangeEnrolmentStatus_Returns403()
    {
        var (_, part, _, ev, cat) = await SetupAsync();
        var enrolment = await EnrolAsync(part, ev.EventId, cat.CategoryId);
        var (otherOrg, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);

        var response = await otherOrg.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status",
            new UpdateEnrolmentStatusRequest { Status = "Confirmed" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------- Results ----------------

    [Fact]
    public async Task Organiser_CanCaptureResult_AndParticipantCanViewIt()
    {
        var (org, part, _, ev, cat) = await SetupAsync();
        var enrolment = await EnrolAsync(part, ev.EventId, cat.CategoryId);

        var create = await org.PostAsJsonAsync("/api/results", new CreateResultRequest
        {
            EnrolmentId = enrolment.EnrolmentId,
            FinishTime = new TimeSpan(0, 52, 30),
            FinishPosition = 3
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var mine = await part.GetFromJsonAsync<List<ResultDto>>("/api/results/mine");
        var result = Assert.Single(mine!);
        Assert.Equal(3, result.FinishPosition);
        Assert.Equal(new TimeSpan(0, 52, 30), result.FinishTime);
        Assert.Equal(ev.EventId, result.EventId);

        var forEvent = await org.GetFromJsonAsync<List<ResultDto>>($"/api/results/event/{ev.EventId}");
        Assert.Single(forEvent!);
    }

    [Fact]
    public async Task Participant_CannotCaptureResult_Returns403()
    {
        var (_, part, _, ev, cat) = await SetupAsync();
        var enrolment = await EnrolAsync(part, ev.EventId, cat.CategoryId);

        var response = await part.PostAsJsonAsync("/api/results", new CreateResultRequest
        {
            EnrolmentId = enrolment.EnrolmentId, FinishTime = new TimeSpan(0, 40, 0), FinishPosition = 1
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotCaptureResult_Returns403()
    {
        var (_, part, _, ev, cat) = await SetupAsync();
        var enrolment = await EnrolAsync(part, ev.EventId, cat.CategoryId);
        var (otherOrg, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);

        var response = await otherOrg.PostAsJsonAsync("/api/results", new CreateResultRequest
        {
            EnrolmentId = enrolment.EnrolmentId, FinishTime = new TimeSpan(0, 40, 0), FinishPosition = 1
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_Twice_Returns409()
    {
        var (org, part, _, ev, cat) = await SetupAsync();
        var enrolment = await EnrolAsync(part, ev.EventId, cat.CategoryId);
        var request = new CreateResultRequest
        {
            EnrolmentId = enrolment.EnrolmentId, FinishTime = new TimeSpan(0, 45, 0), FinishPosition = 2
        };

        Assert.Equal(HttpStatusCode.Created, (await org.PostAsJsonAsync("/api/results", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await org.PostAsJsonAsync("/api/results", request)).StatusCode);
    }

    [Fact]
    public async Task CaptureResult_ForMissingEnrolment_Returns404()
    {
        var (org, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var response = await org.PostAsJsonAsync("/api/results", new CreateResultRequest
        {
            EnrolmentId = 999999, FinishTime = new TimeSpan(0, 45, 0), FinishPosition = 1
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Participant_OnlySeesOwnResults()
    {
        var (org, part1, _, ev, cat) = await SetupAsync();
        var (part2, _) = await CreateLoggedInUserAsync(RoleNames.Participant);
        var e1 = await EnrolAsync(part1, ev.EventId, cat.CategoryId);
        var e2 = await EnrolAsync(part2, ev.EventId, cat.CategoryId);

        await org.PostAsJsonAsync("/api/results", new CreateResultRequest { EnrolmentId = e1.EnrolmentId, FinishTime = new TimeSpan(0, 40, 0), FinishPosition = 1 });
        await org.PostAsJsonAsync("/api/results", new CreateResultRequest { EnrolmentId = e2.EnrolmentId, FinishTime = new TimeSpan(0, 50, 0), FinishPosition = 2 });

        var list = await part2.GetFromJsonAsync<List<ResultDto>>("/api/results/mine");
        var only = Assert.Single(list!);
        Assert.Equal(2, only.FinishPosition);
    }
}
