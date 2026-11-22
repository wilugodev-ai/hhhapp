namespace Hhh.Domain;

public sealed class SyntheaDataset
{
    public Guid OrganizationId { get; set; }
    public required string ArchiveSha256 { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
}

public sealed class SyntheaRecord
{
    public Guid OrganizationId { get; set; }
    public required string SourceTable { get; set; }
    public int RowNumber { get; set; }
    public Guid? PatientId { get; set; }
    public Guid? EncounterId { get; set; }
    public required string FieldsJson { get; set; }
}

public sealed class SyntheaAdmission
{
    public Guid OrganizationId { get; set; }
    public Guid EncounterId { get; set; }
    public Guid PatientId { get; set; }
    public required string PatientName { get; set; }
    public Guid FacilityId { get; set; }
    public required string FacilityName { get; set; }
    public DateTimeOffset AdmittedAt { get; set; }
    public DateTimeOffset? DischargedAt { get; set; }
    public required string EncounterCode { get; set; }
    public required string Description { get; set; }
    public string? ReasonCode { get; set; }
    public string? Reason { get; set; }
}
