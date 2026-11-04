using System.Net;
using System.Net.Http.Json;
using CircDesk.Api.Common;
using CircDesk.Api.Dtos;
using CircDesk.Api.Tests.Infrastructure;

namespace CircDesk.Api.Tests;

public class BooksTests : IntegrationTestBase
{
    public BooksTests(CircDeskApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateBook_AsLibrarian_CreatesCopiesAutomatically()
    {
        var (librarianToken, _) = await TestAuth.CreateLibrarianAsync(Factory, Client, "lib1");
        Client.WithToken(librarianToken);

        var book = await TestDataHelper.CreateBookAsync(Client, "Domain-Driven Design", "Eric Evans", copies: 3);

        Assert.Equal(3, book.TotalCopies);
        Assert.Equal(3, book.AvailableCopies);
    }

    [Fact]
    public async Task CreateBook_AsMember_ReturnsForbidden()
    {
        var (memberToken, _) = await TestAuth.RegisterAsync(Client, "reader1");
        Client.WithToken(memberToken);

        var response = await Client.PostAsJsonAsync("/api/books", new CreateBookRequest("Refactoring", "Martin Fowler", "111", 1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_UnknownBook_ReturnsNotFound()
    {
        var (memberToken, _) = await TestAuth.RegisterAsync(Client, "reader2");
        Client.WithToken(memberToken);

        var response = await Client.GetAsync("/api/books/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Search_FiltersByTitleOrAuthor()
    {
        var (librarianToken, _) = await TestAuth.CreateLibrarianAsync(Factory, Client, "lib2");
        Client.WithToken(librarianToken);
        await TestDataHelper.CreateBookAsync(Client, "The Pragmatic Programmer", "Andy Hunt");
        await TestDataHelper.CreateBookAsync(Client, "Clean Architecture", "Robert C. Martin");

        var response = await Client.GetAsync("/api/books?search=Pragmatic");
        var result = await response.Content.ReadFromJsonAsync<PagedResult<BookResponse>>();

        Assert.Single(result!.Items);
        Assert.Equal("The Pragmatic Programmer", result.Items[0].Title);
    }

    [Fact]
    public async Task Search_Pagination_ReturnsCorrectPageMetadata()
    {
        var (librarianToken, _) = await TestAuth.CreateLibrarianAsync(Factory, Client, "lib3");
        Client.WithToken(librarianToken);
        for (var i = 1; i <= 5; i++)
        {
            await TestDataHelper.CreateBookAsync(Client, $"Book {i:D2}", "Author");
        }

        var response = await Client.GetAsync("/api/books?page=2&pageSize=2");
        var result = await response.Content.ReadFromJsonAsync<PagedResult<BookResponse>>();

        Assert.Equal(2, result!.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal("Book 03", result.Items[0].Title);
    }

    [Fact]
    public async Task AddCopy_DuplicateInventoryCode_ReturnsConflict()
    {
        var (librarianToken, _) = await TestAuth.CreateLibrarianAsync(Factory, Client, "lib4");
        Client.WithToken(librarianToken);
        var book = await TestDataHelper.CreateBookAsync(Client, copies: 1);

        var response = await Client.PostAsJsonAsync($"/api/books/{book.Id}/copies", new AddCopyRequest($"{book.Isbn}-001"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
