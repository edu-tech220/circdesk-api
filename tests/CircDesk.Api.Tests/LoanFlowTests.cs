using System.Net;
using System.Net.Http.Json;
using CircDesk.Api.Dtos;
using CircDesk.Api.Tests.Infrastructure;

namespace CircDesk.Api.Tests;

public class LoanFlowTests : IntegrationTestBase
{
    public LoanFlowTests(CircDeskApiFactory factory) : base(factory)
    {
    }

    private async Task<(HttpClient Member, HttpClient Librarian, BookResponse Book)> ArrangeAsync(int copies = 1)
    {
        var librarianClient = Factory.CreateClient();
        var (librarianToken, _) = await TestAuth.CreateLibrarianAsync(Factory, librarianClient, "librarian");
        librarianClient.WithToken(librarianToken);

        var book = await TestDataHelper.CreateBookAsync(librarianClient, copies: copies);

        var memberClient = Factory.CreateClient();
        var (memberToken, _) = await TestAuth.RegisterAsync(memberClient, "member1");
        memberClient.WithToken(memberToken);

        return (memberClient, librarianClient, book);
    }

    [Fact]
    public async Task Borrow_WithAvailableCopy_Succeeds()
    {
        var (member, _, book) = await ArrangeAsync();

        var response = await member.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var loan = await response.Content.ReadFromJsonAsync<LoanResponse>();
        Assert.Equal("Active", loan!.Status);
        Assert.Equal(book.Id, loan.BookId);
    }

    [Fact]
    public async Task Borrow_NoCopiesAvailable_ReturnsConflict()
    {
        var (member, _, book) = await ArrangeAsync(copies: 1);
        await member.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id));

        var otherClient = Factory.CreateClient();
        var (otherToken, _) = await TestAuth.RegisterAsync(otherClient, "member2");
        otherClient.WithToken(otherToken);

        var response = await otherClient.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Borrow_SameBookTwice_ReturnsConflict()
    {
        var (member, _, book) = await ArrangeAsync(copies: 2);
        await member.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id));

        var response = await member.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Borrow_BeyondMaxActiveLoans_ReturnsConflict()
    {
        var librarianClient = Factory.CreateClient();
        var (librarianToken, _) = await TestAuth.CreateLibrarianAsync(Factory, librarianClient, "librarian");
        librarianClient.WithToken(librarianToken);

        var memberClient = Factory.CreateClient();
        var (memberToken, _) = await TestAuth.RegisterAsync(memberClient, "voracious");
        memberClient.WithToken(memberToken);

        for (var i = 0; i < 5; i++)
        {
            var book = await TestDataHelper.CreateBookAsync(librarianClient, $"Book {i}");
            var response = await memberClient.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var extraBook = await TestDataHelper.CreateBookAsync(librarianClient, "One too many");
        var finalResponse = await memberClient.PostAsJsonAsync("/api/loans", new BorrowRequest(extraBook.Id));

        Assert.Equal(HttpStatusCode.Conflict, finalResponse.StatusCode);
    }

    [Fact]
    public async Task Return_AsMember_ReturnsForbidden()
    {
        var (member, _, book) = await ArrangeAsync();
        var loan = await (await member.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id))).Content.ReadFromJsonAsync<LoanResponse>();

        var response = await member.PostAsync($"/api/loans/{loan!.Id}/return", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Return_OnTime_NoFineAndCopyBecomesAvailable()
    {
        var (member, librarian, book) = await ArrangeAsync();
        var loan = await (await member.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id))).Content.ReadFromJsonAsync<LoanResponse>();

        var response = await librarian.PostAsync($"/api/loans/{loan!.Id}/return", null);
        var result = await response.Content.ReadFromJsonAsync<ReturnResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(result!.FineCharged);
        Assert.Equal("Returned", result.Loan.Status);

        var bookAfter = await (await member.GetAsync($"/api/books/{book.Id}")).Content.ReadFromJsonAsync<BookResponse>();
        Assert.Equal(1, bookAfter!.AvailableCopies);
    }

    [Fact]
    public async Task Return_Overdue_ChargesCappedFine()
    {
        var (member, librarian, book) = await ArrangeAsync();
        var loan = await (await member.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id))).Content.ReadFromJsonAsync<LoanResponse>();
        await TestDataHelper.SetLoanDueDateAsync(Factory, loan!.Id, DateTime.UtcNow.AddDays(-30));

        var response = await librarian.PostAsync($"/api/loans/{loan.Id}/return", null);
        var result = await response.Content.ReadFromJsonAsync<ReturnResponse>();

        Assert.Equal(20.00m, result!.FineCharged);
    }

    [Fact]
    public async Task Return_AlreadyReturned_ReturnsConflict()
    {
        var (member, librarian, book) = await ArrangeAsync();
        var loan = await (await member.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id))).Content.ReadFromJsonAsync<LoanResponse>();
        await librarian.PostAsync($"/api/loans/{loan!.Id}/return", null);

        var response = await librarian.PostAsync($"/api/loans/{loan.Id}/return", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Renew_BeforeDueDate_ExtendsDueDateAndIncrementsCount()
    {
        var (member, _, book) = await ArrangeAsync();
        var loan = await (await member.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id))).Content.ReadFromJsonAsync<LoanResponse>();

        var response = await member.PostAsync($"/api/loans/{loan!.Id}/renew", null);
        var renewed = await response.Content.ReadFromJsonAsync<LoanResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, renewed!.RenewalCount);
        Assert.True(renewed.DueDate > loan.DueDate);
    }

    [Fact]
    public async Task Renew_SecondTime_ReturnsConflictAtMaxRenewals()
    {
        var (member, _, book) = await ArrangeAsync();
        var loan = await (await member.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id))).Content.ReadFromJsonAsync<LoanResponse>();
        await member.PostAsync($"/api/loans/{loan!.Id}/renew", null);

        var response = await member.PostAsync($"/api/loans/{loan.Id}/renew", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Renew_WhenOverdue_ReturnsConflict()
    {
        var (member, _, book) = await ArrangeAsync();
        var loan = await (await member.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id))).Content.ReadFromJsonAsync<LoanResponse>();
        await TestDataHelper.SetLoanDueDateAsync(Factory, loan!.Id, DateTime.UtcNow.AddDays(-1));

        var response = await member.PostAsync($"/api/loans/{loan.Id}/renew", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Renew_OtherMembersLoan_ReturnsForbidden()
    {
        var (member, _, book) = await ArrangeAsync();
        var loan = await (await member.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id))).Content.ReadFromJsonAsync<LoanResponse>();

        var otherClient = Factory.CreateClient();
        var (otherToken, _) = await TestAuth.RegisterAsync(otherClient, "intruder");
        otherClient.WithToken(otherToken);

        var response = await otherClient.PostAsync($"/api/loans/{loan!.Id}/renew", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyOwnLoans()
    {
        var (member, _, book) = await ArrangeAsync();
        await member.PostAsJsonAsync("/api/loans", new BorrowRequest(book.Id));

        var response = await member.GetAsync("/api/loans/mine");
        var result = await response.Content.ReadFromJsonAsync<CircDesk.Api.Common.PagedResult<LoanResponse>>();

        Assert.Single(result!.Items);
    }
}
