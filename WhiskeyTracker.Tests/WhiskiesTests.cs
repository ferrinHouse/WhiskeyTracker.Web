using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Moq;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Whiskies;
using Xunit;

namespace WhiskeyTracker.Tests;

public class WhiskiesTests : TestBase
{
    // Helpers removed - inherited from TestBase

    // --- INDEX TESTS ---
    [Fact]
    public async Task Index_ReturnsAllWhiskies_WhenNoSearch()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey { Name = "Ardbeg", Region = "Islay" });
        context.Whiskies.Add(new Whiskey { Name = "Macallan", Region = "Speyside" });
        await context.SaveChangesAsync();



        var legacyService = new WhiskeyTracker.Web.Services.LegacyMigrationService(context);
        var pageModel = new IndexModel(context, legacyService);
        SetMockUser(pageModel, "test-user");

        await pageModel.OnGetAsync();

        Assert.Equal(2, pageModel.Whiskies.Count);
        Assert.Contains(pageModel.Whiskies, w => w.Name == "Ardbeg");
    }

    [Fact]
    public async Task Index_FiltersBySearchString()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey { Name = "Ardbeg", Distillery = "Ardbeg" });
        context.Whiskies.Add(new Whiskey { Name = "Balvenie", Distillery = "Balvenie" });
        await context.SaveChangesAsync();



        var legacyService = new WhiskeyTracker.Web.Services.LegacyMigrationService(context);
        var pageModel = new IndexModel(context, legacyService) { SearchString = "Ard" };
        SetMockUser(pageModel, "test-user");

        await pageModel.OnGetAsync();

        Assert.Single(pageModel.Whiskies);
        Assert.Equal("Ardbeg", pageModel.Whiskies[0].Name);
    }

    [Fact]
    public async Task Index_FiltersByRegion()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey { Name = "A", Region = "Islay" });
        context.Whiskies.Add(new Whiskey { Name = "B", Region = "Speyside" });
        await context.SaveChangesAsync();



        var legacyService = new WhiskeyTracker.Web.Services.LegacyMigrationService(context);
        var pageModel = new IndexModel(context, legacyService) { WhiskeyRegion = "Islay" };
        SetMockUser(pageModel, "test-user");

        await pageModel.OnGetAsync();

        Assert.Single(pageModel.Whiskies);
        Assert.Equal("A", pageModel.Whiskies[0].Name);
    }

    // --- CREATE TESTS (With File Upload) ---
    [Fact]
    public async Task Create_UploadsFile_AndRedirects()
    {
        using var context = GetInMemoryContext();

        // Mock the Environment to use a temp folder
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(m => m.WebRootPath).Returns(Path.GetTempPath());

        // Mock the File Upload
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("test.jpg");
        mockFile.Setup(f => f.Length).Returns(1024);
        // We mock CopyToAsync to verify it was called
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var pageModel = new CreateModel(context, mockEnv.Object)
        {
            NewWhiskey = new Whiskey { Name = "Test Whiskey" },
            ImageUpload = mockFile.Object
        };

        var result = await pageModel.OnPostAsync();

        mockFile.Verify(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.IsType<RedirectToPageResult>(result);
        var whiskey = await context.Whiskies.FirstAsync();
        Assert.Equal("Test Whiskey", whiskey.Name);
        Assert.Contains("test.jpg", whiskey.ImageFileName); // Confirms filename was generated
    }

    [Fact]
    public async Task Create_WorksWithoutCaskType_AndRedirects()
    {
        using var context = GetInMemoryContext();

        // Mock the Environment
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(m => m.WebRootPath).Returns(Path.GetTempPath());

        var pageModel = new CreateModel(context, mockEnv.Object)
        {
            NewWhiskey = new Whiskey 
            { 
                Name = "No Cask Whiskey",
                Distillery = "Test Distillery",
                CaskType = null // Explicitly null
            }
        };

        var result = await pageModel.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var whiskey = await context.Whiskies.FirstAsync();
        Assert.Equal("No Cask Whiskey", whiskey.Name);
        Assert.Null(whiskey.CaskType);
    }

    [Fact]
    public void Create_OnGet_ReturnsPage()
    {
        using var context = GetInMemoryContext();
        var mockEnv = new Mock<IWebHostEnvironment>();
        var pageModel = new CreateModel(context, mockEnv.Object);

        pageModel.OnGet(); // OnGet for CreateModel returns void

        // No direct assertion on result type needed as OnGet returns void
    }

    [Fact]
    public async Task Create_OnPost_ReturnsPage_WhenModelStateInvalid()
    {
        using var context = GetInMemoryContext();
        var mockEnv = new Mock<IWebHostEnvironment>();
        var pageModel = new CreateModel(context, mockEnv.Object)
        {
            NewWhiskey = new Whiskey { Name = "Invalid" }
        };
        pageModel.ModelState.AddModelError("Error", "Sample Error");

        var result = await pageModel.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(context.Whiskies);
    }

    // --- DETAILS TESTS ---
    [Fact]
    public async Task Details_ReturnsNotFound_WhenIdIsNull()
    {
        using var context = GetInMemoryContext();
        var pageModel = new DetailsModel(context);
        var result = await pageModel.OnGetAsync(null);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_PopulatesData_WhenFound()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey { Id = 1, Name = "Test" });
        await context.SaveChangesAsync();



        var pageModel = new DetailsModel(context);
        SetMockUser(pageModel, "test-user");
        await pageModel.OnGetAsync(1);

        Assert.NotNull(pageModel.Whiskey);
        Assert.Equal("Test", pageModel.Whiskey.Name);
    }

    // --- DELETE TESTS ---

    [Fact]
    public async Task Delete_OnGet_ReturnsNotFound_ForNullId()
    {
        using var context = GetInMemoryContext();
        var pageModel = new DeleteModel(context);
        var result = await pageModel.OnGetAsync(null);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_OnGet_ReturnsNotFound_ForMissingWhiskey()
    {
        using var context = GetInMemoryContext();
        var pageModel = new DeleteModel(context);
        var result = await pageModel.OnGetAsync(999);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_OnGet_ReturnsPage_WithWhiskey()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey { Id = 1, Name = "To Delete" });
        await context.SaveChangesAsync();

        var pageModel = new DeleteModel(context);
        var result = await pageModel.OnGetAsync(1);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(pageModel.Whiskey);
    }

    [Fact]

    public async Task Delete_RemovesWhiskey_WhenFound()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey { Id = 1, Name = "To Delete" });
        await context.SaveChangesAsync();

        var pageModel = new DeleteModel(context);
        var result = await pageModel.OnPostAsync(1);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Empty(context.Whiskies);
    }

    [Fact]

    public async Task Delete_ReturnsNotFound_WhenWhiskeyNotFound()
    {
        // Arrange
        using var context = GetInMemoryContext();
        var pageModel = new DeleteModel(context);

        // Act
        var result = await pageModel.OnPostAsync(999); // A non-existent ID

        // Assert
        Assert.IsType<NotFoundResult>(result);

    }

    [Fact]
    public async Task Delete_OnPost_ReturnsNotFound_ForNullId()
    {
        using var context = GetInMemoryContext();
        var pageModel = new DeleteModel(context);
        var result = await pageModel.OnPostAsync(null);
        Assert.IsType<NotFoundResult>(result);
    }

    // --- EDIT TESTS ---
    [Fact]
    public async Task Edit_OnGet_ReturnsNotFound_ForNullId()
    {
        // Arrange
        using var context = GetInMemoryContext();
        var mockEnv = new Mock<IWebHostEnvironment>();
        var pageModel = new EditModel(context, mockEnv.Object);

        // Act
        var result = await pageModel.OnGetAsync(null);

        // Assert
        Assert.IsType<NotFoundResult>(result);

    }

    [Fact]
    public async Task Edit_OnPost_ReturnsPage_WhenModelStateInvalid()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey { Id = 1, Name = "Original" });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var mockEnv = new Mock<IWebHostEnvironment>();
        var pageModel = new EditModel(context, mockEnv.Object)
        {
            Whiskey = new Whiskey { Id = 1, Name = "Updated" }
        };
        pageModel.ModelState.AddModelError("Error", "Sample Error");

        var result = await pageModel.OnPostAsync();

        Assert.IsType<PageResult>(result);
        var whiskey = await context.Whiskies.FindAsync(1);
        Assert.NotNull(whiskey);
        Assert.Equal("Original", whiskey.Name);
    }

    [Fact]
    public async Task Edit_OnGet_ReturnsNotFound_ForMissingWhiskey()
    {
        // Arrange
        using var context = GetInMemoryContext();
        var mockEnv = new Mock<IWebHostEnvironment>();
        var pageModel = new EditModel(context, mockEnv.Object);

        // Act
        var result = await pageModel.OnGetAsync(123); // Non-existent ID

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_OnGet_ReturnsPage_WithWhiskey()
    {
        // Arrange
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey { Id = 1, Name = "Test" });
        await context.SaveChangesAsync();
        var mockEnv = new Mock<IWebHostEnvironment>();
        var pageModel = new EditModel(context, mockEnv.Object);
        SetMockUser(pageModel, "test-user");

        // Act
        var result = await pageModel.OnGetAsync(1);

        // Assert
        Assert.IsType<PageResult>(result);
        Assert.NotNull(pageModel.Whiskey);
        Assert.NotNull(pageModel.Whiskey);
        Assert.Equal(1, pageModel.Whiskey.Id);
    }

    [Fact]
    public async Task Edit_OnPost_UpdatesWhiskey()
    {
        // Arrange
        using var context = GetInMemoryContext();
        var originalWhiskey = new Whiskey { Id = 1, Name = "Original Name" };
        context.Whiskies.Add(originalWhiskey);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var mockEnv = new Mock<IWebHostEnvironment>();
        var pageModel = new EditModel(context, mockEnv.Object)
        {
            Whiskey = new Whiskey { Id = 1, Name = "Updated Name" }
        };

        // Act
        var result = await pageModel.OnPostAsync();

        // Assert
        Assert.IsType<RedirectToPageResult>(result);
        var updatedWhiskey = await context.Whiskies.FindAsync(1);
        Assert.Equal("Updated Name", updatedWhiskey!.Name);
    }

    [Fact]
    public async Task Edit_OnPost_UpdatesGeneralNotes()
    {
        // Arrange
        using var context = GetInMemoryContext();
        var originalWhiskey = new Whiskey { Id = 1, Name = "Original", GeneralNotes = "Old Notes" };
        context.Whiskies.Add(originalWhiskey);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var mockEnv = new Mock<IWebHostEnvironment>();
        var pageModel = new EditModel(context, mockEnv.Object)
        {
            Whiskey = new Whiskey { Id = 1, Name = "Original", GeneralNotes = "New Fresh Notes" }
        };

        // Act
        var result = await pageModel.OnPostAsync();

        // Assert
        Assert.IsType<RedirectToPageResult>(result);
        var updatedWhiskey = await context.Whiskies.FindAsync(1);
        Assert.Equal("New Fresh Notes", updatedWhiskey!.GeneralNotes);
    }

    [Fact]
    public async Task Edit_OnPost_HandlesConcurrencyError()
    {
        // Arrange
        using var context = GetInMemoryContext();
        var mockEnv = new Mock<IWebHostEnvironment>();
        var pageModel = new EditModel(context, mockEnv.Object)
        {
            Whiskey = new Whiskey { Id = 1, Name = "No Whiskey For You" } // This whiskey doesn't exist
        };

        // Act
        var result = await pageModel.OnPostAsync();

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_OnPost_HandlesFileUpload()
    {
        // Arrange
        using var context = GetInMemoryContext();
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var imagesPath = Path.Combine(tempPath, "images");
        Directory.CreateDirectory(imagesPath);
        var oldFileName = "old_image.jpg";
        var oldFilePath = Path.Combine(imagesPath, oldFileName);
        File.WriteAllText(oldFilePath, "old image content");

        context.Whiskies.Add(new Whiskey { Id = 1, Name = "Test", ImageFileName = oldFileName });
        await context.SaveChangesAsync();

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(m => m.WebRootPath).Returns(tempPath);

        var mockFile = new Mock<IFormFile>();
        var newFileName = "new_image.jpg";
        mockFile.Setup(f => f.FileName).Returns(newFileName);
        mockFile.Setup(f => f.Length).Returns(1024);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream stream, CancellationToken token) =>
            {
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write("new image content");
                }
                return Task.CompletedTask;
            });

        var pageModel = new EditModel(context, mockEnv.Object)
        {
            Whiskey = (await context.Whiskies.FindAsync(1))!,
            ImageUpload = mockFile.Object
        };

        // Act
        var result = await pageModel.OnPostAsync();

        // Assert
        Assert.IsType<RedirectToPageResult>(result);
        var updatedWhiskey = await context.Whiskies.FindAsync(1);
        Assert.NotNull(updatedWhiskey);
        Assert.NotNull(updatedWhiskey.ImageFileName);
        Assert.NotEqual(oldFileName, updatedWhiskey.ImageFileName);
        Assert.Contains(newFileName, updatedWhiskey.ImageFileName);
        Assert.False(File.Exists(oldFilePath));
        Assert.True(File.Exists(Path.Combine(tempPath, "images", updatedWhiskey.ImageFileName)));

        // Clean up
        Directory.Delete(tempPath, true);
    }
}