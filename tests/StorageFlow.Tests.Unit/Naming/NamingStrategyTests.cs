using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Core.Naming;

namespace StorageFlow.Tests.Unit.Naming;

public class GuidNamingStrategyTests
{
    [Fact]
    public async Task GenerateAsync_ReturnsGuidWithExtension()
    {
        var strategy = new GuidNamingStrategy();
        var ctx = new FileNamingContext { OriginalFileName = "photo.jpg" };

        var key = await strategy.GenerateAsync(ctx);

        Assert.EndsWith(".jpg", key);
        Assert.True(Guid.TryParse(Path.GetFileNameWithoutExtension(key), out _));
    }

    [Fact]
    public async Task GenerateAsync_ProducesUniqueKeys()
    {
        var strategy = new GuidNamingStrategy();
        var ctx = new FileNamingContext { OriginalFileName = "photo.jpg" };

        var key1 = await strategy.GenerateAsync(ctx);
        var key2 = await strategy.GenerateAsync(ctx);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public async Task GenerateAsync_PreservesExtensionCase()
    {
        var strategy = new GuidNamingStrategy();
        var ctx = new FileNamingContext { OriginalFileName = "PHOTO.PNG" };

        var key = await strategy.GenerateAsync(ctx);

        Assert.EndsWith(".PNG", key);
    }
}

public class SeoNamingStrategyTests
{
    [Fact]
    public async Task GenerateAsync_SlugifiesFileName()
    {
        var strategy = new SeoNamingStrategy();
        var ctx = new FileNamingContext { OriginalFileName = "My Product Image!.jpg" };

        var key = await strategy.GenerateAsync(ctx);

        Assert.StartsWith("my-product-image-", key);
        Assert.EndsWith(".jpg", key);
    }

    [Fact]
    public async Task GenerateAsync_AppendsShortenedGuidForUniqueness()
    {
        var strategy = new SeoNamingStrategy();
        var ctx = new FileNamingContext { OriginalFileName = "photo.jpg" };

        var key1 = await strategy.GenerateAsync(ctx);
        var key2 = await strategy.GenerateAsync(ctx);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public async Task GenerateAsync_HandlesSpecialCharacters()
    {
        var strategy = new SeoNamingStrategy();
        var ctx = new FileNamingContext { OriginalFileName = "Ürün Görseli.png" };

        var key = await strategy.GenerateAsync(ctx);

        Assert.DoesNotContain("ü", key);
        Assert.DoesNotContain("ö", key);
        Assert.EndsWith(".png", key);
    }

    [Fact]
    public async Task GenerateAsync_WhenFileNameIsEmpty_UsesFileFallback()
    {
        var strategy = new SeoNamingStrategy();
        var ctx = new FileNamingContext { OriginalFileName = "   .jpg" };

        var key = await strategy.GenerateAsync(ctx);

        Assert.StartsWith("file-", key);
    }

    [Fact]
    public async Task GenerateAsync_RemovesConsecutiveHyphens()
    {
        var strategy = new SeoNamingStrategy();
        var ctx = new FileNamingContext { OriginalFileName = "hello---world.jpg" };

        var key = await strategy.GenerateAsync(ctx);

        Assert.DoesNotContain("---", key);
    }
}

public class PatternNamingStrategyTests
{
    [Fact]
    public async Task GenerateAsync_ReplacesYearToken()
    {
        var strategy = new PatternNamingStrategy();
        var now = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);
        var ctx = new FileNamingContext
        {
            OriginalFileName = "photo.jpg",
            Pattern = "{yyyy}",
            UploadedAt = now
        };

        var key = await strategy.GenerateAsync(ctx);

        Assert.Equal("2026", key);
    }

    [Fact]
    public async Task GenerateAsync_ReplacesAllTokens()
    {
        var strategy = new PatternNamingStrategy();
        var now = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);
        var ctx = new FileNamingContext
        {
            OriginalFileName = "my-photo.jpg",
            Pattern = "{yyyy}/{MM}/{dd}/{slug}-{guid}{ext}",
            UploadedAt = now
        };

        var key = await strategy.GenerateAsync(ctx);

        Assert.StartsWith("2026/06/10/my-photo-", key);
        Assert.EndsWith(".jpg", key);
    }

    [Fact]
    public async Task GenerateAsync_TimestampTokenProducesUnixSeconds()
    {
        var strategy = new PatternNamingStrategy();
        var now = DateTimeOffset.UtcNow;
        var ctx = new FileNamingContext
        {
            OriginalFileName = "file.zip",
            Pattern = "{timestamp}",
            UploadedAt = now
        };

        var key = await strategy.GenerateAsync(ctx);

        Assert.True(long.TryParse(key, out _));
    }

    [Fact]
    public async Task GenerateAsync_ThrowsWhenNoPatternProvided()
    {
        var strategy = new PatternNamingStrategy();
        var ctx = new FileNamingContext { OriginalFileName = "file.jpg" };

        await Assert.ThrowsAsync<StorageFlow.Abstractions.Exceptions.StorageNamingException>(
            () => strategy.GenerateAsync(ctx));
    }

    [Fact]
    public async Task GenerateAsync_ThrowsOnUnknownToken()
    {
        var strategy = new PatternNamingStrategy();
        var ctx = new FileNamingContext { OriginalFileName = "file.jpg", Pattern = "{unknown}" };

        await Assert.ThrowsAsync<StorageFlow.Abstractions.Exceptions.StorageNamingException>(
            () => strategy.GenerateAsync(ctx));
    }

    [Fact]
    public async Task GenerateAsync_DefaultPatternUsedWhenContextPatternIsNull()
    {
        var strategy = new PatternNamingStrategy("{yyyy}/{guid}{ext}");
        var ctx = new FileNamingContext
        {
            OriginalFileName = "file.jpg",
            UploadedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        var key = await strategy.GenerateAsync(ctx);

        Assert.StartsWith("2026/", key);
        Assert.EndsWith(".jpg", key);
    }
}

public class ObjectKeyValidatorTests
{
    private readonly ObjectKeyValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/absolute/file.jpg")]
    [InlineData("C:/absolute/file.jpg")]
    [InlineData("folder\\file.jpg")]
    [InlineData("./file.jpg")]
    [InlineData("folder/../file.jpg")]
    public void Validate_InvalidObjectKey_Throws(string objectKey)
    {
        Assert.Throws<StorageFlow.Abstractions.Exceptions.StorageNamingException>(
            () => _validator.Validate(objectKey));
    }

    [Fact]
    public void Validate_RelativeObjectKey_DoesNotThrow()
    {
        _validator.Validate("2026/06/photo-a1b2c3d4.jpg");
    }
}
