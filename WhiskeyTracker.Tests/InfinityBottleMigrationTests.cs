using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Tasting;
using WhiskeyTracker.Web.Services;
using Xunit;

namespace WhiskeyTracker.Tests;

public class InfinityBottleMigrationTests : TestBase
{
    private static IWebHostEnvironment MockEnvironment(string webRoot)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns(webRoot);
        return env.Object;
    }

    [Fact]
    public async Task Migrate_GivesSharedInfinityBottleItsOwnWhiskey_CopyingOnlyTheImage()
    {
        using var context = GetInMemoryContext();

        var webRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var imagesDir = Path.Combine(webRoot, "images");
        Directory.CreateDirectory(imagesDir);
        File.WriteAllText(Path.Combine(imagesDir, "orig.jpg"), "fake-image");

        try
        {
            var original = new Whiskey
            {
                Name = "Eagle Rare", Brand = "Buffalo Trace", Distillery = "Buffalo Trace",
                Region = "Kentucky", Type = "Bourbon", ImageFileName = "orig.jpg"
            };
            context.Whiskies.Add(original);
            await context.SaveChangesAsync();

            var regular = new Bottle { WhiskeyId = original.Id, Status = BottleStatus.Full };
            var infinity = new Bottle { WhiskeyId = original.Id, IsInfinityBottle = true, Status = BottleStatus.Opened };
            context.Bottles.AddRange(regular, infinity);
            await context.SaveChangesAsync();

            var session = new TastingSession { Title = "t" };
            context.TastingSessions.Add(session);
            await context.SaveChangesAsync();
            context.TastingNotes.AddRange(
                new TastingNote { TastingSessionId = session.Id, WhiskeyId = original.Id, BottleId = infinity.Id, Rating = 4 },
                new TastingNote { TastingSessionId = session.Id, WhiskeyId = original.Id, BottleId = regular.Id, Rating = 5 });
            await context.SaveChangesAsync();

            var service = new InfinityBottleMigrationService(context, MockEnvironment(webRoot));

            var shared = await service.FindSharedAsync();
            Assert.Single(shared);
            Assert.Equal(infinity.Id, shared[0].BottleId);

            var migrated = await service.MigrateAsync();

            Assert.Equal(1, migrated);

            var updatedInfinity = await context.Bottles.Include(b => b.Whiskey).SingleAsync(b => b.Id == infinity.Id);
            Assert.NotEqual(original.Id, updatedInfinity.WhiskeyId);
            Assert.Equal("Eagle Rare", updatedInfinity.Whiskey!.Name);
            Assert.Equal("Living Blend", updatedInfinity.Whiskey.Brand);
            Assert.NotEqual("orig.jpg", updatedInfinity.Whiskey.ImageFileName);
            Assert.True(File.Exists(Path.Combine(imagesDir, updatedInfinity.Whiskey.ImageFileName!)));
            Assert.True(File.Exists(Path.Combine(imagesDir, "orig.jpg")));

            // The regular bottle and the original whiskey are untouched
            var updatedRegular = await context.Bottles.SingleAsync(b => b.Id == regular.Id);
            Assert.Equal(original.Id, updatedRegular.WhiskeyId);

            // Only the infinity bottle's tasting note moved to the new whiskey
            var infinityNote = await context.TastingNotes.SingleAsync(n => n.BottleId == infinity.Id);
            var regularNote = await context.TastingNotes.SingleAsync(n => n.BottleId == regular.Id);
            Assert.Equal(updatedInfinity.WhiskeyId, infinityNote.WhiskeyId);
            Assert.Equal(original.Id, regularNote.WhiskeyId);

            // Idempotent: nothing left to migrate
            Assert.Empty(await service.FindSharedAsync());
            Assert.Equal(0, await service.MigrateAsync());
        }
        finally
        {
            Directory.Delete(webRoot, true);
        }
    }

    [Fact]
    public async Task FindShared_IgnoresInfinityBottlesWithTheirOwnWhiskey()
    {
        using var context = GetInMemoryContext();

        var blend = new Whiskey { Name = "House Blend", Brand = "Living Blend", Distillery = "Home Blend" };
        var other = new Whiskey { Name = "Other", Brand = "B", Distillery = "D" };
        context.Whiskies.AddRange(blend, other);
        await context.SaveChangesAsync();
        context.Bottles.AddRange(
            new Bottle { WhiskeyId = blend.Id, IsInfinityBottle = true },
            new Bottle { WhiskeyId = other.Id });
        await context.SaveChangesAsync();

        var service = new InfinityBottleMigrationService(context, MockEnvironment(Path.GetTempPath()));

        Assert.Empty(await service.FindSharedAsync());
    }

    [Fact]
    public async Task OnPostSingleBottle_CreatesSessionAndRedirectsWithBottleId()
    {
        using var context = GetInMemoryContext();

        var whiskey = new Whiskey { Name = "Blend", Brand = "B", Distillery = "D" };
        var col = new Collection { Name = "Bar" };
        context.Whiskies.Add(whiskey);
        context.Collections.Add(col);
        await context.SaveChangesAsync();
        context.CollectionMembers.Add(new CollectionMember { CollectionId = col.Id, UserId = "u1", Role = CollectionRole.Owner });
        var bottle = new Bottle { WhiskeyId = whiskey.Id, CollectionId = col.Id, IsInfinityBottle = true, Status = BottleStatus.Opened };
        context.Bottles.Add(bottle);
        await context.SaveChangesAsync();

        var pageModel = new CreateModel(context, new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        SetMockUser(pageModel, "u1");

        var result = await pageModel.OnPostSingleBottleAsync(bottle.Id);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("./Wizard", redirect.PageName);
        Assert.Equal(bottle.Id, redirect.RouteValues!["bottleId"]);
        Assert.Single(context.TastingSessions);
    }

    [Fact]
    public async Task OnPostSingleBottle_BottleInOtherCollection_ReturnsNotFound()
    {
        using var context = GetInMemoryContext();

        var whiskey = new Whiskey { Name = "Blend", Brand = "B", Distillery = "D" };
        var col = new Collection { Name = "Private" };
        context.Whiskies.Add(whiskey);
        context.Collections.Add(col);
        await context.SaveChangesAsync();
        var bottle = new Bottle { WhiskeyId = whiskey.Id, CollectionId = col.Id, IsInfinityBottle = true };
        context.Bottles.Add(bottle);
        await context.SaveChangesAsync();

        var pageModel = new CreateModel(context, new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        SetMockUser(pageModel, "intruder");

        var result = await pageModel.OnPostSingleBottleAsync(bottle.Id);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(context.TastingSessions);
    }

    [Fact]
    public async Task MigrateBottle_UnsharedInfinityBottle_GetsNewWhiskeyWithGivenName()
    {
        using var context = GetInMemoryContext();

        var redbreast = new Whiskey { Name = "Redbreast 12 Cask", Brand = "Redbreast", Distillery = "Midleton" };
        context.Whiskies.Add(redbreast);
        await context.SaveChangesAsync();
        var infinity = new Bottle { WhiskeyId = redbreast.Id, IsInfinityBottle = true, Status = BottleStatus.Opened };
        context.Bottles.Add(infinity);
        await context.SaveChangesAsync();

        var service = new InfinityBottleMigrationService(context, MockEnvironment(Path.GetTempPath()));

        Assert.Empty(await service.FindSharedAsync());
        Assert.Single(await service.FindAllAsync());

        Assert.True(await service.MigrateBottleAsync(infinity.Id, "Irish Infinity Blend"));

        var updated = await context.Bottles.Include(b => b.Whiskey).SingleAsync();
        Assert.NotEqual(redbreast.Id, updated.WhiskeyId);
        Assert.Equal("Irish Infinity Blend", updated.Whiskey!.Name);
        Assert.Equal("Redbreast 12 Cask", (await context.Whiskies.FindAsync(redbreast.Id))!.Name);
        Assert.False(await service.MigrateBottleAsync(9999));
    }
}
