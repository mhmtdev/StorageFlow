using StorageFlow.Abstractions.Interfaces;

namespace StorageFlow.Tests.Integration.Infrastructure;

public sealed class IntegrationNaming : INamingPolicyKey;

public sealed class IntegrationDownloadPolicy : IPresignedUrlPolicyKey;
