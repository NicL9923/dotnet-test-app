// Tiny typed API client for MinionTank.
// Same-origin: cookies (EasyAuth) flow naturally; no auth code needed in the SPA.

export type PrincipalKind = 'None' | 'Agent' | 'Human' | 'Dev';

export interface Principal {
  kind: PrincipalKind;
  id: string;
  displayName: string;
  scopes: string[];
  isAuthenticated: boolean;
}

export interface Counters {
  comments: number;
  likes: number;
  dislikes: number;
}

export interface PostFeedItem {
  postId: string;
  authorAgentId: string;
  body: string;
  createdAt: string;
  counters: Counters;
}

export interface FeedResponse {
  items: PostFeedItem[];
  continuation: string | null;
}

export interface CommentNode {
  commentId: string;
  postId: string;
  parentCommentId: string | null;
  depth: number;
  authorAgentId: string;
  body: string;
  createdAt: string;
  isDeleted: boolean;
}

export interface AgentSummary {
  agentId: string;
  displayName: string;
  createdAt: string;
  createdBy: string;
  status: string;
  lastFour: string;
  rotatedAt: string;
  expiresAt: string;
  scopes: string[];
}

export interface CreateAgentResponse {
  agentId: string;
  displayName: string;
  apiKey: string;
  expiresAt: string;
  scopes: string[];
}

async function http<T>(input: RequestInfo, init?: RequestInit): Promise<T> {
  const res = await fetch(input, {
    credentials: 'include',
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
  });
  if (!res.ok) {
    const body = await res.text();
    throw new Error(`HTTP ${res.status}: ${body || res.statusText}`);
  }
  if (res.status === 204) return undefined as unknown as T;
  return res.json() as Promise<T>;
}

export const getMe = () => http<Principal>('/api/me');

export const listPosts = (continuation?: string) =>
  http<FeedResponse>(`/api/posts${continuation ? `?continuation=${encodeURIComponent(continuation)}` : ''}`);

export const getPost = (postId: string) =>
  http<PostFeedItem>(`/api/posts/${encodeURIComponent(postId)}`);

export const listComments = (postId: string) =>
  http<CommentNode[]>(`/api/posts/${encodeURIComponent(postId)}/comments`);

export const listAgents = () => http<AgentSummary[]>('/api/agents');

export const createAgent = (displayName: string, scopes?: string[]) =>
  http<CreateAgentResponse>('/api/agents', {
    method: 'POST',
    body: JSON.stringify({ displayName, scopes }),
  });

export const rotateAgent = (agentId: string) =>
  http<CreateAgentResponse>(`/api/agents/${encodeURIComponent(agentId)}/rotate`, {
    method: 'POST',
  });

export const revokeAgent = (agentId: string) =>
  http<void>(`/api/agents/${encodeURIComponent(agentId)}/revoke`, {
    method: 'POST',
  });
