using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Whiskies;
using Xunit;

namespace WhiskeyTracker.Tests;

public class WhiskeyTrimTests : TestBase
{
    [Fact]
    public async Task SaveChanges_TrimsWhiskeyTextFields_OnAdd()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey
        {
            Brand = " New Riff Distilling ",
            Name = " Bottled in Bond\t",
            Distillery = " New Riff ",
            Region = " Kentucky ",
            Type = "Kentucky Straight Bourbon ",
            CaskType = " New Charred Oak "
        });
        await context.SaveChangesAsync();

        var saved = await context.Whiskies.AsNoTracking().SingleAsync();
        Assert.Equal("New Riff Distilling", saved.Brand);
        Assert.Equal("Bottled in Bond", saved.Name);
        Assert.Equal("New Riff", saved.Distillery);
        Assert.Equal("Kentucky", saved.Region);
        Assert.Equal("Kentucky Straight Bourbon", saved.Type);
        Assert.Equal("New Charred Oak", saved.CaskType);
    }

    [Fact]
    public async Task SaveChanges_TrimsWhiskeyTextFields_OnEdit()
    {
        using var context = GetInMemoryContext();
        var whiskey = new Whiskey { Brand = "New Riff Distilling", Name = "Rye", Distillery = "New Riff" };
        context.Whiskies.Add(whiskey);
        await context.SaveChangesAsync();

        whiskey.Brand = "New Riff Distilling ";
        whiskey.Type = " Kentucky Straight Rye";
        await context.SaveChangesAsync();

        var saved = await context.Whiskies.AsNoTracking().SingleAsync();
        Assert.Equal("New Riff Distilling", saved.Brand);
        Assert.Equal("Kentucky Straight Rye", saved.Type);
    }

    [Fact]
    public async Task Index_FilterLists_HaveNoWhitespaceOnlyDuplicates()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.AddRange(
            new Whiskey { Brand = "New Riff Distilling", Name = "A", Distillery = "D", Type = "Kentucky Straight Bourbon", Region = "Kentucky" },
            new Whiskey { Brand = "New Riff Distilling ", Name = "B", Distillery = "D", Type = "Kentucky Straight Bourbon ", Region = " Kentucky" });
        await context.SaveChangesAsync();

        var pageModel = new IndexModel(context, new WhiskeyTracker.Web.Services.LegacyMigrationService(context));
        SetMockUser(pageModel, "user1");
        await pageModel.OnGetAsync();

        Assert.Equal(new[] { "New Riff Distilling" }, pageModel.Brands!.Select(i => i.Text));
        Assert.Equal(new[] { "Kentucky Straight Bourbon" }, pageModel.Types!.Select(i => i.Text));
        Assert.Equal(new[] { "Kentucky" }, pageModel.Regions!.Select(i => i.Text));

        pageModel.WhiskeyBrand = "New Riff Distilling";
        await pageModel.OnGetAsync();
        Assert.Equal(2, pageModel.Whiskies.Count);
    }
}
