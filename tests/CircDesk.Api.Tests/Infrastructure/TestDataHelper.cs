using System.Net.Http.Json;
using CircDesk.Api.Data;
using CircDesk.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CircDesk.Api.Tests.Infrastructure;

public static class TestDataHelper
{
    public static async Task<BookResponse> CreateBookAsync(
        HttpClient librarianClient, string title = "Clean Code", string author = "Robert C. Martin", string? isbn = null, int copies = 1)
    {
        isbn ??= Guid.NewGuid().ToString("N")[..10];
        var response = await librarianClient.PostAsJsonAsync("/api/books", new CreateBookRequest(title, author, isbn, copies));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BookResponse>())!;
    }

    public static async Task SetLoanDueDateAsync(CircDeskApiFactory factory, int loanId, DateTime dueDate)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CircDeskDbContext>();
        var loan = await db.Loans.FirstAsync(l => l.Id == loanId);
        loan.DueDate = dueDate;
        await db.SaveChangesAsync();
    }

    public static async Task SetHoldExpiredAsync(CircDeskApiFactory factory, int holdId, DateTime expiresAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CircDeskDbContext>();
        var hold = await db.Holds.FirstAsync(h => h.Id == holdId);
        hold.ExpiresAt = expiresAt;
        await db.SaveChangesAsync();
    }
}
