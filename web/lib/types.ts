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
  reference: number;
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

export type AgentRunStepResponse = {
  sequence: number;
  role: string;
  content: string | null;
  toolName: string | null;
  argumentsSummary: string | null;
  resultSummary: string | null;
  status: string | null;
  requiresApproval: boolean;
  createdAt: string;
};

export type AgentRunResponse = {
  id: string;
  status: "AwaitingApproval" | "Completed" | "IterationLimitReached" | "Failed";
  finalAnswer: string | null;
  failureReason: string | null;
  pendingToolName: string | null;
  pendingActionSummary: string | null;
  steps: AgentRunStepResponse[];
  createdAt: string;
  updatedAt: string;
};

type AdminLogEntry = {
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

type ActivityEntry = {
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

type ObservabilityStats = {
  requestsToday: number;
  errorRatePercent: number;
  averageLatencyMilliseconds: number;
  aiSpendThisMonthUsd: number;
  aiPricingComplete: boolean;
};

type RequestMetricPoint = {
  timestamp: string;
  requests: number;
  errors: number;
  errorRatePercent: number;
};

type LogLevelCount = {
  level: string;
  count: number;
};

type AiUsageMetricPoint = {
  timestamp: string;
  inputTokens: number;
  outputTokens: number;
  estimatedCostUsd: number;
  operations: number;
  failures: number;
  unpricedOperations: number;
};

type SecurityMetricPoint = {
  timestamp: string;
  events: number;
};

type SecurityEventSummary = {
  timestamp: string;
  event: string;
  message: string;
  source: string;
};

type TopErrorSummary = {
  signature: string;
  message: string;
  exceptionType: string | null;
  level: string;
  count: number;
  lastSeen: string;
};

export type ObservabilitySnapshot = {
  range: "24h" | "7d" | "30d";
  from: string;
  to: string;
  stats: ObservabilityStats;
  requests: RequestMetricPoint[];
  logLevels: LogLevelCount[];
  aiUsage: AiUsageMetricPoint[];
  security: SecurityMetricPoint[];
  recentSecurityEvents: SecurityEventSummary[];
  topErrors: TopErrorSummary[];
};

export type ApiErrorBody = {
  message?: string;
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
};
