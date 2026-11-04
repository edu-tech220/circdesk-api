using System.Net;
using System.Net.Http.Json;
using CircDesk.Api.Dtos;
using CircDesk.Api.Tests.Infrastructure;

namespace CircDesk.Api.Tests;

public class FinesTests : IntegrationTestBase
{
    public FinesTests(CircDeskApiFactory factory) : base(factory)
    {
    }

    private async Task<(HttpClient Member, HttpClient Librarian, FineResponse Fine)> ArrangeOverdueFineAsync()
    {
        var librarianClient = Factory.CreateClient();
        var (librarianToken, _) = await TestAuth.CreateLibrarianAsync(Factory, librarianClient, "librarian");
        librarianClient.WithToken(librarianToken);
        var book = await TestDataHelper.CreateBookAsync(librarianClient, copies: 1);

        var memberClient = Factory.CreateClient();
        var (memberToken, _) = await TestAuth.RegisterAsync(memberClient, "member1");
        memberClient.WithToken(memberToken);

        var loan = await (await memberClient.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id)))
            .Content.ReadFromJsonAsync<LoanResponse>();
        await TestDataHelper.SetLoanDueDateAsync(Factory, loan!.Id, DateTime.UtcNow.AddDays(-5));
        var returnResult = await (await librarianClient.PostAsync($"/api/loans/{loan.Id}/return", null))
            .Content.ReadFromJsonAsync<ReturnResponse>();

        var fines = await (await memberClient.GetAsync("/api/fines/mine")).Content.ReadFromJsonAsync<CircDesk.Api.Common.PagedResult<FineResponse>>();
        var fine = Assert.Single(fines!.Items);
        Assert.Equal(returnResult!.FineCharged, fine.Amount);

        return (memberClient, librarianClient, fine);
    }

    [Fact]
    public async Task Pay_OwnFine_MarksAsPaid()
    {
        var (member, _, fine) = await ArrangeOverdueFineAsync();

        var response = await member.PostAsync($"/api/fines/{fine.Id}/pay", null);
        var paid = await response.Content.ReadFromJsonAsync<FineResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Paid", paid!.Status);
        Assert.NotNull(paid.PaidAt);
    }

    [Fact]
    public async Task Pay_OtherMembersFine_ReturnsForbidden()
    {
        var (_, _, fine) = await ArrangeOverdueFineAsync();

        var intruderClient = Factory.CreateClient();
        var (intruderToken, _) = await TestAuth.RegisterAsync(intruderClient, "intruder");
        intruderClient.WithToken(intruderToken);

        var response = await intruderClient.PostAsync($"/api/fines/{fine.Id}/pay", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Pay_AlreadyPaidFine_ReturnsConflict()
    {
        var (member, _, fine) = await ArrangeOverdueFineAsync();
        await member.PostAsync($"/api/fines/{fine.Id}/pay", null);

        var response = await member.PostAsync($"/api/fines/{fine.Id}/pay", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Borrow_WithOutstandingFinesAboveThreshold_ReturnsForbidden()
    {
        var librarianClient = Factory.CreateClient();
        var (librarianToken, _) = await TestAuth.CreateLibrarianAsync(Factory, librarianClient, "librarian");
        librarianClient.WithToken(librarianToken);

        var memberClient = Factory.CreateClient();
        var (memberToken, _) = await TestAuth.RegisterAsync(memberClient, "debtor");
        memberClient.WithToken(memberToken);

        for (var i = 0; i < 3; i++)
        {
            var book = await TestDataHelper.CreateBookAsync(librarianClient, $"Overdue Book {i}", copies: 1);
            var loan = await (await memberClient.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id)))
                .Content.ReadFromJsonAsync<LoanResponse>();
            await TestDataHelper.SetLoanDueDateAsync(Factory, loan!.Id, DateTime.UtcNow.AddDays(-60));
            await librarianClient.PostAsync($"/api/loans/{loan.Id}/return", null);
        }

        var newBook = await TestDataHelper.CreateBookAsync(librarianClient, "Fresh Book", copies: 1);
        var response = await memberClient.PostAsJsonAsync("/api/loans", new BorrowRequest(newBook.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
