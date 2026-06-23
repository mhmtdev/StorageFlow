using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;
using StorageFlow.Core.Validation;

namespace StorageFlow.Tests.Unit.Validation;

public class FileSizeValidatorTests
{
    private static FileValidationContext Context(long? length = null, Stream? stream = null) => new()
    {
        Content = stream ?? new MemoryStream(new byte[length.HasValue ? (int)length.Value : 0]),
        FileName = "test.jpg",
        ContentLength = length
    };

    [Fact]
    public async Task ValidateAsync_WhenFileSizeWithinLimits_ReturnsSuccess()
    {
        var policy = new ValidationPolicy { MinFileSizeBytes = 100, MaxFileSizeBytes = 1000 };
        var validator = new FileSizeValidator(policy);

        var result = await validator.ValidateAsync(Context(500));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenFileSizeExceedsMax_ReturnsFailed()
    {
        var policy = new ValidationPolicy { MaxFileSizeBytes = 100 };
        var validator = new FileSizeValidator(policy);

        var result = await validator.ValidateAsync(Context(200));

        Assert.False(result.IsValid);
        Assert.Contains("exceeds the maximum", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WhenFileSizeBelowMin_ReturnsFailed()
    {
        var policy = new ValidationPolicy { MinFileSizeBytes = 500 };
        var validator = new FileSizeValidator(policy);

        var result = await validator.ValidateAsync(Context(100));

        Assert.False(result.IsValid);
        Assert.Contains("below the minimum", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WhenNoLimitsDefined_ReturnsSuccess()
    {
        var policy = new ValidationPolicy();
        var validator = new FileSizeValidator(policy);

        var result = await validator.ValidateAsync(Context(999_999_999));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenContentLengthUnknownAndStreamNotSeekable_ReturnsSuccess()
    {
        var policy = new ValidationPolicy { MaxFileSizeBytes = 10 };
        var validator = new FileSizeValidator(policy);
        var nonSeekable = new NonSeekableStream(new byte[100]);

        var ctx = new FileValidationContext { Content = nonSeekable, FileName = "test.jpg" };
        var result = await validator.ValidateAsync(ctx);

        Assert.True(result.IsValid); // size unknown → skip
    }

    [Fact]
    public void Order_ShouldBe10() => Assert.Equal(10, new FileSizeValidator(new ValidationPolicy()).Order);
}

public class ExtensionValidatorTests
{
    private static FileValidationContext Ctx(string fileName) => new()
    {
        Content = new MemoryStream(),
        FileName = fileName
    };

    [Fact]
    public async Task ValidateAsync_WhenExtensionInAllowedList_ReturnsSuccess()
    {
        var policy = new ValidationPolicy { AllowedExtensions = [".jpg", ".png"] };
        var validator = new ExtensionValidator(policy);

        var result = await validator.ValidateAsync(Ctx("photo.jpg"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenExtensionNotInAllowedList_ReturnsFailed()
    {
        var policy = new ValidationPolicy { AllowedExtensions = [".jpg", ".png"] };
        var validator = new ExtensionValidator(policy);

        var result = await validator.ValidateAsync(Ctx("document.pdf"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenExtensionInBlockedList_ReturnsFailed()
    {
        var policy = new ValidationPolicy { BlockedExtensions = [".exe", ".bat"] };
        var validator = new ExtensionValidator(policy);

        var result = await validator.ValidateAsync(Ctx("malware.exe"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenExtensionCheckIsCaseInsensitive_ReturnsSuccess()
    {
        var policy = new ValidationPolicy { AllowedExtensions = [".jpg"] };
        var validator = new ExtensionValidator(policy);

        var result = await validator.ValidateAsync(Ctx("PHOTO.JPG"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenNoRestrictions_ReturnsSuccess()
    {
        var validator = new ExtensionValidator(new ValidationPolicy());

        var result = await validator.ValidateAsync(Ctx("anything.xyz"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Order_ShouldBe20() => Assert.Equal(20, new ExtensionValidator(new ValidationPolicy()).Order);
}

public class MimeTypeValidatorTests
{
    private static FileValidationContext Ctx(string? contentType) => new()
    {
        Content = new MemoryStream(),
        FileName = "test.jpg",
        ContentType = contentType
    };

    [Fact]
    public async Task ValidateAsync_WhenMimeTypeInAllowedList_ReturnsSuccess()
    {
        var policy = new ValidationPolicy { AllowedMimeTypes = ["image/jpeg", "image/png"] };
        var validator = new MimeTypeValidator(policy);

        var result = await validator.ValidateAsync(Ctx("image/jpeg"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenMimeTypeNotInAllowedList_ReturnsFailed()
    {
        var policy = new ValidationPolicy { AllowedMimeTypes = ["image/jpeg"] };
        var validator = new MimeTypeValidator(policy);

        var result = await validator.ValidateAsync(Ctx("application/pdf"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenContentTypeHasParameters_StripsThemBeforeChecking()
    {
        var policy = new ValidationPolicy { AllowedMimeTypes = ["image/jpeg"] };
        var validator = new MimeTypeValidator(policy);

        var result = await validator.ValidateAsync(Ctx("image/jpeg; charset=utf-8"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenNoAllowedMimeTypesDefined_ReturnsSuccess()
    {
        var validator = new MimeTypeValidator(new ValidationPolicy());

        var result = await validator.ValidateAsync(Ctx("application/octet-stream"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenAllowedMimesDefinedButContentTypeNull_ReturnsFailed()
    {
        var policy = new ValidationPolicy { AllowedMimeTypes = ["image/jpeg"] };
        var validator = new MimeTypeValidator(policy);

        var result = await validator.ValidateAsync(Ctx(null));

        Assert.False(result.IsValid);
        Assert.Contains("Content-Type", result.ErrorMessage);
    }

    [Fact]
    public void Order_ShouldBe30() => Assert.Equal(30, new MimeTypeValidator(new ValidationPolicy()).Order);
}

public class FileSignatureValidatorTests
{
    [Theory]
    [InlineData("test.jpg",  new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00 })]
    [InlineData("test.jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE1, 0x00 })]
    [InlineData("test.png",  new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
    [InlineData("test.pdf",  new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D })]
    [InlineData("test.zip",  new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00 })]
    [InlineData("test.mp3",  new byte[] { 0x49, 0x44, 0x33, 0x03, 0x00 })] // ID3
    public async Task ValidateAsync_WhenSignatureMatchesExtension_ReturnsSuccess(string fileName, byte[] header)
    {
        var policy = new ValidationPolicy { RequireValidSignature = true };
        var validator = new FileSignatureValidator(policy);
        var stream = CreateStream(header, 20);
        var ctx = new FileValidationContext { Content = stream, FileName = fileName };

        var result = await validator.ValidateAsync(ctx);

        Assert.True(result.IsValid, $"Expected valid for {fileName}. Error: {result.ErrorMessage}");
    }

    [Fact]
    public async Task ValidateAsync_WhenRenamedExeAsJpg_ReturnsFailed()
    {
        var policy = new ValidationPolicy { RequireValidSignature = true };
        var validator = new FileSignatureValidator(policy);
        // MZ header (EXE)
        var exeHeader = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03 };
        var stream = CreateStream(exeHeader, 10);
        var ctx = new FileValidationContext { Content = stream, FileName = "malware.jpg" };

        var result = await validator.ValidateAsync(ctx);

        Assert.False(result.IsValid);
        Assert.Contains("signature", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WhenRequireSignatureIsFalse_SkipsCheck()
    {
        var policy = new ValidationPolicy { RequireValidSignature = false };
        var validator = new FileSignatureValidator(policy);
        var stream = new MemoryStream(new byte[] { 0x00, 0x00, 0x00 });
        var ctx = new FileValidationContext { Content = stream, FileName = "anything.jpg" };

        var result = await validator.ValidateAsync(ctx);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenUnknownExtension_SkipsSignatureCheck()
    {
        var policy = new ValidationPolicy { RequireValidSignature = true };
        var validator = new FileSignatureValidator(policy);
        var stream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02 });
        var ctx = new FileValidationContext { Content = stream, FileName = "file.xyz" };

        var result = await validator.ValidateAsync(ctx);

        Assert.True(result.IsValid); // unknown ext → skip
    }

    [Fact]
    public async Task ValidateAsync_ResetsStreamPositionAfterReading()
    {
        var policy = new ValidationPolicy { RequireValidSignature = true };
        var validator = new FileSignatureValidator(policy);
        var bytes = new byte[20];
        bytes[0] = 0xFF; bytes[1] = 0xD8; bytes[2] = 0xFF;
        var stream = new MemoryStream(bytes);
        var ctx = new FileValidationContext { Content = stream, FileName = "photo.jpg" };

        await validator.ValidateAsync(ctx);

        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Order_ShouldBe40() => Assert.Equal(40, new FileSignatureValidator(new ValidationPolicy()).Order);

    private static MemoryStream CreateStream(byte[] header, int totalSize)
    {
        var data = new byte[totalSize];
        Array.Copy(header, data, Math.Min(header.Length, totalSize));
        return new MemoryStream(data);
    }
}

// ── Test helpers ──────────────────────────────────────────────────────────────

internal sealed class NonSeekableStream : Stream
{
    private readonly MemoryStream _inner;
    public NonSeekableStream(byte[] data) => _inner = new MemoryStream(data);
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
