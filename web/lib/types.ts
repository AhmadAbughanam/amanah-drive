export type AuthResponse = {
  accessToken: string;
};

export type Folder = {
  id: string;
  name: string;
  parentFolderId: string | null;
  createdAt: string;
  updatedAt: string;
};

export type FileItem = {
  id: string;
  folderId: string | null;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  checksumSha256: string;
  processingJobId: string | null;
  createdAt: string;
  updatedAt: string;
};

export type FolderContents = {
  parentFolderId: string | null;
  page: number;
  pageSize: number;
  folders: Folder[];
  files: FileItem[];
};

export type ApiErrorBody = {
  message?: string;
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
};
