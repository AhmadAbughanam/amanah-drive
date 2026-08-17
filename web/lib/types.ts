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

export type SearchResult = {
  chunkId: string;
  fileId: string;
  fileName: string;
  chunkIndex: number;
  snippet: string;
  score: number;
};

export type SearchResponse = {
  results: SearchResult[];
};

export type ChatCitation = {
  chunkId: string;
  fileId: string | null;
  fileName: string;
  snippet: string;
};

export type ChatResponse = {
  conversationId: string;
  answer: string;
  citations: ChatCitation[];
};

export type ChatMessageResponse = {
  id: string;
  role: string;
  content: string;
  citations: ChatCitation[];
  createdAt: string;
};

export type ChatHistoryResponse = {
  conversationId: string;
  createdAt: string;
  updatedAt: string;
  page: number;
  pageSize: number;
  messages: ChatMessageResponse[];
};

export type AdminLogEntry = {
  timestamp: string;
  level: string;
  message: string;
  exception: string | null;
  properties: Record<string, unknown>;
};

export type AdminLogResponse = {
  page: number;
  pageSize: number;
  hasMore: boolean;
  entries: AdminLogEntry[];
};

export type ActivityEntry = {
  id: string;
  type: string;
  summary: string;
  occurredAt: string;
  fileId: string | null;
  conversationId: string | null;
};

export type ActivityResponse = {
  page: number;
  pageSize: number;
  hasMore: boolean;
  entries: ActivityEntry[];
};

export type ApiErrorBody = {
  message?: string;
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
};
