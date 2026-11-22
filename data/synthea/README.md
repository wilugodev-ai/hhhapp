# Synthea clinical sample

`sample-csv.zip` is the published Synthea CSV sample used in HHH. The archive contains 18 related files with 412,443 source rows, including 120 synthetic patients and 6,762 encounters. Of those encounters, 172 have an `ENCOUNTERCLASS` of `inpatient`. The archive SHA-256 is `ce211a05f27022ff1038000243a9291825ceb0107394e399b14c967948c030c3`. [Synthea downloads](https://synthetichealth.github.io/downloads.html) · [CSV data dictionary](https://github.com/synthetichealth/synthea/wiki/CSV-File-Data-Dictionary)

From the repository root, load the sample into an existing HHH organization using its ID.

```powershell
dotnet run --project src/Hhh.Operator -- import-synthea <organization-id> data/synthea/sample-csv.zip
```

The default destination is the local SQL Server database `HhhLocal`. The operator also accepts the application's `ConnectionStrings__Hhh` and `Database__Provider` settings. It applies migrations, imports the files in one transaction and reports row counts for every source file. Repeating the same import returns the existing counts.

In HHH, open **Admissions** and select a patient to inspect the source rows for that stay. **Open FHIR JSON** returns the stay as an HL7 FHIR R4 `Encounter`. Admission and discharge times use the source's UTC timestamps.

Open **Healthcare files** to browse all 18 CSV files across visits. Claims contains 14,816 claim headers and links each claim ID to its transactions. The transaction file contains 171,480 rows. Pharmacy prescriptions contains 8,054 medication records with dispense counts and costs. Membership coverage contains 5,276 coverage periods for the sample population, including periods with no insurance. Choose **Show original fields** to see the original identifiers and values.

For SQL analysis, `SyntheaRecords` preserves every CSV field in `FieldsJson`, with the organization, source table, row number and available patient and encounter IDs. `SyntheaAdmissions` links inpatient encounters to the patient and facility directories. `SyntheaDatasets` stores the source fingerprint and import time.

Walonoski et al. describe the source in *Synthea: An approach, method, and software mechanism for generating synthetic patients and the synthetic electronic health care record*. [Journal of the American Medical Informatics Association](https://doi.org/10.1093/jamia/ocx079)
