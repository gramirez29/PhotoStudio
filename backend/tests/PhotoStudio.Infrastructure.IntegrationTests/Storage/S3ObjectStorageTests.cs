using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Storage;
using PhotoStudio.Infrastructure.Storage;

namespace PhotoStudio.Infrastructure.IntegrationTests.Storage;

/// <summary>
/// Tests of <see cref="S3ObjectStorage"/>. The signed addresses are checked for real against an S3-compatible server (the one
/// of <c>docker-compose.yml</c>, or the one named by the <c>STORAGE_TEST_*</c> variables); they skip when none answers.
/// </summary>
/// <param name="fixture">The storage under test.</param>
public sealed class S3ObjectStorageTests(StorageFixture fixture) : IClassFixture<StorageFixture>, IDisposable
{
    private static readonly byte[] Photo = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8];

    private readonly string _prefix = $"tests/{Guid.NewGuid():N}";
    private readonly MutableTimeProvider _clock = new(DateTimeOffset.UtcNow);
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false });
    private S3ObjectStorage? _storage;

    /// <summary>
    /// Releases the clients.
    /// </summary>
    public void Dispose()
    {
        _storage?.Dispose();
        _http.Dispose();
    }

    /// <summary>
    /// An upload address takes the photo with the content type it was signed for, and the object is then there with its size and type.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Upload_WithTheSignedAddress_StoresTheObject()
    {
        Assert.SkipUnless(fixture.IsAvailable, "The object storage is not reachable; run 'docker compose up -d storage' in the backend folder.");
        var storage = Storage();
        var key = $"{_prefix}/proof.jpg";
        var url = await storage.CreateUploadUrlAsync(key, "image/jpeg", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        var response = await Put(url, "image/jpeg", Photo);
        var info = await storage.GetInfoAsync(key, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        url.RequiredHeaders["Content-Type"].ShouldBe("image/jpeg");
        info.ShouldBe(new StoredObjectInfo(Photo.Length, "image/jpeg"));
        await storage.DeleteAsync(key, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The content type is part of the signature: an upload with another one is refused, so an address for a JPEG cannot take anything else.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Upload_WithAnotherContentType_IsRefused()
    {
        Assert.SkipUnless(fixture.IsAvailable, "The object storage is not reachable; run 'docker compose up -d storage' in the backend folder.");
        var storage = Storage();
        var key = $"{_prefix}/not-a-photo.jpg";
        var url = await storage.CreateUploadUrlAsync(key, "image/jpeg", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        var response = await Put(url, "text/html", Photo);

        response.IsSuccessStatusCode.ShouldBeFalse();
        (await storage.GetInfoAsync(key, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// An address cannot be used for another object: changing the key invalidates the signature.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Upload_ToAnotherKey_IsRefused()
    {
        Assert.SkipUnless(fixture.IsAvailable, "The object storage is not reachable; run 'docker compose up -d storage' in the backend folder.");
        var storage = Storage();
        var url = await storage.CreateUploadUrlAsync($"{_prefix}/mine.jpg", "image/jpeg", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        var forged = new PresignedUrl(new Uri(url.Url.ToString().Replace("mine.jpg", "other.jpg", StringComparison.Ordinal)), url.ExpiresAt, url.RequiredHeaders);

        var response = await Put(forged, "image/jpeg", Photo);

        response.IsSuccessStatusCode.ShouldBeFalse();
        (await storage.GetInfoAsync($"{_prefix}/other.jpg", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// A download address gives the object back and asks the browser to save it with a safe name.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Download_WithTheSignedAddress_ReturnsTheObjectAsAnAttachment()
    {
        Assert.SkipUnless(fixture.IsAvailable, "The object storage is not reachable; run 'docker compose up -d storage' in the backend folder.");
        var storage = Storage();
        var key = $"{_prefix}/final.jpg";
        await Put(await storage.CreateUploadUrlAsync(key, "image/jpeg", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken), "image/jpeg", Photo);
        var url = await storage.CreateDownloadUrlAsync(key, TimeSpan.FromMinutes(5), "Sesión 01.jpg", TestContext.Current.CancellationToken);

        var response = await _http.GetAsync(url.Url, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).ShouldBe(Photo);
        response.Content.Headers.ContentDisposition.ShouldNotBeNull();
        response.Content.Headers.ContentDisposition.DispositionType.ShouldBe("attachment");
        response.Content.Headers.ContentDisposition.FileNameStar.ShouldBe("Sesión 01.jpg");
        await storage.DeleteAsync(key, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// An address that expired is refused: this is the window in which a leaked address is useful.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Download_AfterTheAddressExpired_IsRefused()
    {
        Assert.SkipUnless(fixture.IsAvailable, "The object storage is not reachable; run 'docker compose up -d storage' in the backend folder.");
        // The clock is in the past, so the address is born already expired.
        _clock.SetNow(DateTimeOffset.UtcNow.AddHours(-2));
        var storage = Storage();
        var key = $"{_prefix}/expired.jpg";
        var url = await storage.CreateDownloadUrlAsync(key, TimeSpan.FromMinutes(5), null, TestContext.Current.CancellationToken);

        var response = await _http.GetAsync(url.Url, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    /// <summary>
    /// The size of an object that does not exist is simply "not there", not an error.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task GetInfo_OfAMissingObject_ReturnsNull()
    {
        Assert.SkipUnless(fixture.IsAvailable, "The object storage is not reachable; run 'docker compose up -d storage' in the backend folder.");

        (await Storage().GetInfoAsync($"{_prefix}/missing.jpg", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// Deleting removes the object, and deleting what is not there is not an error.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Delete_RemovesTheObject_AndIsIdempotent()
    {
        Assert.SkipUnless(fixture.IsAvailable, "The object storage is not reachable; run 'docker compose up -d storage' in the backend folder.");
        var storage = Storage();
        var key = $"{_prefix}/gone.jpg";
        await Put(await storage.CreateUploadUrlAsync(key, "image/jpeg", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken), "image/jpeg", Photo);

        await storage.DeleteAsync(key, TestContext.Current.CancellationToken);
        await storage.DeleteAsync(key, TestContext.Current.CancellationToken);

        (await storage.GetInfoAsync(key, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// Deleting many removes them all in one call, and leaves the others.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task DeleteMany_RemovesEveryKeyAndOnlyThose()
    {
        Assert.SkipUnless(fixture.IsAvailable, "The object storage is not reachable; run 'docker compose up -d storage' in the backend folder.");
        var storage = Storage();
        var doomed = new[] { $"{_prefix}/a.jpg", $"{_prefix}/b.jpg", $"{_prefix}/c.jpg" };
        var kept = $"{_prefix}/kept.jpg";
        foreach (var key in doomed.Append(kept))
        {
            await Put(await storage.CreateUploadUrlAsync(key, "image/jpeg", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken), "image/jpeg", Photo);
        }

        await storage.DeleteManyAsync(doomed, TestContext.Current.CancellationToken);

        foreach (var key in doomed)
        {
            (await storage.GetInfoAsync(key, TestContext.Current.CancellationToken)).ShouldBeNull();
        }

        (await storage.GetInfoAsync(kept, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await storage.DeleteAsync(kept, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A key that could climb out of its prefix or confuse the storage is refused before anything is signed or sent.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("/absolute.jpg")]
    [InlineData("a/../b.jpg")]
    [InlineData("a//b.jpg")]
    [InlineData("a/b/")]
    [InlineData("a b.jpg")]
    [InlineData("a?b=c.jpg")]
    public async Task EveryOperation_RefusesAnInvalidKey(string key)
    {
        var storage = Storage();

        await Should.ThrowAsync<ArgumentException>(() => storage.CreateUploadUrlAsync(key, "image/jpeg", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentException>(() => storage.CreateDownloadUrlAsync(key, TimeSpan.FromMinutes(1), null, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentException>(() => storage.GetInfoAsync(key, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentException>(() => storage.DeleteAsync(key, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentException>(() => storage.DeleteManyAsync([$"{_prefix}/fine.jpg", key], TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// An address cannot live longer than the limit, nor be born expired: a long-lived address is a leaked one waiting to happen.
    /// </summary>
    /// <param name="minutes">Requested lifetime in minutes.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(16)]
    public async Task Upload_RefusesALifetimeOutOfBounds(int minutes) =>
        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => Storage().CreateUploadUrlAsync($"{_prefix}/x.jpg", "image/jpeg", TimeSpan.FromMinutes(minutes), TestContext.Current.CancellationToken));

    /// <summary>
    /// A download address has a lower limit than an hour.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task Download_RefusesALifetimeOverTheLimit() =>
        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => Storage().CreateDownloadUrlAsync($"{_prefix}/x.jpg", ObjectStorageLimits.MaxDownloadLifetime.Add(TimeSpan.FromSeconds(1)), null, TestContext.Current.CancellationToken));

    /// <summary>
    /// The file name offered to the browser loses what could break the header or climb out of a folder.
    /// </summary>
    /// <param name="name">The name asked for.</param>
    /// <param name="ascii">What the plain filename becomes.</param>
    [Theory]
    [InlineData("foto.jpg", "foto.jpg")]
    [InlineData("a/b\\c.jpg", "a_b_c.jpg")]
    [InlineData("x\"; filename=\"y.jpg", "x__ filename=_y.jpg")]
    [InlineData("a\r\nSet-Cookie: x.jpg", "a__Set-Cookie: x.jpg")]
    [InlineData("   ", "download")]
    public void BuildContentDisposition_KeepsTheNameSafe(string name, string ascii)
    {
        var header = S3ObjectStorage.BuildContentDisposition(name);

        header.ShouldStartWith($"attachment; filename=\"{ascii}\"");
        header.ShouldNotContain("\r");
        header.ShouldNotContain("\n");
    }

    /// <summary>
    /// The stand-in that is used when the storage is not configured answers "unavailable" to everything.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task WithoutConfiguration_EveryOperationIsUnavailable()
    {
        var storage = new UnavailableObjectStorage();

        await Should.ThrowAsync<StorageUnavailableException>(() => storage.CreateUploadUrlAsync("a.jpg", "image/jpeg", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<StorageUnavailableException>(() => storage.CreateDownloadUrlAsync("a.jpg", TimeSpan.FromMinutes(1), null, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<StorageUnavailableException>(() => storage.GetInfoAsync("a.jpg", TestContext.Current.CancellationToken));
        await Should.ThrowAsync<StorageUnavailableException>(() => storage.DeleteAsync("a.jpg", TestContext.Current.CancellationToken));
        await Should.ThrowAsync<StorageUnavailableException>(() => storage.DeleteManyAsync(["a.jpg"], TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// When the storage cannot be reached the error is "unavailable" and says nothing about the key or the address.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task WhenTheStorageDoesNotAnswer_TheErrorIsUnavailableAndLeaksNothing()
    {
        var options = ObjectStorageOptions.Create("http://127.0.0.1:1", "photostudio-test", "key", "secret");
        using var storage = new S3ObjectStorage(options, TimeProvider.System, NullLogger<S3ObjectStorage>.Instance);

        var error = await Should.ThrowAsync<StorageUnavailableException>(() => storage.GetInfoAsync("secret-folder/secret-name.jpg", TestContext.Current.CancellationToken));

        error.Message.ShouldNotContain("secret-folder");
        error.Message.ShouldNotContain("127.0.0.1");
    }

    /// <summary>
    /// Creates the storage under test once per test.
    /// </summary>
    /// <returns>The storage.</returns>
    private S3ObjectStorage Storage() => _storage ??= new S3ObjectStorage(fixture.Options, _clock, NullLogger<S3ObjectStorage>.Instance);

    /// <summary>
    /// Uploads bytes to a signed address.
    /// </summary>
    /// <param name="url">The signed address.</param>
    /// <param name="contentType">The content type to send.</param>
    /// <param name="content">The bytes.</param>
    /// <returns>The response.</returns>
    private async Task<HttpResponseMessage> Put(PresignedUrl url, string contentType, byte[] content)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url.Url) { Content = new ByteArrayContent(content) };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return await _http.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
