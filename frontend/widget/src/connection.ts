export interface ConnectionOptions {
  gatewayUrl: string;
  agentId: string;
  channel?: 'web' | 'phone' | 'whatsapp';
}

interface SessionTokenResponse {
  token: string;
  wsUrl: string;
  expiresAt: string;
}

export type MessageCallback = (data: ArrayBuffer) => void;
export type CloseCallback = (code: number, reason: string) => void;
export type ErrorCallback = (error: Event) => void;

export class SessionConnection {
  private _socket: WebSocket | null = null;
  private _messageCallbacks: MessageCallback[] = [];
  private _closeCallbacks: CloseCallback[] = [];
  private _errorCallbacks: ErrorCallback[] = [];

  async connect(options: ConnectionOptions): Promise<void> {
    const session = await this._fetchSessionToken(options);
    await this._openWebSocket(session.wsUrl, session.token);
  }

  send(pcm: ArrayBuffer): void {
    if (this._socket?.readyState === WebSocket.OPEN) {
      this._socket.send(pcm);
    }
  }

  close(): void {
    this._socket?.close(1000, 'Session ended by user');
    this._socket = null;
  }

  onMessage(callback: MessageCallback): () => void {
    this._messageCallbacks.push(callback);
    return () => {
      const i = this._messageCallbacks.indexOf(callback);
      if (i !== -1) this._messageCallbacks.splice(i, 1);
    };
  }

  onClose(callback: CloseCallback): () => void {
    this._closeCallbacks.push(callback);
    return () => {
      const i = this._closeCallbacks.indexOf(callback);
      if (i !== -1) this._closeCallbacks.splice(i, 1);
    };
  }

  onError(callback: ErrorCallback): () => void {
    this._errorCallbacks.push(callback);
    return () => {
      const i = this._errorCallbacks.indexOf(callback);
      if (i !== -1) this._errorCallbacks.splice(i, 1);
    };
  }

  private async _fetchSessionToken(options: ConnectionOptions): Promise<SessionTokenResponse> {
    const response = await fetch(
      `${options.gatewayUrl}/api/config/agents/${options.agentId}/session-token`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          agentId: options.agentId,
          channel: options.channel ?? 'web',
        }),
      }
    );

    if (!response.ok) {
      const status = response.status;
      if (status === 401) throw new Error('Unauthorized: check agent ID.');
      if (status === 429) throw new Error('Rate limit reached. Please try again later.');
      throw new Error(`Token request failed with status ${status}.`);
    }

    const body = await response.json() as SessionTokenResponse;
    if (!body.token || !body.wsUrl) {
      throw new Error('Token response did not include a relay URL.');
    }

    return body;
  }

  private _openWebSocket(wsUrl: string, token: string): Promise<void> {
    return new Promise((resolve, reject) => {
      const socketUrl = new URL(normalizeSessionRelayUrl(wsUrl));
      socketUrl.searchParams.set('token', normalizeSessionToken(token));

      const socket = new WebSocket(socketUrl.toString());
      socket.binaryType = 'arraybuffer';

      const timeout = setTimeout(() => {
        socket.close();
        reject(new Error('WebSocket connection timed out.'));
      }, 10_000);

      socket.addEventListener('open', () => {
        clearTimeout(timeout);
        this._socket = socket;
        resolve();
      });

      socket.addEventListener('error', (event) => {
        clearTimeout(timeout);
        this._errorCallbacks.forEach(cb => cb(event));
        reject(new Error('WebSocket connection failed.'));
      });

      socket.addEventListener('message', (event) => {
        if (event.data instanceof ArrayBuffer) {
          this._messageCallbacks.forEach(cb => cb(event.data));
        }
      });

      socket.addEventListener('close', (event) => {
        this._closeCallbacks.forEach(cb => cb(event.code, event.reason));
      });
    });
  }
}

function normalizeSessionRelayUrl(wsUrl: string): string {
  return wsUrl.trim();
}

function normalizeSessionToken(token: string): string {
  const normalizedToken = trimJwtBoundaryNoise(token);
  const parts = normalizedToken.split('.');

  if (parts.length !== 3 || parts.some((part) => part.length === 0 || !isJwtSegment(part))) {
    throw new Error('Token response returned an invalid session token.');
  }

  return normalizedToken;
}

function trimJwtBoundaryNoise(value: string): string {
  let start = 0;
  let end = value.length;

  while (start < end && !isJwtBoundaryCharacter(value[start]!)) {
    start += 1;
  }

  while (end > start && !isJwtBoundaryCharacter(value[end - 1]!)) {
    end -= 1;
  }

  return value.slice(start, end);
}

function isJwtBoundaryCharacter(character: string): boolean {
  return /^[A-Za-z0-9._-]$/.test(character);
}

function isJwtSegment(segment: string): boolean {
  return /^[A-Za-z0-9_-]+$/.test(segment);
}
