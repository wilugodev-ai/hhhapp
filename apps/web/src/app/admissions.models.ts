import { SourceFile, SourcePage } from "./healthcare-files.models";

export interface Admission {
  encounterId: string;
  patientName: string;
  facilityName: string;
  admittedAt: string;
  dischargedAt: string | null;
  description: string;
  reason: string | null;
}

export interface AdmissionPage {
  total: number;
  patients: number;
  facilities: number;
  page: number;
  pageSize: number;
  items: Admission[];
}

export interface AdmissionRecords extends SourcePage {
  tables: SourceFile[];
}
