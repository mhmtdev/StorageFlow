using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;
using StorageFlow.Core.Configuration;
using StorageFlow.Core.Validation;

namespace StorageFlow.Core.Pipeline;

/// <summary>
/// Executes the upload pipeline: Validation → Naming → Provider Upload.
/// Presigned URL cache warm-up is performed if a cache accessor is supplied.
/// </summary>
internal sealed class UploadPipeline
{
    private readonly IStorageProvider _provider;
    private readonly StorageFlowOptions _options;
    private readonly string _providerName;
    private readonly INamingStrategyResolver _namingStrategyResolver;
    private readonly IObjectKeyValidator _objectKeyValidator;
    private readonly IPresignedUrlCache? _presignedUrlCache;
    private readonly IReadOnlyList<IFileValidator> _customValidators;

    /// <param name="provider">The resolved provider that will perform the actual upload.</param>
    /// <param name="options">Global configuration (policies, profiles).</param>
    /// <param name="namingStrategyResolver">Resolves built-in and custom naming strategies.</param>
    /// <param name="objectKeyValidator">Validates generated object keys.</param>
    /// <param name="providerPolicyOverrides">Provider-level policy overrides, if any.</param>
    /// <param name="presignedUrlCache">Optional presigned URL cache for post-upload warm-up.</param>
    /// <param name="customValidators">Application validators inserted by their order.</param>
    public UploadPipeline(
        IStorageProvider provider,
        StorageFlowOptions options,
        INamingStrategyResolver namingStrategyResolver,
        IObjectKeyValidator objectKeyValidator,
        ProviderPolicyOverrides? providerPolicyOverrides = null,
        IPresignedUrlCache? presignedUrlCache = null,
        IReadOnlyList<IFileValidator>? customValidators = null)
    {
        _provider = provider;
        _options = options;
        _providerName = provider.ProviderName;
        _namingStrategyResolver = namingStrategyResolver;
        _objectKeyValidator = objectKeyValidator;
        _presignedUrlCache = presignedUrlCache;
        _customValidators = customValidators ?? [];
        ProviderPolicyOverrides = providerPolicyOverrides ?? new ProviderPolicyOverrides();
    }

    internal ProviderPolicyOverrides ProviderPolicyOverrides { get; }

    /// <summary>
    /// Runs the full upload pipeline for the given bucket, stream, and options.
    /// </summary>
    internal async Task<StorageResult<UploadResult>> ExecuteAsync(
        string bucket,
        UploadRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // ── Step 1: Validation ──────────────────────────────────────────
            Stream content = request.Content!;

            if (request.ValidationPolicyKey is not null)
            {
                var policy = ResolveValidationPolicy(request.ValidationPolicyKey);
                PrefixReplayStream? replayStream = null;
                if (policy.RequireValidSignature && !content.CanSeek)
                {
                    replayStream = await PrefixReplayStream.CreateAsync(
                        content,
                        FileSignatureValidator.MaximumSignatureLength,
                        cancellationToken);
                    content = replayStream;
                }

                var validators = BuildValidators(policy);
                var context = new FileValidationContext
                {
                    Content = content,
                    FileName = request.FileName!,
                    ContentType = request.ContentType,
                    ContentLength = request.ContentLength
                };

                foreach (var validator in validators.OrderBy(v => v.Order))
                {
                    var result = await validator.ValidateAsync(context, cancellationToken);
                    if (!result.IsValid)
                        return StorageResult<UploadResult>.Failure(
                            StorageError.Create(StorageErrorCode.ValidationFailed, result.ErrorMessage!));

                    if (replayStream is not null && !replayStream.TryResetPrefix())
                    {
                        return StorageResult<UploadResult>.Failure(
                            StorageError.Create(
                                StorageErrorCode.ValidationFailed,
                                $"Validator '{validator.GetType().FullName}' consumed more than the replayable " +
                                "signature prefix from a non-seekable upload stream."));
                    }
                }
            }

            // ── Step 2: Naming ──────────────────────────────────────────────
            var namingPolicy = ResolveNamingPolicy(request.NamingPolicyKey);
            var namingStrategy = _namingStrategyResolver.Resolve(namingPolicy);
            var namingContext = new FileNamingContext
            {
                OriginalFileName = request.FileName!,
                Pattern = namingPolicy.Pattern,
                UploadedAt = DateTimeOffset.UtcNow
            };
            var objectKey = await namingStrategy.GenerateAsync(namingContext, cancellationToken);
            _objectKeyValidator.Validate(objectKey);

            // ── Step 3: Provider Upload ─────────────────────────────────────
            var uploadResult = await _provider.UploadAsync(
                bucket,
                objectKey,
                content,
                request.ContentType,
                request.ContentLength,
                request.Metadata.Count == 0 ? null : request.Metadata,
                new UploadHeaders
                {
                    CacheControl = request.CacheControl,
                    ContentDisposition = request.ContentDisposition
                },
                cancellationToken);

            // ── Step 4: Presigned URL Cache warm-up (optional) ──────────────
            if (_presignedUrlCache is not null && request.PresignedUrlPolicyKey is not null)
            {
                var presignedPolicy = ResolvePresignedUrlPolicy(request.PresignedUrlPolicyKey);
                var policyCacheKey = GetPolicyCacheKey(request.PresignedUrlPolicyKey);
                var url = await _provider.GetPresignedUrlAsync(
                    bucket, objectKey, presignedPolicy.Expiration, presignedPolicy.HttpMethod, cancellationToken);

                await _presignedUrlCache.SetAsync(
                    _providerName, bucket, objectKey, policyCacheKey,
                    url, presignedPolicy.Expiration, cancellationToken);
            }

            return StorageResult<UploadResult>.Success(uploadResult);
        }
        catch (StorageValidationException ex)
        {
            return StorageResult<UploadResult>.Failure(
                StorageError.Create(StorageErrorCode.ValidationFailed, ex.Message, ex));
        }
        catch (StorageProviderException ex)
        {
            return StorageResult<UploadResult>.Failure(
                StorageError.Create(ex.ErrorCode, ex.Message, ex));
        }
        catch (StorageNamingException ex)
        {
            return StorageResult<UploadResult>.Failure(
                StorageError.Create(StorageErrorCode.Unknown, ex.Message, ex));
        }
        catch (StorageConfigurationException ex)
        {
            return StorageResult<UploadResult>.Failure(
                StorageError.Create(StorageErrorCode.Unknown, ex.Message, ex));
        }
        catch (Exception ex)
        {
            return StorageResult<UploadResult>.Failure(
                StorageError.Create(StorageErrorCode.Unknown, $"Unexpected error during upload: {ex.Message}", ex));
        }
    }

    // ── Policy resolution (provider-level overrides first, then global) ───────

    internal ValidationPolicy ResolveValidationPolicy(Type policyKey)
    {
        if (ProviderPolicyOverrides.ValidationPolicies.TryGetValue(policyKey, out var providerPolicy))
            return providerPolicy;

        if (_options.GlobalValidationPolicies.TryGetValue(policyKey, out var globalPolicy))
            return globalPolicy;

        throw new StorageConfigurationException(
            $"Validation policy '{policyKey.FullName}' was not found for provider '{_providerName}' " +
            "and is not defined as a global policy.");
    }

    internal PresignedUrlPolicy ResolvePresignedUrlPolicy(Type policyKey)
    {
        if (ProviderPolicyOverrides.PresignedUrlPolicies.TryGetValue(policyKey, out var providerPolicy))
            return providerPolicy;

        if (_options.GlobalPresignedUrlPolicies.TryGetValue(policyKey, out var globalPolicy))
            return globalPolicy;

        throw new StorageConfigurationException(
            $"Presigned URL policy '{policyKey.FullName}' was not found for provider '{_providerName}' " +
            "and is not defined as a global policy.");
    }

    internal NamingPolicy ResolveNamingPolicy(Type? policyKey)
    {
        policyKey ??= _options.DefaultNamingPolicyKey;

        if (policyKey is null)
            return new NamingPolicy().UseGuid();

        if (ProviderPolicyOverrides.NamingPolicies.TryGetValue(policyKey, out var providerPolicy))
            return providerPolicy;

        if (_options.GlobalNamingPolicies.TryGetValue(policyKey, out var globalPolicy))
            return globalPolicy;

        throw new StorageConfigurationException(
            $"Naming policy '{policyKey.FullName}' was not found for provider '{_providerName}' " +
            "and is not defined as a global policy.");
    }

    // ── Builder helpers ───────────────────────────────────────────────────────

    private IEnumerable<IFileValidator> BuildValidators(ValidationPolicy policy)
    {
        yield return new FileSizeValidator(policy);
        yield return new ExtensionValidator(policy);
        yield return new MimeTypeValidator(policy);
        yield return new FileSignatureValidator(policy);

        foreach (var validator in _customValidators)
            yield return validator;
    }

    private static string GetPolicyCacheKey(Type policyKey) =>
        policyKey.FullName ?? policyKey.Name;
}
