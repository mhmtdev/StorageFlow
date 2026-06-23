using Microsoft.Extensions.DependencyInjection;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;
using StorageFlow.Core.Configuration;
using StorageFlow.Core.DependencyInjection;
using StorageFlow.Testing;

namespace StorageFlow.Tests.Component.Uploads;

public class UploadPipelineTests
{
    private sealed class ImagePolicy : IValidationPolicyKey;
    private sealed class MediaNaming : INamingPolicyKey;
    private sealed class OtherNaming : INamingPolicyKey;
    private sealed class UnknownNaming : INamingPolicyKey;
    private sealed class CustomNaming : INamingPolicyKey;
    private sealed class MediaProfile : IStorageProfileKey;

    private sealed class ValidatorOrderLog
    {
        public List<int> Orders { get; } = [];
    }

    private sealed class EarlyValidator(ValidatorOrderLog log) : IFileValidator
    {
        public int Order => 5;

        public Task<ValidationResult> ValidateAsync(
            FileValidationContext context,
            CancellationToken cancellationToken = default)
        {
            log.Orders.Add(Order);
            return Task.FromResult(ValidationResult.Success());
        }
    }

    private sealed class LateValidator(ValidatorOrderLog log) : IFileValidator
    {
        public int Order => 50;

        public Task<ValidationResult> ValidateAsync(
            FileValidationContext context,
            CancellationToken cancellationToken = default)
        {
            log.Orders.Add(Order);
            return Task.FromResult(ValidationResult.Success());
        }
    }

    private sealed class ConsumingValidator : IFileValidator
    {
        public int Order => 50;

        public async Task<ValidationResult> ValidateAsync(
            FileValidationContext context,
            CancellationToken cancellationToken = default)
        {
            var buffer = new byte[9];
            await context.Content.ReadExactlyAsync(buffer, cancellationToken);
            return ValidationResult.Success();
        }
    }

    private sealed class NamingPrefix
    {
        public string Value { get; init; } = "custom";
    }

    private sealed class CustomNamingStrategy : IFileNamingStrategy
    {
        private readonly NamingPrefix _prefix;

        public CustomNamingStrategy(NamingPrefix prefix)
        {
            _prefix = prefix;
        }

        public Task<string> GenerateAsync(
            FileNamingContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult($"{_prefix.Value}/{context.Pattern ?? "default"}{context.Extension}");
    }

    private static IStorageService BuildService(
        Action<StorageFlowOptions>? configure = null,
        Action<InMemoryRegistrationBuilder>? configureProvider = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        configureServices?.Invoke(services);
        services.AddStorageFlow(options =>
        {
            options.Providers.UseInMemory(configureProvider);
            configure?.Invoke(options);
        });
        return services.BuildServiceProvider().GetRequiredService<IStorageService>();
    }

    private static void AddImagePolicy(
        StorageFlowOptions options,
        long? maxBytes = null)
    {
        options.Validation.AddPolicy<ImagePolicy>(policy =>
        {
            policy.AllowedExtensions = [".jpg", ".png"];
            policy.AllowedMimeTypes = ["image/jpeg", "image/png"];
            policy.MaxFileSizeBytes = maxBytes;
        });
    }

    [Fact]
    public async Task FluentUpload_ValidFile_ReturnsSuccess()
    {
        var service = BuildService(options => AddImagePolicy(options));
        var result = await service
            .Validation<ImagePolicy>()
            .FromStream(new MemoryStream(new byte[100]), "photo.jpg", "image/jpeg", 100)
            .UploadAsync("media");

        Assert.True(result.IsSuccess);
        Assert.Equal("media", result.Value!.Bucket);
        Assert.Equal("memory", result.Value.ProviderName);
    }

    [Fact]
    public async Task FluentUpload_NonSeekableSignatureStreamPreservesEveryByte()
    {
        var bytes = new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02, 0x03, 0x04,
            0x05, 0x06, 0x07, 0x08
        };
        var service = BuildService(options =>
            options.Validation.AddPolicy<ImagePolicy>(policy =>
            {
                policy.AllowedExtensions = [".jpg"];
                policy.AllowedMimeTypes = ["image/jpeg"];
                policy.RequireValidSignature = true;
            }));

        var upload = await service
            .Validation<ImagePolicy>()
            .FromStream(
                new NonSeekableUploadStream(bytes),
                "photo.jpg",
                "image/jpeg",
                bytes.Length)
            .UploadAsync("media");

        Assert.True(upload.IsSuccess, upload.Error?.Message);
        var download = await service
            .Object("media", upload.Value!.ObjectKey)
            .DownloadAsync();
        Assert.True(download.IsSuccess, download.Error?.Message);

        await using var content = download.Value!.Content;
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy);
        Assert.Equal(bytes, copy.ToArray());
    }

    [Fact]
    public async Task FluentUpload_CustomValidatorsRunInOrder()
    {
        var log = new ValidatorOrderLog();
        var service = BuildService(
            options => AddImagePolicy(options),
            configureServices: services =>
            {
                services.AddSingleton(log);
                services.AddStorageFlowValidator<LateValidator>();
                services.AddStorageFlowValidator<EarlyValidator>();
            });

        var result = await service
            .Validation<ImagePolicy>()
            .FromStream(new MemoryStream([1]), "photo.jpg", "image/jpeg", 1)
            .UploadAsync("media");

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal([5, 50], log.Orders);
    }

    [Fact]
    public async Task FluentUpload_CustomValidatorCannotCorruptNonSeekableUpload()
    {
        var bytes = new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02, 0x03, 0x04,
            0x05, 0x06, 0x07, 0x08
        };
        var service = BuildService(
            options => options.Validation.AddPolicy<ImagePolicy>(policy =>
            {
                policy.AllowedExtensions = [".jpg"];
                policy.RequireValidSignature = true;
            }),
            configureServices: services =>
                services.AddStorageFlowValidator<ConsumingValidator>());

        var result = await service
            .Validation<ImagePolicy>()
            .FromStream(new NonSeekableUploadStream(bytes), "photo.jpg")
            .UploadAsync("media");

        Assert.False(result.IsSuccess);
        Assert.Equal(StorageErrorCode.ValidationFailed, result.Error!.Code);
        Assert.Contains(nameof(ConsumingValidator), result.Error.Message);
    }

    [Fact]
    public async Task FluentUpload_FileExceedsMaxSize_ReturnsValidationFailure()
    {
        var service = BuildService(options => AddImagePolicy(options, 10));
        var result = await service
            .FromStream(new MemoryStream(new byte[100]), "photo.jpg", "image/jpeg", 100)
            .Validation<ImagePolicy>()
            .UploadAsync("media");

        Assert.False(result.IsSuccess);
        Assert.Equal(StorageErrorCode.ValidationFailed, result.Error!.Code);
    }

    [Fact]
    public async Task FluentUpload_LastPolicySelectionWins()
    {
        var service = BuildService(options =>
        {
            options.Validation.AddPolicy<ImagePolicy>(policy => policy.MaxFileSizeBytes = 1);
            options.Validation.AddPolicy<PermissivePolicy>(policy => policy.MaxFileSizeBytes = 100);
        });

        var result = await service
            .Validation<ImagePolicy>()
            .Validation<PermissivePolicy>()
            .FromStream(new MemoryStream(new byte[10]), "file.bin", contentLength: 10)
            .UploadAsync("media");

        Assert.True(result.IsSuccess);
    }

    private sealed class PermissivePolicy : IValidationPolicyKey;

    [Fact]
    public async Task FluentUpload_WithoutNamingPolicy_UsesGuidFallback()
    {
        var service = BuildService();
        var result = await service
            .FromStream(new MemoryStream(new byte[5]), "photo.jpg")
            .UploadAsync("media");

        Assert.True(result.IsSuccess);
        Assert.True(Guid.TryParse(
            Path.GetFileNameWithoutExtension(result.Value!.ObjectKey),
            out _));
    }

    [Fact]
    public async Task FluentUpload_UsesGlobalDefaultNamingPolicy()
    {
        var service = BuildService(options =>
            options.Naming
                .AddPolicy<MediaNaming>(policy => policy.UseSeo())
                .AsDefault());

        var result = await service
            .FromStream(new MemoryStream(new byte[5]), "My Product Image.jpg")
            .UploadAsync("media");

        Assert.True(result.IsSuccess);
        Assert.StartsWith("my-product-image-", result.Value!.ObjectKey);
    }

    [Fact]
    public async Task FluentUpload_ProviderOverrideAppliesToGlobalDefaultNaming()
    {
        var service = BuildService(
            options => options.Naming
                .AddPolicy<MediaNaming>(policy =>
                    policy.UsePattern("global/{guid}{ext}"))
                .AsDefault(),
            provider => provider.Naming.AddPolicy<MediaNaming>(policy =>
                policy.UsePattern("provider/{guid}{ext}")));

        var result = await service
            .FromStream(new MemoryStream(new byte[5]), "photo.jpg")
            .UploadAsync("media");

        Assert.True(result.IsSuccess);
        Assert.StartsWith("provider/", result.Value!.ObjectKey);
    }

    [Fact]
    public async Task FluentUpload_ExplicitNamingOverridesGlobalDefault()
    {
        var service = BuildService(options =>
        {
            options.Naming
                .AddPolicy<MediaNaming>(policy => policy.UseSeo())
                .AsDefault();
            options.Naming.AddPolicy<OtherNaming>(policy =>
                policy.UsePattern("explicit/{guid}{ext}"));
        });

        var result = await service
            .Naming<OtherNaming>()
            .FromStream(new MemoryStream(new byte[5]), "photo.jpg")
            .UploadAsync("media");

        Assert.True(result.IsSuccess);
        Assert.StartsWith("explicit/", result.Value!.ObjectKey);
    }

    [Fact]
    public async Task FluentUpload_AlternateNamingKeyUsesItsConfiguredPattern()
    {
        var service = BuildService(options =>
        {
            options.Naming.AddPolicy<MediaNaming>(policy =>
                policy.UsePattern("global/{guid}{ext}"));
            options.Naming.AddPolicy<OtherNaming>(policy =>
                policy.UsePattern("alternate/{yyyy}/{guid}{ext}"));
        });

        var result = await service
            .Naming<OtherNaming>()
            .FromStream(new MemoryStream(new byte[5]), "photo.jpg")
            .UploadAsync("media");

        Assert.True(result.IsSuccess);
        Assert.StartsWith($"alternate/{DateTime.UtcNow.Year}/", result.Value!.ObjectKey);
    }

    [Fact]
    public async Task FluentUpload_UnknownNamingPolicyReturnsFailure()
    {
        var service = BuildService();
        var result = await service
            .Naming<UnknownNaming>()
            .FromStream(new MemoryStream(new byte[5]), "photo.jpg")
            .UploadAsync("media");

        Assert.False(result.IsSuccess);
        Assert.Contains(nameof(UnknownNaming), result.Error!.Message);
    }

    [Fact]
    public async Task FluentUpload_CustomNamingStrategyResolvesFromDependencyInjection()
    {
        var service = BuildService(
            options => options.Naming.AddPolicy<CustomNaming>(policy =>
                policy.UseStrategy<CustomNamingStrategy>()),
            configureServices: services =>
            {
                services.AddSingleton(new NamingPrefix());
                services.AddStorageFlowNamingStrategy<CustomNamingStrategy>();
            });

        var result = await service
            .Naming<CustomNaming>()
            .FromStream(new MemoryStream(new byte[5]), "photo.jpg")
            .UploadAsync("media");

        Assert.True(result.IsSuccess);
        Assert.Equal("custom/default.jpg", result.Value!.ObjectKey);
    }

    [Fact]
    public async Task FluentUpload_ProfileProvidesProviderBucketAndPolicies()
    {
        var service = BuildService(options =>
        {
            options.Naming.AddPolicy<MediaNaming>(policy =>
                policy.UsePattern("profile/{guid}{ext}"));
            options.Profiles.Add<MediaProfile>(profile => profile
                .Provider(SFTestProvider.InMemory)
                .Bucket("profile-bucket")
                .Naming<MediaNaming>());
        });

        var result = await service
            .Profile<MediaProfile>()
            .FromStream(new MemoryStream(new byte[5]), "photo.jpg")
            .UploadAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("profile-bucket", result.Value!.Bucket);
        Assert.StartsWith("profile/", result.Value.ObjectKey);
    }

    [Fact]
    public async Task FluentUpload_ExplicitBucketOverridesProfileBucket()
    {
        var service = BuildService(options =>
            options.Profiles.Add<MediaProfile>(profile =>
                profile.Bucket("profile-bucket")));

        var result = await service
            .Profile<MediaProfile>()
            .FromStream(new MemoryStream(new byte[5]), "photo.jpg")
            .UploadAsync("explicit-bucket");

        Assert.True(result.IsSuccess);
        Assert.Equal("explicit-bucket", result.Value!.Bucket);
    }

    [Fact]
    public async Task FluentUpload_OperationSettingAfterProfileOverridesProfileSetting()
    {
        var service = BuildService(options =>
        {
            options.Naming.AddPolicy<MediaNaming>(policy =>
                policy.UsePattern("profile/{guid}{ext}"));
            options.Naming.AddPolicy<OtherNaming>(policy =>
                policy.UsePattern("operation/{guid}{ext}"));
            options.Profiles.Add<MediaProfile>(profile => profile
                .Bucket("media")
                .Naming<MediaNaming>());
        });

        var result = await service
            .Profile<MediaProfile>()
            .Naming<OtherNaming>()
            .FromStream(new MemoryStream(new byte[5]), "photo.jpg")
            .UploadAsync();

        Assert.True(result.IsSuccess);
        Assert.StartsWith("operation/", result.Value!.ObjectKey);
    }

    [Fact]
    public async Task FluentUpload_DoesNotDisposeCallerStream()
    {
        var service = BuildService();
        var stream = new MemoryStream(new byte[5]);

        var result = await service
            .FromStream(stream, "photo.jpg")
            .UploadAsync("media");

        Assert.True(result.IsSuccess);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task FluentUpload_HeadersAreStoredSeparatelyAndLastValueWins()
    {
        var services = new ServiceCollection();
        services.AddStorageFlow(options => options.Providers.UseInMemory());
        await using var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetRequiredService<IStorageService>();

        var result = await service
            .CacheControl("private")
            .CacheControl("public, max-age=86400")
            .ContentDisposition("inline")
            .ContentDisposition("attachment")
            .Metadata("source", "api")
            .FromStream(new MemoryStream([1, 2, 3]), "file.bin")
            .UploadAsync("media");

        var registry = serviceProvider.GetRequiredService<IStorageProviderRegistry>();
        var provider = Assert.IsType<InMemoryStorageProvider>(
            registry.Get(SFTestProvider.InMemory));
        var headers = provider.GetHeaders("media", result.Value!.ObjectKey);
        var metadata = provider.GetMetadata("media", result.Value.ObjectKey);

        Assert.Equal("public, max-age=86400", headers!.CacheControl);
        Assert.Equal("attachment", headers.ContentDisposition);
        Assert.Equal("api", metadata!["source"]);
        Assert.DoesNotContain("Cache-Control", metadata.Keys);
    }

    [Theory]
    [InlineData(true, "public\r\nx-injected: value")]
    [InlineData(false, "attachment\nx-injected: value")]
    [InlineData(true, " ")]
    [InlineData(false, "")]
    public async Task FluentUpload_InvalidStandardHeaderReturnsOperationFailure(
        bool cacheControl,
        string value)
    {
        var service = BuildService();
        var operation = cacheControl
            ? service.CacheControl(value)
            : service.ContentDisposition(value);

        var result = await operation
            .FromStream(new MemoryStream([1]), "file.bin")
            .UploadAsync("media");

        Assert.False(result.IsSuccess);
        Assert.Contains(
            cacheControl ? "Cache-Control" : "Content-Disposition",
            result.Error!.Message);
    }

    [Fact]
    public async Task FluentUpload_ValidHeaderAfterInvalidValueClearsThatHeaderError()
    {
        var service = BuildService();

        var result = await service
            .CacheControl("bad\r\nvalue")
            .CacheControl("no-cache")
            .FromStream(new MemoryStream([1]), "file.bin")
            .UploadAsync("media");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task FluentUpload_MissingSourceReturnsFailure()
    {
        var service = BuildService();

        var result = await service.UploadAsync("media");

        Assert.False(result.IsSuccess);
        Assert.Contains("FromStream", result.Error!.Message);
    }

    [Fact]
    public async Task FluentUpload_MissingBucketReturnsFailure()
    {
        var service = BuildService();

        var result = await service
            .FromStream(new MemoryStream([1]), "file.bin")
            .UploadAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("bucket", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FluentUpload_UnregisteredProfileReturnsFailure()
    {
        var service = BuildService();

        var result = await service
            .Profile<MediaProfile>()
            .FromStream(new MemoryStream([1]), "file.bin")
            .UploadAsync("media");

        Assert.False(result.IsSuccess);
        Assert.Contains("not registered", result.Error!.Message);
    }

    [Fact]
    public void GlobalNaming_MultipleDefaultsThrows()
    {
        var services = new ServiceCollection();

        Assert.Throws<StorageFlow.Abstractions.Exceptions.StorageConfigurationException>(() =>
            services.AddStorageFlow(options =>
            {
                options.Providers.UseInMemory();
                options.Naming
                    .AddPolicy<MediaNaming>(policy => policy.UseGuid())
                    .AsDefault();
                options.Naming
                    .AddPolicy<OtherNaming>(policy => policy.UseGuid())
                    .AsDefault();
            }));
    }

    private sealed class NonSeekableUploadStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
