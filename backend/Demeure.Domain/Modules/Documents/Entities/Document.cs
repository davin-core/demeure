using Demeure.Domain.Common;

namespace Demeure.Domain.Modules.Documents.Entities;

public sealed class Document : OrganizationEntity
{
    public Guid? PropertyId { get; init; }
    public Guid? LeaseId { get; init; }
    public Guid? MaintenanceRequestId { get; init; }
    public required Guid UploadedByUserId { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required string StorageKey { get; init; }
    public required long SizeBytes { get; init; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Available;
}

public enum DocumentStatus { Available, Archived }
