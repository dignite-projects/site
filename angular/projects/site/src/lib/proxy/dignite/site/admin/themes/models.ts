import type { SiteThemeFileStatus } from '../../themes/site-theme-file-status.enum';
import type { SiteThemeIssueSeverity } from '../../themes/site-theme-issue-severity.enum';
import type { SiteThemeUsageKindDto } from './site-theme-usage-kind-dto.enum';

export interface PublishSiteThemeDto {
  fingerprint: string;
  comment?: string;
}

export interface RenameSiteThemeFileDto {
  path: string;
  newPath: string;
  updateReferences: boolean;
}

export interface SaveSiteThemeFileDto {
  path: string;
  body: string;
  concurrencyStamp?: string;
}

export interface SiteReleaseDetailDto extends SiteReleaseDto {
  files: SiteReleaseFileDto[];
}

export interface SiteReleaseDto {
  id?: string;
  number: number;
  comment?: string;
  defaultLayout?: string;
  changedFileCount: number;
  creationTime?: string;
  creatorId?: string;
  isLive: boolean;
}

export interface SiteReleaseFileContentDto extends SiteReleaseFileDto {
  body?: string;
}

export interface SiteReleaseFileDto {
  path?: string;
  isAsset: boolean;
  contentHash?: string;
}

export interface SiteThemeCheckDto {
  fingerprint?: string;
  issues: SiteThemeIssueDto[];
  hasErrors: boolean;
  layouts: string[];
  changes: SiteThemeFileChangeDto[];
  defaultLayout?: string;
  liveDefaultLayout?: string;
  liveReleaseNumber?: number;
  hasChanges: boolean;
}

export interface SiteThemeDto {
  defaultLayout?: string;
  liveReleaseId?: string;
  liveReleaseNumber?: number;
  concurrencyStamp?: string;
}

export interface SiteThemeFileChangeDto {
  path?: string;
  status: SiteThemeFileStatus;
}

export interface SiteThemeFileDto {
  path?: string;
  isAsset: boolean;
  body?: string;
  contentHash?: string;
  concurrencyStamp?: string;
  lastModificationTime?: string;
  syntaxErrors: SiteThemeSyntaxErrorDto[];
}

export interface SiteThemeFileSummaryDto {
  path?: string;
  isAsset: boolean;
  contentHash?: string;
  status: SiteThemeFileStatus;
  lastModificationTime?: string;
}

export interface SiteThemeFileUsageDto {
  kind: SiteThemeUsageKindDto;
  sourcePath?: string;
  pageName?: string;
}

export interface SiteThemeFileUsagesDto {
  path?: string;
  usages: SiteThemeFileUsageDto[];
}

export interface SiteThemeIssueDto {
  severity: SiteThemeIssueSeverity;
  code?: string;
  path?: string;
  line?: number;
  column?: number;
  message?: string;
}

export interface SiteThemeRenameResultDto {
  file: SiteThemeFileDto;
  updatedPaths: string[];
  defaultLayoutUpdated: boolean;
  pagesStillUsingOldPath: string[];
}

export interface SiteThemeSyntaxErrorDto {
  line: number;
  column: number;
  message?: string;
}

export interface UpdateSiteThemeDto {
  defaultLayout?: string;
  concurrencyStamp?: string;
}
