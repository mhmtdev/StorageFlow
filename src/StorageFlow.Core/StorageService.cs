using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;
using StorageFlow.Core.Configuration;
using StorageFlow.Core.Pipeline;

namespace StorageFlow.Core;

/// <summary>
/// Default fluent implementation of <see cref="IStorageService"/>.
/// </summary>
public sealed class StorageService : IStorageService
{
    private readonly StorageOperationExecutor _executor;

    /// <summary>Creates the StorageFlow fluent entry point.</summary>
    public StorageService(
        IStorageProviderRegistry registry,
        StorageFlowOptions options,
        IReadOnlyDictionary<StorageProviderToken, ProviderPolicyOverrides> policyOverrides,
        INamingStrategyResolver namingStrategyResolver,
        IObjectKeyValidator objectKeyValidator,
        IDeliveryUrlResolver deliveryUrlResolver,
        IPresignedUrlCache? presignedUrlCache = null,
        IEnumerable<IFileValidator>? customValidators = null)
    {
        _executor = new StorageOperationExecutor(
            registry,
            options,
            policyOverrides,
            namingStrategyResolver,
            objectKeyValidator,
            deliveryUrlResolver,
            presignedUrlCache,
            customValidators?.ToArray() ?? []);
    }

    /// <inheritdoc />
    public IStorageOperationBuilder Provider(StorageProviderToken provider) =>
        NewBuilder().Provider(provider);

    /// <inheritdoc />
    public IStorageOperationBuilder Profile<TProfileKey>()
        where TProfileKey : IStorageProfileKey =>
        NewBuilder().Profile<TProfileKey>();

    /// <inheritdoc />
    public IStorageOperationBuilder Validation<TPolicyKey>()
        where TPolicyKey : IValidationPolicyKey =>
        NewBuilder().Validation<TPolicyKey>();

    /// <inheritdoc />
    public IStorageOperationBuilder Naming<TPolicyKey>()
        where TPolicyKey : INamingPolicyKey =>
        NewBuilder().Naming<TPolicyKey>();

    /// <inheritdoc />
    public IStorageOperationBuilder PresignedUrl<TPolicyKey>()
        where TPolicyKey : IPresignedUrlPolicyKey =>
        NewBuilder().PresignedUrl<TPolicyKey>();

    /// <inheritdoc />
    public IStorageOperationBuilder Metadata(string key, string value) =>
        NewBuilder().Metadata(key, value);

    /// <inheritdoc />
    public IStorageOperationBuilder Metadata(IReadOnlyDictionary<string, string> values) =>
        NewBuilder().Metadata(values);

    /// <inheritdoc />
    public IStorageOperationBuilder CacheControl(string value) =>
        NewBuilder().CacheControl(value);

    /// <inheritdoc />
    public IStorageOperationBuilder ContentDisposition(string value) =>
        NewBuilder().ContentDisposition(value);

    /// <inheritdoc />
    public IStorageOperationBuilder FromStream(
        Stream content,
        string fileName,
        string? contentType = null,
        long? contentLength = null) =>
        NewBuilder().FromStream(content, fileName, contentType, contentLength);

    /// <inheritdoc />
    public Task<StorageResult<UploadResult>> UploadAsync(
        CancellationToken cancellationToken = default) =>
        NewBuilder().UploadAsync(cancellationToken);

    /// <inheritdoc />
    public Task<StorageResult<UploadResult>> UploadAsync(
        string bucket,
        CancellationToken cancellationToken = default) =>
        NewBuilder().UploadAsync(bucket, cancellationToken);

    /// <inheritdoc />
    public IStorageObjectBuilder Object(string objectKey) =>
        NewBuilder().Object(objectKey);

    /// <inheritdoc />
    public IStorageObjectBuilder Object(string bucket, string objectKey) =>
        NewBuilder().Object(bucket, objectKey);

    /// <inheritdoc />
    public IStorageObjectCollectionBuilder Objects(IReadOnlyList<string> objectKeys) =>
        NewBuilder().Objects(objectKeys);

    /// <inheritdoc />
    public IStorageObjectCollectionBuilder Objects(
        string bucket,
        IReadOnlyList<string> objectKeys) =>
        NewBuilder().Objects(bucket, objectKeys);

    private StorageOperationBuilder NewBuilder() => new(_executor);
}

internal sealed class StorageOperationBuilder : IStorageOperationBuilder
{
    private readonly StorageOperationExecutor _executor;
    private readonly UploadRequest _request = new();
    private StorageProviderToken? _providerToken;
    private string? _profileBucket;
    private Type? _profilePresignedUrlPolicyKey;
    private string? _profileError;
    private string? _metadataError;
    private string? _cacheControlError;
    private string? _contentDispositionError;

    internal StorageOperationBuilder(StorageOperationExecutor executor)
    {
        _executor = executor;
    }

    public IStorageOperationBuilder Provider(StorageProviderToken provider)
    {
        _providerToken = provider;
        return this;
    }

    public IStorageOperationBuilder Profile<TProfileKey>()
        where TProfileKey : IStorageProfileKey
    {
        if (!_executor.TryGetProfile(typeof(TProfileKey), out var profile))
        {
            _profileError =
                $"Storage profile '{typeof(TProfileKey).FullName}' is not registered.";
            return this;
        }

        _providerToken = profile.ProviderToken;
        _profileBucket = profile.BucketName;
        _request.ValidationPolicyKey = profile.ValidationPolicyKey;
        _request.NamingPolicyKey = profile.NamingPolicyKey;
        _request.PresignedUrlPolicyKey = profile.PresignedUrlPolicyKey;
        _profilePresignedUrlPolicyKey = profile.PresignedUrlPolicyKey;
        _profileError = null;
        return this;
    }

    public IStorageOperationBuilder Validation<TPolicyKey>()
        where TPolicyKey : IValidationPolicyKey
    {
        _request.ValidationPolicyKey = typeof(TPolicyKey);
        return this;
    }

    public IStorageOperationBuilder Naming<TPolicyKey>()
        where TPolicyKey : INamingPolicyKey
    {
        _request.NamingPolicyKey = typeof(TPolicyKey);
        return this;
    }

    public IStorageOperationBuilder PresignedUrl<TPolicyKey>()
        where TPolicyKey : IPresignedUrlPolicyKey
    {
        _request.PresignedUrlPolicyKey = typeof(TPolicyKey);
        _profilePresignedUrlPolicyKey = typeof(TPolicyKey);
        return this;
    }

    public IStorageOperationBuilder Metadata(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            _metadataError = "Metadata key cannot be empty.";
        else
            _request.Metadata[key] = value;

        return this;
    }

    public IStorageOperationBuilder Metadata(IReadOnlyDictionary<string, string> values)
    {
        foreach (var (key, value) in values)
            Metadata(key, value);

        return this;
    }

    public IStorageOperationBuilder CacheControl(string value)
    {
        SetHeader(
            value,
            "Cache-Control",
            error => _cacheControlError = error,
            normalized => _request.CacheControl = normalized);
        return this;
    }

    public IStorageOperationBuilder ContentDisposition(string value)
    {
        SetHeader(
            value,
            "Content-Disposition",
            error => _contentDispositionError = error,
            normalized => _request.ContentDisposition = normalized);
        return this;
    }

    public IStorageOperationBuilder FromStream(
        Stream content,
        string fileName,
        string? contentType = null,
        long? contentLength = null)
    {
        _request.Content = content;
        _request.FileName = fileName;
        _request.ContentType = contentType;
        _request.ContentLength = contentLength;
        return this;
    }

    public Task<StorageResult<UploadResult>> UploadAsync(
        CancellationToken cancellationToken = default) =>
        UploadCoreAsync(_profileBucket, cancellationToken);

    public Task<StorageResult<UploadResult>> UploadAsync(
        string bucket,
        CancellationToken cancellationToken = default) =>
        UploadCoreAsync(bucket, cancellationToken);

    public IStorageObjectBuilder Object(string objectKey) =>
        new StorageObjectBuilder(
            _executor,
            _providerToken,
            _profileBucket,
            objectKey,
            _profilePresignedUrlPolicyKey,
            ConfigurationError());

    public IStorageObjectBuilder Object(string bucket, string objectKey) =>
        new StorageObjectBuilder(
            _executor,
            _providerToken,
            bucket,
            objectKey,
            _profilePresignedUrlPolicyKey,
            ConfigurationError());

    public IStorageObjectCollectionBuilder Objects(IReadOnlyList<string> objectKeys) =>
        new StorageObjectCollectionBuilder(
            _executor,
            _providerToken,
            _profileBucket,
            objectKeys,
            ConfigurationError());

    public IStorageObjectCollectionBuilder Objects(
        string bucket,
        IReadOnlyList<string> objectKeys) =>
        new StorageObjectCollectionBuilder(
            _executor,
            _providerToken,
            bucket,
            objectKeys,
            ConfigurationError());

    private Task<StorageResult<UploadResult>> UploadCoreAsync(
        string? bucket,
        CancellationToken cancellationToken)
    {
        var error = ConfigurationError() ?? ValidateUpload(bucket);
        return error is null
            ? _executor.UploadAsync(_providerToken, bucket!, _request, cancellationToken)
            : Task.FromResult(StorageOperationExecutor.Fail<UploadResult>(error));
    }

    private string? ConfigurationError() =>
        _profileError ??
        _metadataError ??
        _cacheControlError ??
        _contentDispositionError;

    private void SetHeader(
        string value,
        string headerName,
        Action<string?> setError,
        Action<string> apply)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            setError($"{headerName} cannot be empty.");
            return;
        }

        if (value.Contains('\r') || value.Contains('\n'))
        {
            setError($"{headerName} cannot contain CR or LF characters.");
            return;
        }

        apply(value);
        setError(null);
    }

    private string? ValidateUpload(string? bucket)
    {
        if (string.IsNullOrWhiteSpace(bucket))
            return "A bucket must be supplied explicitly or by a storage profile.";
        if (_request.Content is null)
            return "An upload stream must be supplied with FromStream().";
        if (string.IsNullOrWhiteSpace(_request.FileName))
            return "A file name must be supplied with FromStream().";
        if (!_request.Content.CanRead)
            return "The upload stream must be readable.";
        return null;
    }
}

internal sealed class StorageObjectBuilder : IStorageObjectBuilder
{
    private readonly StorageOperationExecutor _executor;
    private readonly StorageProviderToken? _providerToken;
    private readonly string? _bucket;
    private readonly string _objectKey;
    private readonly Type? _presignedUrlPolicyKey;
    private readonly string? _configurationError;

    internal StorageObjectBuilder(
        StorageOperationExecutor executor,
        StorageProviderToken? providerToken,
        string? bucket,
        string objectKey,
        Type? presignedUrlPolicyKey,
        string? configurationError)
    {
        _executor = executor;
        _providerToken = providerToken;
        _bucket = bucket;
        _objectKey = objectKey;
        _presignedUrlPolicyKey = presignedUrlPolicyKey;
        _configurationError = configurationError;
    }

    public Task<StorageResult<DownloadResult>> DownloadAsync(
        CancellationToken cancellationToken = default) =>
        Execute((provider, bucket, key, ct) =>
            _executor.DownloadAsync(provider, bucket, key, ct), cancellationToken);

    public Task<StorageResult> DeleteAsync(
        CancellationToken cancellationToken = default)
    {
        var error = Validate();
        return error is null
            ? _executor.DeleteAsync(_providerToken, _bucket!, _objectKey, cancellationToken)
            : Task.FromResult(StorageOperationExecutor.Fail(error));
    }

    public Task<StorageResult<bool>> ExistsAsync(
        CancellationToken cancellationToken = default) =>
        Execute((provider, bucket, key, ct) =>
            _executor.ExistsAsync(provider, bucket, key, ct), cancellationToken);

    public Task<StorageResult<string>> GetPresignedUrlAsync(
        CancellationToken cancellationToken = default)
    {
        var error = Validate();
        if (error is not null)
            return Task.FromResult(StorageOperationExecutor.Fail<string>(error));
        if (_presignedUrlPolicyKey is null)
        {
            return Task.FromResult(StorageOperationExecutor.Fail<string>(
                "A presigned URL policy must be supplied by a profile or generic policy selection."));
        }

        return _executor.GetPresignedUrlAsync(
            _providerToken,
            _bucket!,
            _objectKey,
            _presignedUrlPolicyKey,
            cancellationToken);
    }

    public Task<StorageResult<string>> GetPresignedUrlAsync<TPolicyKey>(
        CancellationToken cancellationToken = default)
        where TPolicyKey : IPresignedUrlPolicyKey
    {
        var error = Validate();
        return error is null
            ? _executor.GetPresignedUrlAsync(
                _providerToken,
                _bucket!,
                _objectKey,
                typeof(TPolicyKey),
                cancellationToken)
            : Task.FromResult(StorageOperationExecutor.Fail<string>(error));
    }

    public StorageResult<ObjectDeliveryUrlResult> GetDeliveryUrl<TPolicyKey>()
        where TPolicyKey : IDeliveryUrlPolicyKey
    {
        var error = Validate();
        return error is null
            ? _executor.GetDeliveryUrl(
                _providerToken,
                _bucket!,
                _objectKey,
                typeof(TPolicyKey))
            : StorageOperationExecutor.Fail<ObjectDeliveryUrlResult>(error);
    }

    private Task<StorageResult<T>> Execute<T>(
        Func<StorageProviderToken?, string, string, CancellationToken, Task<StorageResult<T>>> operation,
        CancellationToken cancellationToken)
    {
        var error = Validate();
        return error is null
            ? operation(_providerToken, _bucket!, _objectKey, cancellationToken)
            : Task.FromResult(StorageOperationExecutor.Fail<T>(error));
    }

    private string? Validate()
    {
        if (_configurationError is not null)
            return _configurationError;
        if (string.IsNullOrWhiteSpace(_bucket))
            return "A bucket must be supplied explicitly or by a storage profile.";
        if (string.IsNullOrWhiteSpace(_objectKey))
            return "An object key must be supplied.";
        return null;
    }
}

internal sealed class StorageObjectCollectionBuilder : IStorageObjectCollectionBuilder
{
    private readonly StorageOperationExecutor _executor;
    private readonly StorageProviderToken? _providerToken;
    private readonly string? _bucket;
    private readonly IReadOnlyList<string>? _objectKeys;
    private readonly string? _configurationError;

    internal StorageObjectCollectionBuilder(
        StorageOperationExecutor executor,
        StorageProviderToken? providerToken,
        string? bucket,
        IReadOnlyList<string>? objectKeys,
        string? configurationError)
    {
        _executor = executor;
        _providerToken = providerToken;
        _bucket = bucket;
        _objectKeys = objectKeys;
        _configurationError = configurationError;
    }

    public StorageResult<IReadOnlyList<ObjectDeliveryUrlResult>>
        GetDeliveryUrls<TPolicyKey>()
        where TPolicyKey : IDeliveryUrlPolicyKey
    {
        var error = Validate();
        return error is null
            ? _executor.GetDeliveryUrls(
                _providerToken,
                _bucket!,
                _objectKeys!,
                typeof(TPolicyKey))
            : StorageOperationExecutor.Fail<IReadOnlyList<ObjectDeliveryUrlResult>>(error);
    }

    private string? Validate()
    {
        if (_configurationError is not null)
            return _configurationError;
        if (string.IsNullOrWhiteSpace(_bucket))
            return "A bucket must be supplied explicitly or by a storage profile.";
        if (_objectKeys is null)
            return "An object key collection must be supplied.";
        return null;
    }
}

internal sealed class StorageOperationExecutor
{
    private readonly IStorageProviderRegistry _registry;
    private readonly StorageFlowOptions _options;
    private readonly IReadOnlyDictionary<StorageProviderToken, ProviderPolicyOverrides> _policyOverrides;
    private readonly INamingStrategyResolver _namingStrategyResolver;
    private readonly IObjectKeyValidator _objectKeyValidator;
    private readonly IDeliveryUrlResolver _deliveryUrlResolver;
    private readonly IPresignedUrlCache? _presignedUrlCache;
    private readonly IReadOnlyList<IFileValidator> _customValidators;

    internal StorageOperationExecutor(
        IStorageProviderRegistry registry,
        StorageFlowOptions options,
        IReadOnlyDictionary<StorageProviderToken, ProviderPolicyOverrides> policyOverrides,
        INamingStrategyResolver namingStrategyResolver,
        IObjectKeyValidator objectKeyValidator,
        IDeliveryUrlResolver deliveryUrlResolver,
        IPresignedUrlCache? presignedUrlCache,
        IReadOnlyList<IFileValidator> customValidators)
    {
        _registry = registry;
        _options = options;
        _policyOverrides = policyOverrides;
        _namingStrategyResolver = namingStrategyResolver;
        _objectKeyValidator = objectKeyValidator;
        _deliveryUrlResolver = deliveryUrlResolver;
        _presignedUrlCache = presignedUrlCache;
        _customValidators = customValidators;
    }

    internal bool TryGetProfile(Type key, out StorageProfile profile) =>
        _options.RegisteredProfiles.TryGetValue(key, out profile!);

    internal async Task<StorageResult<UploadResult>> UploadAsync(
        StorageProviderToken? providerToken,
        string bucket,
        UploadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var (token, provider) = ResolveProvider(providerToken);
            var pipeline = CreatePipeline(token, provider);
            return await pipeline.ExecuteAsync(bucket, request, cancellationToken);
        }
        catch (StorageConfigurationException ex)
        {
            return Fail<UploadResult>(ex.Message, ex);
        }
    }

    internal async Task<StorageResult<DownloadResult>> DownloadAsync(
        StorageProviderToken? providerToken,
        string bucket,
        string objectKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var (_, provider) = ResolveProvider(providerToken);
            return StorageResult<DownloadResult>.Success(
                await provider.DownloadAsync(bucket, objectKey, cancellationToken));
        }
        catch (StorageProviderException ex)
        {
            return Fail<DownloadResult>(ex.Message, ex, ex.ErrorCode);
        }
        catch (Exception ex)
        {
            return Fail<DownloadResult>(ex.Message, ex);
        }
    }

    internal async Task<StorageResult> DeleteAsync(
        StorageProviderToken? providerToken,
        string bucket,
        string objectKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var (_, provider) = ResolveProvider(providerToken);
            await provider.DeleteAsync(bucket, objectKey, cancellationToken);
            return StorageResult.Success();
        }
        catch (StorageProviderException ex)
        {
            return Fail(ex.Message, ex, ex.ErrorCode);
        }
        catch (Exception ex)
        {
            return Fail(ex.Message, ex);
        }
    }

    internal async Task<StorageResult<bool>> ExistsAsync(
        StorageProviderToken? providerToken,
        string bucket,
        string objectKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var (_, provider) = ResolveProvider(providerToken);
            return StorageResult<bool>.Success(
                await provider.ExistsAsync(bucket, objectKey, cancellationToken));
        }
        catch (StorageProviderException ex)
        {
            return Fail<bool>(ex.Message, ex, ex.ErrorCode);
        }
        catch (Exception ex)
        {
            return Fail<bool>(ex.Message, ex);
        }
    }

    internal async Task<StorageResult<string>> GetPresignedUrlAsync(
        StorageProviderToken? providerToken,
        string bucket,
        string objectKey,
        Type policyKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var (token, provider) = ResolveProvider(providerToken);
            var pipeline = CreatePipeline(token, provider);
            var policy = pipeline.ResolvePresignedUrlPolicy(policyKey);
            var cacheKey = policyKey.FullName ?? policyKey.Name;

            if (_presignedUrlCache is not null)
            {
                var cached = await _presignedUrlCache.GetAsync(
                    provider.ProviderName,
                    bucket,
                    objectKey,
                    cacheKey,
                    cancellationToken);
                if (cached is not null)
                    return StorageResult<string>.Success(cached);
            }

            var url = await provider.GetPresignedUrlAsync(
                bucket,
                objectKey,
                policy.Expiration,
                policy.HttpMethod,
                cancellationToken);

            if (_presignedUrlCache is not null)
            {
                await _presignedUrlCache.SetAsync(
                    provider.ProviderName,
                    bucket,
                    objectKey,
                    cacheKey,
                    url,
                    policy.Expiration,
                    cancellationToken);
            }

            return StorageResult<string>.Success(url);
        }
        catch (StorageConfigurationException ex)
        {
            return Fail<string>(ex.Message, ex);
        }
        catch (StorageProviderException ex)
        {
            return Fail<string>(ex.Message, ex, ex.ErrorCode);
        }
        catch (Exception ex)
        {
            return Fail<string>(ex.Message, ex);
        }
    }

    internal StorageResult<ObjectDeliveryUrlResult> GetDeliveryUrl(
        StorageProviderToken? providerToken,
        string bucket,
        string objectKey,
        Type policyKey)
    {
        try
        {
            var token = ResolveProviderToken(providerToken);
            var policy = ResolveDeliveryUrlPolicy(token, policyKey);
            return _deliveryUrlResolver.Resolve(policy, bucket, objectKey);
        }
        catch (StorageConfigurationException ex)
        {
            return Fail<ObjectDeliveryUrlResult>(ex.Message, ex);
        }
    }

    internal StorageResult<IReadOnlyList<ObjectDeliveryUrlResult>> GetDeliveryUrls(
        StorageProviderToken? providerToken,
        string bucket,
        IReadOnlyList<string> objectKeys,
        Type policyKey)
    {
        try
        {
            var token = ResolveProviderToken(providerToken);
            var policy = ResolveDeliveryUrlPolicy(token, policyKey);
            return _deliveryUrlResolver.ResolveMany(policy, bucket, objectKeys);
        }
        catch (StorageConfigurationException ex)
        {
            return Fail<IReadOnlyList<ObjectDeliveryUrlResult>>(ex.Message, ex);
        }
    }

    internal static StorageResult<T> Fail<T>(
        string message,
        Exception? exception = null,
        StorageErrorCode code = StorageErrorCode.Unknown) =>
        StorageResult<T>.Failure(StorageError.Create(code, message, exception));

    internal static StorageResult Fail(
        string message,
        Exception? exception = null,
        StorageErrorCode code = StorageErrorCode.Unknown) =>
        StorageResult.Failure(StorageError.Create(code, message, exception));

    private (StorageProviderToken Token, IStorageProvider Provider) ResolveProvider(
        StorageProviderToken? providerToken)
    {
        var token = providerToken ?? _registry.GetDefaultProviderToken();
        return (token, _registry.Get(token));
    }

    private StorageProviderToken ResolveProviderToken(StorageProviderToken? providerToken)
    {
        var token = providerToken ?? _registry.GetDefaultProviderToken();
        _registry.Get(token);
        return token;
    }

    private DeliveryUrlPolicy ResolveDeliveryUrlPolicy(
        StorageProviderToken providerToken,
        Type policyKey)
    {
        if (_policyOverrides.TryGetValue(providerToken, out var overrides) &&
            overrides.DeliveryUrlPolicies.TryGetValue(policyKey, out var providerPolicy))
        {
            return providerPolicy;
        }

        if (_options.GlobalDeliveryUrlPolicies.TryGetValue(policyKey, out var globalPolicy))
            return globalPolicy;

        throw new StorageConfigurationException(
            $"Delivery URL policy '{policyKey.FullName}' was not found for provider " +
            $"'{providerToken.Name}' and is not defined as a global policy.");
    }

    private UploadPipeline CreatePipeline(
        StorageProviderToken token,
        IStorageProvider provider)
    {
        _policyOverrides.TryGetValue(token, out var overrides);
        return new UploadPipeline(
            provider,
            _options,
            _namingStrategyResolver,
            _objectKeyValidator,
            overrides,
            _presignedUrlCache,
            _customValidators);
    }
}
