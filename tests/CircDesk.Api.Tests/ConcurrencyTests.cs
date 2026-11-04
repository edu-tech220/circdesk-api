using System.Net;
using System.Net.Http.Json;
using CircDesk.Api.Dtos;
using CircDesk.Api.Tests.Infrastructure;

namespace CircDesk.Api.Tests;

public class ConcurrencyTests : IntegrationTestBase
{
    public ConcurrencyTests(CircDeskApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Borrow_TwoMembersRaceForLastCopy_OnlyOneSucceeds()
    {
        var librarianClient = Factory.CreateClient();
        var (librarianToken, _) = await TestAuth.CreateLibrarianAsync(Factory, librarianClient, "librarian");
        librarianClient.WithToken(librarianToken);
        var book = await TestDataHelper.CreateBookAsync(librarianClient, copies: 1);

        var clientA = Factory.CreateClient();
        var (tokenA, _) = await TestAuth.RegisterAsync(clientA, "racerA");
        clientA.WithToken(tokenA);

        var clientB = Factory.CreateClient();
        var (tokenB, _) = await TestAuth.RegisterAsync(clientB, "racerB");
        clientB.WithToken(tokenB);

        var taskA = clientA.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id));
        var taskB = clientB.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id));
        var responses = await Task.WhenAll(taskA, taskB);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, successCount);
        Assert.Equal(1, conflictCount);

        var bookAfter = await (await clientA.GetAsync($"/api/books/{book.Id}")).Content.ReadFromJsonAsync<BookResponse>();
        Assert.Equal(0, bookAfter!.AvailableCopies);
    }
}
