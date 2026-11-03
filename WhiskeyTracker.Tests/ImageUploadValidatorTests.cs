using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Moq;
using WhiskeyTracker.Web.Data;
using WhiskeyTracker.Web.Pages.Whiskies;
using WhiskeyTracker.Web.Services;
using Xunit;

namespace WhiskeyTracker.Tests;

public class ImageUploadValidatorTests : TestBase
{
    private static IFormFile MockFile(string fileName, long length)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.Length).Returns(length);
        return mockFile.Object;
    }

    private class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private static HttpResponseMessage ImageResponse(int bytes, string mediaType = "image/jpeg")
    {
        var content = new ByteArrayContent(new byte[bytes]);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    [Theory]
    [InlineData("bottle.jpg")]
    [InlineData("bottle.JPEG")]
    [InlineData("bottle.png")]
    [InlineData("bottle.gif")]
    [InlineData("bottle.webp")]
    public void ValidateUpload_AcceptsImageExtensions(string fileName)
    {
        Assert.Null(ImageUploadValidator.ValidateUpload(MockFile(fileName, 1024)));
    }

    [Theory]
    [InlineData("payload.html")]
    [InlineData("payload.svg")]
    [InlineData("payload.js")]
    [InlineData("payload.jpg.html")]
    [InlineData("noextension")]
    public void ValidateUpload_RejectsNonImageExtensions(string fileName)
    {
        Assert.NotNull(ImageUploadValidator.ValidateUpload(MockFile(fileName, 1024)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ImageUploadValidator.MaxImageBytes + 1)]
    public void ValidateUpload_RejectsEmptyOrOversizedFiles(long length)
    {
        Assert.NotNull(ImageUploadValidator.ValidateUpload(MockFile("bottle.jpg", length)));
    }

    [Theory]
    [InlineData("https://lh3.googleusercontent.com/abc", true)]
    [InlineData("https://photoslibrary.googleapis.com/abc", true)]
    [InlineData("https://googleusercontent.com/abc", false)]
    [InlineData("https://attackergoogleusercontent.com/abc", false)]
    [InlineData("https://lh3.googleusercontent.com.evil.com/abc", false)]
    [InlineData("http://lh3.googleusercontent.com/abc", false)]
    [InlineData("not a url", false)]
    public void IsAllowedGooglePhotoUrl_OnlyAllowsHttpsGoogleSubdomains(string url, bool expected)
    {
        Assert.Equal(expected, ImageUploadValidator.IsAllowedGooglePhotoUrl(url));
    }

    [Fact]
    public async Task DownloadGooglePhotoAsync_ReturnsBytes_ForImageWithinLimit()
    {
        var bytes = await ImageUploadValidator.DownloadGooglePhotoAsync(
            "https://lh3.googleusercontent.com/abc", "token", new StubHandler(ImageResponse(2048)));

        Assert.NotNull(bytes);
        Assert.Equal(2048, bytes.Length);
    }

    [Fact]
    public async Task DownloadGooglePhotoAsync_ReturnsNull_WhenOverSizeLimit()
    {
        var bytes = await ImageUploadValidator.DownloadGooglePhotoAsync(
            "https://lh3.googleusercontent.com/abc", "token",
            new StubHandler(ImageResponse((int)ImageUploadValidator.MaxImageBytes + 1)));

        Assert.Null(bytes);
    }

    [Fact]
    public async Task DownloadGooglePhotoAsync_ReturnsNull_ForNonImageContent()
    {
        var bytes = await ImageUploadValidator.DownloadGooglePhotoAsync(
            "https://lh3.googleusercontent.com/abc", "token", new StubHandler(ImageResponse(100, "text/html")));

        Assert.Null(bytes);
    }

    [Fact]
    public async Task DownloadGooglePhotoAsync_ReturnsNull_ForRedirect()
    {
        var redirect = new HttpResponseMessage(HttpStatusCode.Found);
        redirect.Headers.Location = new Uri("http://169.254.169.254/");

        var bytes = await ImageUploadValidator.DownloadGooglePhotoAsync(
            "https://lh3.googleusercontent.com/abc", "token", new StubHandler(redirect));

        Assert.Null(bytes);
    }

    [Fact]
    public async Task Create_RejectsHtmlUpload_AndDoesNotSave()
    {
        using var context = GetInMemoryContext();
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(m => m.WebRootPath).Returns(Path.GetTempPath());

        var pageModel = new CreateModel(context, mockEnv.Object)
        {
            NewWhiskey = new Whiskey { Name = "Test Whiskey" },
            ImageUpload = MockFile("payload.html", 1024)
        };

        var result = await pageModel.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.ModelState.ContainsKey("ImageUpload"));
        Assert.False(await context.Whiskies.AnyAsync());
    }

    [Fact]
    public async Task Edit_RejectsOversizedUpload_AndKeepsExistingImage()
    {
        using var context = GetInMemoryContext();
        context.Whiskies.Add(new Whiskey { Id = 1, Name = "Test", ImageFileName = "old_image.jpg" });
        await context.SaveChangesAsync();

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(m => m.WebRootPath).Returns(Path.GetTempPath());

        var pageModel = new EditModel(context, mockEnv.Object)
        {
            Whiskey = (await context.Whiskies.FindAsync(1))!,
            ImageUpload = MockFile("big.jpg", ImageUploadValidator.MaxImageBytes + 1)
        };

        var result = await pageModel.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.ModelState.ContainsKey("ImageUpload"));
        Assert.Equal("old_image.jpg", pageModel.Whiskey.ImageFileName);
    }
}
