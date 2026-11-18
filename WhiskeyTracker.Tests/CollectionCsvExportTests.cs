using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Collections;
using WhiskeyTracker.Web.Services;
using Xunit;

namespace WhiskeyTracker.Tests;

public class CollectionCsvExportTests : TestBase
{
    [Fact]
    public void Build_EscapesCommasQuotesAndFormulas()
    {
        var bottle = new Bottle
        {
            Whiskey = new Whiskey { Name = "Rare, \"Cask\"", Brand = "=cmd", Distillery = "D", Age = 12, ABV = 46.5 },
            PurchasePrice = 59.99m,
            PurchaseDate = new DateOnly(2026, 1, 2)
        };

        var csv = CollectionCsvExporter.Build(new[] { bottle });
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Name,Brand,", lines[0]);
        Assert.StartsWith("\"Rare, \"\"Cask\"\"\",'=cmd,D,,,12,46.5,", lines[1]);
        Assert.Contains("2026-01-02,59.99", lines[1]);
    }

    [Theory]
    [InlineData("+1", "'+1")]
    [InlineData("-2", "'-2")]
    [InlineData("@SUM", "'@SUM")]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("plain", "plain")]
    public void Escape_NeutralizesFormulaPrefixes(string input, string expected)
    {
        Assert.Equal(expected, CollectionCsvExporter.Escape(input));
    }

    [Fact]
    public void Escape_QuotesEmbeddedNewlines()
    {
        Assert.Equal("\"line1\nline2\"", CollectionCsvExporter.Escape("line1\nline2"));
    }

    [Fact]
    public void Build_HandlesBottleWithNullWhiskey()
    {
        var csv = CollectionCsvExporter.Build(new[] { new Bottle { Status = BottleStatus.Full } });
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.StartsWith(",,,,,,,,Full,", lines[1]);
    }

    private DetailsModel Create(AppDbContext context, string? userId)
    {
        var model = new DetailsModel(context, GetMockUserManager(context));
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        if (userId != null)
            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "test"));
        model.PageContext = new PageContext { HttpContext = http };
        return model;
    }

    [Fact]
    public async Task Export_ReturnsCsvForMember_AndForbidsNonMember()
    {
        using var context = GetInMemoryContext();
        var col = new Collection { Name = "My Bar" };
        context.Collections.Add(col);
        var w = new Whiskey { Name = "W1", Brand = "B1", Distillery = "D1" };
        context.Whiskies.Add(w);
        await context.SaveChangesAsync();
        context.CollectionMembers.Add(new CollectionMember { CollectionId = col.Id, UserId = "u1", Role = CollectionRole.Owner });
        context.Bottles.Add(new Bottle { WhiskeyId = w.Id, CollectionId = col.Id });
        await context.SaveChangesAsync();

        var ok = await Create(context, "u1").OnGetExportAsync(col.Id);
        var file = Assert.IsType<FileContentResult>(ok);
        Assert.Equal("text/csv", file.ContentType);
        Assert.StartsWith("My-Bar-", file.FileDownloadName);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, file.FileContents.Take(3).ToArray()); // UTF-8 BOM
        Assert.Contains("W1,B1,D1", System.Text.Encoding.UTF8.GetString(file.FileContents));

        Assert.IsType<ForbidResult>(await Create(context, "stranger").OnGetExportAsync(col.Id));
        Assert.IsType<ChallengeResult>(await Create(context, null).OnGetExportAsync(col.Id));
        Assert.IsType<NotFoundResult>(await Create(context, "u1").OnGetExportAsync(999));
    }
}
