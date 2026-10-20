using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Bottles;
using Moq;
using Xunit;

namespace WhiskeyTracker.Tests;

public class InfinityBottleListTests : TestBase
{
    private static IWebHostEnvironment MockEnvironment(string webRoot)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns(webRoot);
        return env.Object;
    }

    [Fact]
    public async Task Index_OnGetAsync_ReturnsChallenge_WhenUnauthenticated()
    {
        using var context = GetInMemoryContext();
        var pageModel = new IndexModel(context);
        // Do not set mock user (userId will be null)
        pageModel.PageContext = new PageContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        var result = await pageModel.OnGetAsync();

        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task Index_OnGetAsync_LoadsOnlyUserInfinityBottles_AndCalculatesStats()
    {
        using var context = GetInMemoryContext();
        
        // Setup User A and User B collections
        var colA = new Collection { Name = "User A Bar" };
        var colB = new Collection { Name = "User B Bar" };
        context.Collections.AddRange(colA, colB);
        await context.SaveChangesAsync();

        context.CollectionMembers.AddRange(
            new CollectionMember { CollectionId = colA.Id, UserId = "user-a", Role = CollectionRole.Owner },
            new CollectionMember { CollectionId = colB.Id, UserId = "user-b", Role = CollectionRole.Owner }
        );

        var whiskey1 = new Whiskey { Name = "User A Infinity 1", Brand = "House" };
        var whiskey2 = new Whiskey { Name = "User A Infinity 2", Brand = "Solera" };
        var whiskey3 = new Whiskey { Name = "User A Standard Bottle", Brand = "Retail" };
        var whiskey4 = new Whiskey { Name = "User B Infinity", Brand = "Other" };
        context.Whiskies.AddRange(whiskey1, whiskey2, whiskey3, whiskey4);
        await context.SaveChangesAsync();

        // Add bottles
        var bottle1 = new Bottle
        {
            Whiskey = whiskey1,
            CollectionId = colA.Id,
            IsInfinityBottle = true,
            CapacityMl = 750,
            CurrentVolumeMl = 300,
            Status = BottleStatus.Opened
        };
        var bottle2 = new Bottle
        {
            Whiskey = whiskey2,
            CollectionId = colA.Id,
            IsInfinityBottle = true,
            CapacityMl = 1000,
            CurrentVolumeMl = 500,
            Status = BottleStatus.Opened
        };
        var regularBottle = new Bottle
        {
            Whiskey = whiskey3,
            CollectionId = colA.Id,
            IsInfinityBottle = false, // Not infinity
            CapacityMl = 750,
            CurrentVolumeMl = 750,
            Status = BottleStatus.Full
        };
        var otherUserBottle = new Bottle
        {
            Whiskey = whiskey4,
            CollectionId = colB.Id, // Belonging to user B
            IsInfinityBottle = true,
            CapacityMl = 750,
            CurrentVolumeMl = 400,
            Status = BottleStatus.Opened
        };
        context.Bottles.AddRange(bottle1, bottle2, regularBottle, otherUserBottle);
        await context.SaveChangesAsync();

        // Add a blend component to bottle1
        context.BlendComponents.Add(new BlendComponent
        {
            InfinityBottleId = bottle1.Id,
            AmountAddedMl = 60,
            DateAdded = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        await context.SaveChangesAsync();

        var pageModel = new IndexModel(context);
        SetMockUser(pageModel, "user-a");

        var result = await pageModel.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal(2, pageModel.InfinityBottles.Count);
        Assert.Equal(2, pageModel.TotalBottlesCount);
        Assert.Equal(800, pageModel.TotalVolumeMl); // 300 + 500
        Assert.Equal(1, pageModel.TotalAdditionsCount);

        var item1 = pageModel.InfinityBottles.FirstOrDefault(b => b.BottleId == bottle1.Id);
        Assert.NotNull(item1);
        Assert.Equal("User A Infinity 1", item1.WhiskeyName);
        Assert.Equal(1, item1.ComponentCount);
        Assert.Equal(60, item1.TotalMlAdded);
        Assert.Equal(40.0, item1.FillPercentage); // 300 / 750 * 100
    }

    [Fact]
    public async Task Index_OnGetAsync_FiltersByCollection_AndStatus()
    {
        using var context = GetInMemoryContext();

        var col1 = new Collection { Name = "Main Bar" };
        var col2 = new Collection { Name = "Basement Bar" };
        context.Collections.AddRange(col1, col2);
        await context.SaveChangesAsync();

        context.CollectionMembers.AddRange(
            new CollectionMember { CollectionId = col1.Id, UserId = "user-1", Role = CollectionRole.Owner },
            new CollectionMember { CollectionId = col2.Id, UserId = "user-1", Role = CollectionRole.Owner }
        );

        var w1 = new Whiskey { Name = "Open Main Blend" };
        var w2 = new Whiskey { Name = "Empty Main Blend" };
        var w3 = new Whiskey { Name = "Open Basement Blend" };
        context.Whiskies.AddRange(w1, w2, w3);
        await context.SaveChangesAsync();

        context.Bottles.AddRange(
            new Bottle { Whiskey = w1, CollectionId = col1.Id, IsInfinityBottle = true, Status = BottleStatus.Opened, CapacityMl = 750, CurrentVolumeMl = 350 },
            new Bottle { Whiskey = w2, CollectionId = col1.Id, IsInfinityBottle = true, Status = BottleStatus.Empty, CapacityMl = 750, CurrentVolumeMl = 0 },
            new Bottle { Whiskey = w3, CollectionId = col2.Id, IsInfinityBottle = true, Status = BottleStatus.Opened, CapacityMl = 750, CurrentVolumeMl = 200 }
        );
        await context.SaveChangesAsync();

        var pageModel = new IndexModel(context)
        {
            CollectionId = col1.Id,
            Status = BottleStatus.Opened
        };
        SetMockUser(pageModel, "user-1");

        var result = await pageModel.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.Single(pageModel.InfinityBottles);
        Assert.Equal("Open Main Blend", pageModel.InfinityBottles[0].WhiskeyName);
    }

    [Fact]
    public async Task Create_OnPostAsync_CreatesNewWhiskeyAndInfinityBottle()
    {
        using var context = GetInMemoryContext();

        var col = new Collection { Name = "My Tasting Room" };
        context.Collections.Add(col);
        await context.SaveChangesAsync();

        context.CollectionMembers.Add(new CollectionMember
        {
            CollectionId = col.Id,
            UserId = "blender-user",
            Role = CollectionRole.Owner
        });
        await context.SaveChangesAsync();

        var pageModel = new CreateModel(context, MockEnvironment(Path.GetTempPath()))
        {
            Input = new CreateModel.CreateInfinityBottleInput
            {
                Name = "Master Peat Blend",
                Brand = "Custom Decanter",
                Type = "Peated Blend",
                Region = "Islay Style",
                Description = "Heavily peated experimental solera",
                CollectionId = col.Id,
                CapacityMl = 1000,
                CurrentVolumeMl = 150,
                Status = BottleStatus.Opened
            }
        };
        SetMockUser(pageModel, "blender-user");

        var result = await pageModel.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Bottles/Infinity", redirect.PageName);

        var createdBottle = await context.Bottles.Include(b => b.Whiskey).FirstOrDefaultAsync();
        Assert.NotNull(createdBottle);
        Assert.True(createdBottle.IsInfinityBottle);
        Assert.Equal(1000, createdBottle.CapacityMl);
        Assert.Equal(150, createdBottle.CurrentVolumeMl);
        Assert.Equal(col.Id, createdBottle.CollectionId);
        Assert.Equal("blender-user", createdBottle.UserId);
        Assert.NotNull(createdBottle.Whiskey);
        Assert.Equal("Master Peat Blend", createdBottle.Whiskey.Name);
        Assert.Equal("Custom Decanter", createdBottle.Whiskey.Brand);
        Assert.Equal("Heavily peated experimental solera", createdBottle.Whiskey.GeneralNotes);
    }

    [Fact]
    public async Task Create_OnPostAsync_CopiesImageFromLibraryWhiskey_ToNewWhiskey()
    {
        using var context = GetInMemoryContext();

        var webRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var imagesDir = Path.Combine(webRoot, "images");
        Directory.CreateDirectory(imagesDir);
        File.WriteAllText(Path.Combine(imagesDir, "source.jpg"), "fake-image");

        try
        {
            var col = new Collection { Name = "Main Collection" };
            var source = new Whiskey { Name = "Reference Bottle", Brand = "Reference Distillery", ImageFileName = "source.jpg" };
            context.Collections.Add(col);
            context.Whiskies.Add(source);
            await context.SaveChangesAsync();

            context.CollectionMembers.Add(new CollectionMember
            {
                CollectionId = col.Id,
                UserId = "user-123",
                Role = CollectionRole.Owner
            });
            await context.SaveChangesAsync();

            var pageModel = new CreateModel(context, MockEnvironment(webRoot))
            {
                Input = new CreateModel.CreateInfinityBottleInput
                {
                    Name = "House Blend",
                    CopyImageFromWhiskeyId = source.Id,
                    CollectionId = col.Id,
                    CapacityMl = 750,
                    CurrentVolumeMl = 0,
                    Status = BottleStatus.Opened
                }
            };
            SetMockUser(pageModel, "user-123");

            var result = await pageModel.OnPostAsync();

            Assert.IsType<RedirectToPageResult>(result);

            var bottle = await context.Bottles.Include(b => b.Whiskey).SingleAsync();
            Assert.True(bottle.IsInfinityBottle);
            Assert.NotEqual(source.Id, bottle.WhiskeyId); // Separate whiskey record
            Assert.Equal("House Blend", bottle.Whiskey!.Name);
            Assert.Equal(2, await context.Whiskies.CountAsync());

            // Image is a distinct copy, so editing/deleting one never affects the other
            Assert.NotNull(bottle.Whiskey.ImageFileName);
            Assert.NotEqual("source.jpg", bottle.Whiskey.ImageFileName);
            Assert.EndsWith(".jpg", bottle.Whiskey.ImageFileName);
            Assert.True(File.Exists(Path.Combine(imagesDir, bottle.Whiskey.ImageFileName)));
            Assert.True(File.Exists(Path.Combine(imagesDir, "source.jpg")));
        }
        finally
        {
            Directory.Delete(webRoot, true);
        }
    }

    [Fact]
    public async Task Create_OnPostAsync_RejectsUnauthorizedCollection()
    {
        using var context = GetInMemoryContext();

        var col = new Collection { Name = "Private Collection" };
        context.Collections.Add(col);
        await context.SaveChangesAsync();

        // User is NOT added as a member of col

        var pageModel = new CreateModel(context, MockEnvironment(Path.GetTempPath()))
        {
            Input = new CreateModel.CreateInfinityBottleInput
            {
                Name = "Unauthorized Blend",
                CollectionId = col.Id,
                CapacityMl = 750,
                CurrentVolumeMl = 0
            }
        };
        SetMockUser(pageModel, "intruder");

        var result = await pageModel.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        Assert.Contains("Input.CollectionId", pageModel.ModelState.Keys);
        Assert.Empty(context.Bottles);
    }
}
