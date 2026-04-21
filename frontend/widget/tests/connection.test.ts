import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SessionConnection } from '../src/connection.js';

type Listener = (event: unknown) => void;

class FakeWebSocket {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSED = 3;
  static instances: FakeWebSocket[] = [];

  readonly url: string;
  binaryType = '';
  readyState = FakeWebSocket.CONNECTING;
  sentMessages: ArrayBuffer[] = [];
  private readonly listeners = new Map<string, Listener[]>();

  constructor(url: string | URL) {
    this.url = url.toString();
    FakeWebSocket.instances.push(this);

    queueMicrotask(() => {
      this.readyState = FakeWebSocket.OPEN;
      this.emit('open', {});
    });
  }

  static reset(): void {
    FakeWebSocket.instances = [];
  }

  addEventListener(type: string, listener: Listener): void {
    const listeners = this.listeners.get(type) ?? [];
    listeners.push(listener);
    this.listeners.set(type, listeners);
  }

  send(message: ArrayBuffer): void {
    this.sentMessages.push(message);
  }

  close(code = 1000, reason = ''): void {
    this.readyState = FakeWebSocket.CLOSED;
    this.emit('close', { code, reason });
  }

  emit(type: string, event: unknown): void {
    const listeners = this.listeners.get(type) ?? [];
    listeners.forEach((listener) => listener(event));
  }
}

describe('SessionConnection', () => {
  beforeEach(() => {
    FakeWebSocket.reset();
    vi.stubGlobal('WebSocket', FakeWebSocket as unknown as typeof WebSocket);
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('requests a session token and opens the returned relay URL', async () => {
    vi.mocked(fetch).mockResolvedValue(
      createResponse({
        token: 'session-token',
        wsUrl: 'wss://platform.test/relay/connect',
        expiresAt: '2026-04-21T15:00:00Z',
      }),
    );

    const connection = new SessionConnection();
    await connection.connect({
      agentId: 'agent-123',
      gatewayUrl: 'https://platform.test',
    });

    expect(fetch).toHaveBeenCalledWith(
      'https://platform.test/api/config/agents/agent-123/session-token',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ agentId: 'agent-123', channel: 'web' }),
      }),
    );
    expect(FakeWebSocket.instances[0]?.url).toBe('wss://platform.test/relay/connect?token=session-token');
  });

  it('forwards outbound PCM after the socket opens', async () => {
    vi.mocked(fetch).mockResolvedValue(
      createResponse({
        token: 'session-token',
        wsUrl: 'wss://platform.test/relay/connect',
        expiresAt: '2026-04-21T15:00:00Z',
      }),
    );

    const connection = new SessionConnection();
    await connection.connect({ agentId: 'agent-123', gatewayUrl: 'https://platform.test' });

    const buffer = new ArrayBuffer(8);
    connection.send(buffer);

    expect(FakeWebSocket.instances[0]?.sentMessages).toEqual([buffer]);
  });

  it('delivers inbound binary messages to listeners', async () => {
    vi.mocked(fetch).mockResolvedValue(
      createResponse({
        token: 'session-token',
        wsUrl: 'wss://platform.test/relay/connect',
        expiresAt: '2026-04-21T15:00:00Z',
      }),
    );

    const connection = new SessionConnection();
    const listener = vi.fn();
    connection.onMessage(listener);

    await connection.connect({ agentId: 'agent-123', gatewayUrl: 'https://platform.test' });

    const payload = new ArrayBuffer(4);
    FakeWebSocket.instances[0]?.emit('message', { data: payload });

    expect(listener).toHaveBeenCalledWith(payload);
  });

  it('throws a clear error when rate limits reject the token request', async () => {
    vi.mocked(fetch).mockResolvedValue({
      ok: false,
      status: 429,
      json: async () => ({ error: 'Too many sessions' }),
    } as Response);

    const connection = new SessionConnection();

    await expect(
      connection.connect({
        agentId: 'agent-123',
        gatewayUrl: 'https://platform.test',
      }),
    ).rejects.toThrow('Rate limit reached. Please try again later.');
  });
});

function createResponse(body: unknown): Response {
  return {
    ok: true,
    status: 200,
    json: async () => body,
  } as Response;
}