using System.Net;
using System.Net.Http.Json;
using CircDesk.Api.Dtos;
using CircDesk.Api.Services;
using CircDesk.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CircDesk.Api.Tests;

public class HoldsTests : IntegrationTestBase
{
    public HoldsTests(CircDeskApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateHold_WhenCopyAvailable_ReturnsConflict()
    {
        var librarianClient = Factory.CreateClient();
        var (librarianToken, _) = await TestAuth.CreateLibrarianAsync(Factory, librarianClient, "librarian");
        librarianClient.WithToken(librarianToken);
        var book = await TestDataHelper.CreateBookAsync(librarianClient, copies: 1);

        var memberClient = Factory.CreateClient();
        var (memberToken, _) = await TestAuth.RegisterAsync(memberClient, "member1");
        memberClient.WithToken(memberToken);

        var response = await memberClient.PostAsJsonAsync("/api/holds", new CreateHoldRequest(book.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateHold_WhenNoCopiesAvailable_QueuesInOrder()
    {
        var librarianClient = Factory.CreateClient();
        var (librarianToken, _) = await TestAuth.CreateLibrarianAsync(Factory, librarianClient, "librarian");
        librarianClient.WithToken(librarianToken);
        var book = await TestDataHelper.CreateBookAsync(librarianClient, copies: 1);

        var borrowerClient = Factory.CreateClient();
        var (borrowerToken, _) = await TestAuth.RegisterAsync(borrowerClient, "borrower");
        borrowerClient.WithToken(borrowerToken);
        await borrowerClient.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id));

        var firstClient = Factory.CreateClient();
        var (firstToken, _) = await TestAuth.RegisterAsync(firstClient, "first");
        firstClient.WithToken(firstToken);
        var firstHold = await (await firstClient.PostAsJsonAsync("/api/holds", new CreateHoldRequest(book.Id)))
            .Content.ReadFromJsonAsync<HoldResponse>();

        var secondClient = Factory.CreateClient();
        var (secondToken, _) = await TestAuth.RegisterAsync(secondClient, "second");
        secondClient.WithToken(secondToken);
        var secondHold = await (await secondClient.PostAsJsonAsync("/api/holds", new CreateHoldRequest(book.Id)))
            .Content.ReadFromJsonAsync<HoldResponse>();

        Assert.Equal(1, firstHold!.QueuePosition);
        Assert.Equal(2, secondHold!.QueuePosition);
    }

    [Fact]
    public async Task ReturnCopy_PromotesOldestQueuedHoldToReady()
    {
        var librarianClient = Factory.CreateClient();
        var (librarianToken, _) = await TestAuth.CreateLibrarianAsync(Factory, librarianClient, "librarian");
        librarianClient.WithToken(librarianToken);
        var book = await TestDataHelper.CreateBookAsync(librarianClient, copies: 1);

        var borrowerClient = Factory.CreateClient();
        var (borrowerToken, _) = await TestAuth.RegisterAsync(borrowerClient, "borrower");
        borrowerClient.WithToken(borrowerToken);
        var loan = await (await borrowerClient.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id)))
            .Content.ReadFromJsonAsync<LoanResponse>();

        var waitingClient = Factory.CreateClient();
        var (waitingToken, _) = await TestAuth.RegisterAsync(waitingClient, "waiting");
        waitingClient.WithToken(waitingToken);
        await waitingClient.PostAsJsonAsync("/api/holds", new CreateHoldRequest(book.Id));

        await librarianClient.PostAsync($"/api/loans/{loan!.Id}/return", null);

        var holds = await (await waitingClient.GetAsync("/api/holds/mine")).Content.ReadFromJsonAsync<CircDesk.Api.Common.PagedResult<HoldResponse>>();
        var hold = Assert.Single(holds!.Items);
        Assert.Equal("Ready", hold.Status);
        Assert.NotNull(hold.ExpiresAt);

        var bookAfterReturn = await (await waitingClient.GetAsync($"/api/books/{book.Id}")).Content.ReadFromJsonAsync<BookResponse>();
        Assert.Equal(0, bookAfterReturn!.AvailableCopies);

        var borrowResponse = await waitingClient.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id));
        Assert.Equal(HttpStatusCode.Created, borrowResponse.StatusCode);
    }

    [Fact]
    public async Task CancelHold_OtherMembersHold_ReturnsForbidden()
    {
        var librarianClient = Factory.CreateClient();
        var (librarianToken, _) = await TestAuth.CreateLibrarianAsync(Factory, librarianClient, "librarian");
        librarianClient.WithToken(librarianToken);
        var book = await TestDataHelper.CreateBookAsync(librarianClient, copies: 1);

        var borrowerClient = Factory.CreateClient();
        var (borrowerToken, _) = await TestAuth.RegisterAsync(borrowerClient, "borrower");
        borrowerClient.WithToken(borrowerToken);
        await borrowerClient.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id));

        var holderClient = Factory.CreateClient();
        var (holderToken, _) = await TestAuth.RegisterAsync(holderClient, "holder");
        holderClient.WithToken(holderToken);
        var hold = await (await holderClient.PostAsJsonAsync("/api/holds", new CreateHoldRequest(book.Id)))
            .Content.ReadFromJsonAsync<HoldResponse>();

        var intruderClient = Factory.CreateClient();
        var (intruderToken, _) = await TestAuth.RegisterAsync(intruderClient, "intruder");
        intruderClient.WithToken(intruderToken);

        var response = await intruderClient.DeleteAsync($"/api/holds/{hold!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ExpireDueHolds_CascadesToNextInQueue()
    {
        var librarianClient = Factory.CreateClient();
        var (librarianToken, _) = await TestAuth.CreateLibrarianAsync(Factory, librarianClient, "librarian");
        librarianClient.WithToken(librarianToken);
        var book = await TestDataHelper.CreateBookAsync(librarianClient, copies: 1);

        var borrowerClient = Factory.CreateClient();
        var (borrowerToken, _) = await TestAuth.RegisterAsync(borrowerClient, "borrower");
        borrowerClient.WithToken(borrowerToken);
        var loan = await (await borrowerClient.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id)))
            .Content.ReadFromJsonAsync<LoanResponse>();

        var firstClient = Factory.CreateClient();
        var (firstToken, _) = await TestAuth.RegisterAsync(firstClient, "first");
        firstClient.WithToken(firstToken);
        var firstHold = await (await firstClient.PostAsJsonAsync("/api/holds", new CreateHoldRequest(book.Id)))
            .Content.ReadFromJsonAsync<HoldResponse>();

        var secondClient = Factory.CreateClient();
        var (secondToken, _) = await TestAuth.RegisterAsync(secondClient, "second");
        secondClient.WithToken(secondToken);
        await secondClient.PostAsJsonAsync("/api/holds", new CreateHoldRequest(book.Id));

        await librarianClient.PostAsync($"/api/loans/{loan!.Id}/return", null);
        await TestDataHelper.SetHoldExpiredAsync(Factory, firstHold!.Id, DateTime.UtcNow.AddHours(-1));

        using (var scope = Factory.Services.CreateScope())
        {
            var holdService = scope.ServiceProvider.GetRequiredService<IHoldService>();
            var expiredCount = await holdService.ExpireDueHoldsAsync();
            Assert.Equal(1, expiredCount);
        }

        var secondHolds = await (await secondClient.GetAsync("/api/holds/mine")).Content.ReadFromJsonAsync<CircDesk.Api.Common.PagedResult<HoldResponse>>();
        var promoted = Assert.Single(secondHolds!.Items);
        Assert.Equal("Ready", promoted.Status);
    }
}
