using System.Net;
using System.Net.Http.Json;
using RaceDay.Contracts;

namespace RaceDay.Api.Tests;

public class EventManagementTests : ApiTestBase
{
    public EventManagementTests(RaceDayFactory factory) : base(factory) { }

    [Fact]
    public async Task Organiser_CanCreateEvent_Returns201()
    {
        var (organiser, user) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var response = await organiser.PostAsJsonAsync("/api/events", NewEventRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ev = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.Equal(user.UserId, ev!.OrganiserId);
        Assert.Equal("City 10km Run", ev.Name);
    }

    [Fact]
    public async Task Participant_CannotCreateEvent_Returns403()
    {
        var (participant, _) = await CreateLoggedInUserAsync(RoleNames.Participant);
        var response = await participant.PostAsJsonAsync("/api/events", NewEventRequest());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_CannotCreateEvent_Returns401()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/events", NewEventRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithInvalidEventType_Returns400()
    {
        var (organiser, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var request = NewEventRequest();
        request.EventType = "Swim";
        var response = await organiser.PostAsJsonAsync("/api/events", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithMissingName_Returns400()
    {
        var (organiser, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var request = NewEventRequest();
        request.Name = "";
        var response = await organiser.PostAsJsonAsync("/api/events", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BothRoles_CanViewEvents()
    {
        var (organiser, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var (participant, _) = await CreateLoggedInUserAsync(RoleNames.Participant);
        var ev = await CreateEventAsync(organiser);

        foreach (var client in new[] { organiser, participant })
        {
            var list = await client.GetFromJsonAsync<List<EventDto>>("/api/events");
            Assert.Contains(list!, e => e.EventId == ev.EventId);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/events/{ev.EventId}")).StatusCode);
        }
    }

    [Fact]
    public async Task GetEvent_NotFound_Returns404()
    {
        var (participant, _) = await CreateLoggedInUserAsync(RoleNames.Participant);
        var response = await participant.GetAsync("/api/events/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_Mine_ReturnsOnlyOwnEvents()
    {
        var (org1, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var (org2, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var mine = await CreateEventAsync(org1, "Org1 Event");
        var other = await CreateEventAsync(org2, "Org2 Event");

        var list = await org1.GetFromJsonAsync<List<EventDto>>("/api/events/mine");
        Assert.Contains(list!, e => e.EventId == mine.EventId);
        Assert.DoesNotContain(list!, e => e.EventId == other.EventId);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var (organiser, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var ev = await CreateEventAsync(organiser);

        var update = NewEventRequest("Renamed Event");
        update.DistanceKm = 21.1m;
        update.EventType = EventTypes.Cycle;
        var response = await organiser.PutAsJsonAsync($"/api/events/{ev.EventId}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.Equal("Renamed Event", updated!.Name);
        Assert.Equal(EventTypes.Cycle, updated.EventType);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_Returns403()
    {
        var (owner, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var (intruder, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var ev = await CreateEventAsync(owner);

        var response = await intruder.PutAsJsonAsync($"/api/events/{ev.EventId}", NewEventRequest("Hijacked"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent_Returns403()
    {
        var (owner, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var (intruder, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var ev = await CreateEventAsync(owner);

        Assert.Equal(HttpStatusCode.Forbidden, (await intruder.DeleteAsync($"/api/events/{ev.EventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/events/{ev.EventId}")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateOrDeleteEvent_Returns403()
    {
        var (organiser, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var (participant, _) = await CreateLoggedInUserAsync(RoleNames.Participant);
        var ev = await CreateEventAsync(organiser);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await participant.PutAsJsonAsync($"/api/events/{ev.EventId}", NewEventRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await participant.DeleteAsync($"/api/events/{ev.EventId}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent_ThenItIsGone()
    {
        var (organiser, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var ev = await CreateEventAsync(organiser);

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.DeleteAsync($"/api/events/{ev.EventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.GetAsync($"/api/events/{ev.EventId}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteEventThatHasEnrolments()
    {
        var (organiser, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var (participant, _) = await CreateLoggedInUserAsync(RoleNames.Participant);
        var ev = await CreateEventAsync(organiser);
        var cat = await CreateCategoryAsync(organiser, ev.EventId);
        await EnrolAsync(participant, ev.EventId, cat.CategoryId);

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.DeleteAsync($"/api/events/{ev.EventId}")).StatusCode);
    }

    [Fact]
    public async Task Categories_OrganiserManagesThem_BothRolesCanView()
    {
        var (organiser, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var (participant, _) = await CreateLoggedInUserAsync(RoleNames.Participant);
        var ev = await CreateEventAsync(organiser);

        var cat = await CreateCategoryAsync(organiser, ev.EventId, "Under 20");
        var update = await organiser.PutAsJsonAsync($"/api/categories/{cat.CategoryId}",
            new CreateCategoryRequest { Name = "Under 18" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var list = await participant.GetFromJsonAsync<List<CategoryDto>>($"/api/events/{ev.EventId}/categories");
        Assert.Contains(list!, c => c.Name == "Under 18");

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.DeleteAsync($"/api/categories/{cat.CategoryId}")).StatusCode);
    }

    [Fact]
    public async Task Categories_OtherOrganiserCannotCreate_Returns403()
    {
        var (owner, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var (intruder, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var ev = await CreateEventAsync(owner);

        var response = await intruder.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new CreateCategoryRequest { Name = "Sneaky" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Categories_DuplicateNameInSameEvent_Returns409()
    {
        var (organiser, _) = await CreateLoggedInUserAsync(RoleNames.Organiser);
        var ev = await CreateEventAsync(organiser);
        await CreateCategoryAsync(organiser, ev.EventId, "Senior");

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new CreateCategoryRequest { Name = "Senior" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
