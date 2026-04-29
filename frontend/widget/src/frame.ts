import { BrowserAudioCapture } from './audio.js';
import { SessionConnection } from './connection.js';
import { WidgetStateMachine, type WidgetState } from './state.js';
import { CountdownTimer } from './timer.js';

interface FrameConfig {
  agentId: string;
  gatewayUrl: string;
  buttonLabel: string;
  sessionTimeoutSeconds: number;
}

// Pcm16Player must be declared before the module-level initialisation below.
// Class declarations are NOT hoisted in the Vite IIFE bundle — placing this after
// the init block causes `new Pcm16Player()` inside bootstrapFrame to receive
// `undefined` (the hoisted-but-uninitialised var binding), producing
// "TypeError: h is not a constructor".
class Pcm16Player {
  // Gemini Live outputs PCM at 24 kHz mono. The AudioContext and every AudioBuffer
  // must use 24 000 Hz — using 16 000 Hz here causes the audio to play at 1.5× speed
  // with wrong pitch, which sounds like distorted noise to the caller.
  private static readonly SAMPLE_RATE = 24_000;

  private _context: AudioContext | null = null;
  private readonly _activeSources = new Set<AudioBufferSourceNode>();
  // Tracks the absolute AudioContext time at which the next chunk should start.
  // When behind real-time (network stall) we clamp to currentTime so playback
  // never freezes. Scheduling prevents audible gaps between consecutive chunks.
  private _nextStartTime = 0;

  play(buffer: ArrayBuffer): void {
    if (!this._context) {
      this._context = new AudioContext({ sampleRate: Pcm16Player.SAMPLE_RATE });
      this._nextStartTime = this._context.currentTime;
    }

    const input = new Int16Array(buffer);
    const output = new Float32Array(input.length);
    for (let index = 0; index < input.length; index += 1) {
      output[index] = input[index] / 0x7fff;
    }

    const audioBuffer = this._context.createBuffer(1, output.length, Pcm16Player.SAMPLE_RATE);
    audioBuffer.copyToChannel(output, 0);

    const source = this._context.createBufferSource();
    source.buffer = audioBuffer;
    source.connect(this._context.destination);
    source.addEventListener('ended', () => {
      this._activeSources.delete(source);
    });

    // Schedule this chunk to start exactly where the previous one ended.
    // If we have fallen behind real-time (e.g. after a network hiccup), clamp
    // to currentTime so we never schedule into the past.
    const startTime = Math.max(this._context.currentTime, this._nextStartTime);
    this._activeSources.add(source);
    source.start(startTime);
    this._nextStartTime = startTime + audioBuffer.duration;
  }

  interrupt(): void {
    for (const source of this._activeSources) {
      try {
        source.stop();
      } catch {
        // Ignore sources that have already ended between iteration and stop().
      }
    }

    this._activeSources.clear();

    if (this._context) {
      this._nextStartTime = this._context.currentTime;
    }
  }

  close(): void {
    this.interrupt();
    this._context?.close();
    this._context = null;
    this._nextStartTime = 0;
  }
}

const config = readFrameConfig();

if (!config) {
  renderConfigurationError();
} else {
  bootstrapFrame(config);
}

function bootstrapFrame(frameConfig: FrameConfig): void {
  document.body.innerHTML = `
    <div class="axv-frame-app" data-state="idle">
      <style>
        :root {
          color: #201710;
          background: linear-gradient(180deg, #f8efe1 0%, #fffaf4 100%);
          font-family: Avenir Next, Segoe UI, sans-serif;
        }

        * {
          box-sizing: border-box;
        }

        body {
          margin: 0;
          min-height: 100vh;
          color: #201710;
        }

        .axv-frame-app {
          min-height: 100vh;
          padding: 24px;
          display: grid;
          place-items: center;
          background:
            radial-gradient(circle at top right, rgba(204, 142, 87, 0.32), transparent 30%),
            linear-gradient(180deg, #f8efe1 0%, #fffaf4 100%);
        }

        .axv-panel {
          width: min(460px, 100%);
          min-height: min(720px, calc(100vh - 48px));
          padding: 28px;
          display: flex;
          flex-direction: column;
          gap: 24px;
          border-radius: 32px;
          border: 1px solid rgba(100, 68, 42, 0.14);
          background: rgba(255, 250, 244, 0.92);
          box-shadow: 0 28px 96px rgba(54, 32, 17, 0.16);
        }

        .axv-header,
        .axv-footer {
          display: flex;
          align-items: center;
          justify-content: space-between;
          gap: 12px;
        }

        .axv-status {
          display: inline-flex;
          align-items: center;
          gap: 8px;
          padding: 8px 12px;
          border-radius: 999px;
          background: rgba(191, 106, 47, 0.12);
          color: #8f4c1d;
          font-size: 0.85rem;
          text-transform: uppercase;
          letter-spacing: 0.08em;
        }

        .axv-status-dot {
          width: 10px;
          height: 10px;
          border-radius: 50%;
          background: currentColor;
        }

        .axv-close {
          border: none;
          background: rgba(32, 23, 16, 0.06);
          color: #201710;
          width: 40px;
          height: 40px;
          border-radius: 999px;
          cursor: pointer;
          font: inherit;
        }

        .axv-copy h1 {
          margin: 0;
          font-size: clamp(2.1rem, 7vw, 3.1rem);
          line-height: 0.95;
          letter-spacing: -0.06em;
        }

        .axv-copy p {
          margin: 12px 0 0;
          color: rgba(48, 33, 22, 0.8);
        }

        .axv-orb {
          flex: 1;
          display: grid;
          place-items: center;
          border-radius: 28px;
          background:
            radial-gradient(circle at center, rgba(191, 106, 47, 0.28), rgba(191, 106, 47, 0) 55%),
            linear-gradient(180deg, rgba(255, 255, 255, 0.8), rgba(244, 232, 221, 0.9));
          border: 1px solid rgba(100, 68, 42, 0.12);
        }

        .axv-orb-core {
          width: 180px;
          height: 180px;
          border-radius: 50%;
          display: grid;
          place-items: center;
          background: linear-gradient(180deg, #c77736 0%, #9f4f17 100%);
          color: #fff8f1;
          box-shadow: 0 18px 48px rgba(155, 79, 23, 0.32);
          font-size: 3rem;
          transition: transform 180ms ease, box-shadow 180ms ease;
        }

        .axv-frame-app[data-state='speaking'] .axv-orb-core,
        .axv-frame-app[data-state='receiving'] .axv-orb-core,
        .axv-frame-app[data-state='connected'] .axv-orb-core {
          transform: scale(1.04);
          box-shadow: 0 22px 60px rgba(155, 79, 23, 0.42);
        }

        .axv-frame-app[data-state='error'] .axv-orb-core {
          background: linear-gradient(180deg, #cf5d49 0%, #9f2c20 100%);
        }

        .axv-meta {
          display: grid;
          gap: 12px;
        }

        .axv-meta-row {
          display: flex;
          justify-content: space-between;
          gap: 16px;
          padding: 14px 16px;
          border-radius: 18px;
          background: rgba(255, 255, 255, 0.72);
          border: 1px solid rgba(100, 68, 42, 0.1);
        }

        .axv-meta-label {
          color: rgba(48, 33, 22, 0.65);
        }

        .axv-meta-value {
          font-weight: 600;
          color: #201710;
        }

        .axv-error {
          min-height: 1.4rem;
          color: #9f2c20;
          margin: 0;
        }

        .axv-primary {
          width: 100%;
          border: none;
          border-radius: 18px;
          padding: 16px 18px;
          background: #201710;
          color: #fff8f1;
          cursor: pointer;
          font: inherit;
          font-weight: 600;
        }

        .axv-primary:hover {
          filter: brightness(1.08);
        }

        .axv-primary:focus-visible,
        .axv-close:focus-visible {
          outline: 2px solid #8f4c1d;
          outline-offset: 2px;
        }

        @media (max-width: 640px) {
          .axv-frame-app {
            padding: 0;
          }

          .axv-panel {
            width: 100%;
            min-height: 100vh;
            border-radius: 0;
            padding: 20px;
          }
        }
      </style>
      <section class="axv-panel">
        <header class="axv-header">
          <div class="axv-status">
            <span class="axv-status-dot" aria-hidden="true"></span>
            <span id="status-label">Idle</span>
          </div>
          <button id="close-frame" class="axv-close" type="button" aria-label="Close voice widget">x</button>
        </header>
        <section class="axv-copy">
          <h1>${escapeHtml(frameConfig.buttonLabel)}</h1>
          <p>Speak naturally in Sinhala, Tamil, or English. The session stays inside this isolated frame.</p>
        </section>
        <section class="axv-orb" aria-hidden="true">
          <div id="orb-core" class="axv-orb-core">O</div>
        </section>
        <section class="axv-meta">
          <div class="axv-meta-row">
            <span class="axv-meta-label">Session</span>
            <span id="session-state" class="axv-meta-value">Ready</span>
          </div>
          <div class="axv-meta-row">
            <span class="axv-meta-label">Remaining</span>
            <span id="session-timer" class="axv-meta-value">${CountdownTimer.format(frameConfig.sessionTimeoutSeconds)}</span>
          </div>
        </section>
        <p id="error-text" class="axv-error" role="status" aria-live="polite"></p>
        <footer class="axv-footer">
          <button id="primary-action" class="axv-primary" type="button">Start conversation</button>
        </footer>
      </section>
    </div>
  `;

  const root = document.querySelector<HTMLElement>('.axv-frame-app');
  const statusLabel = document.querySelector<HTMLElement>('#status-label');
  const sessionState = document.querySelector<HTMLElement>('#session-state');
  const timerLabel = document.querySelector<HTMLElement>('#session-timer');
  const primaryAction = document.querySelector<HTMLButtonElement>('#primary-action');
  const closeFrame = document.querySelector<HTMLButtonElement>('#close-frame');
  const errorText = document.querySelector<HTMLElement>('#error-text');
  const orbCore = document.querySelector<HTMLElement>('#orb-core');

  if (!root || !statusLabel || !sessionState || !timerLabel || !primaryAction || !closeFrame || !errorText || !orbCore) {
    throw new Error('Widget frame failed to render.');
  }

  const timerDisplay = timerLabel;

  const stateMachine = new WidgetStateMachine();
  const connection = new SessionConnection();
  const audioCapture = new BrowserAudioCapture();
  const timer = new CountdownTimer(frameConfig.sessionTimeoutSeconds);
  const player = new Pcm16Player();

  let stopping = false;

  const finalizeStop = (): void => {
    timer.stop();
    audioCapture.stop();
    player.close();
    stopping = false;

    if (stateMachine.current === 'ending') {
      stateMachine.transition('idle');
      return;
    }

    if (stateMachine.current !== 'idle' && stateMachine.current !== 'error') {
      stateMachine.transition('idle');
    }
  };

  const moveToError = (message: string): void => {
    timer.stop();
    audioCapture.stop();
    player.close();

    if (stateMachine.current === 'requesting-token' || stateMachine.current === 'connecting' || stateMachine.current === 'ending') {
      stateMachine.transition('error');
    } else if (stateMachine.current === 'connected' || stateMachine.current === 'speaking' || stateMachine.current === 'receiving') {
      stateMachine.transition('error');
    }

    errorText.textContent = message;
  };

  stateMachine.onStateChange((_, next) => {
    root.setAttribute('data-state', next);
    applyStateText(next, statusLabel, sessionState, primaryAction, orbCore);

    if (next !== 'error') {
      errorText.textContent = '';
    }
  });

  timer.onTick((remaining) => {
    timerLabel.textContent = CountdownTimer.format(remaining);
  });

  timer.onExpired(() => {
    void stopSession();
  });

  audioCapture.onChunk((buffer) => {
    connection.send(buffer);
    if (stateMachine.current === 'connected' || stateMachine.current === 'receiving') {
      stateMachine.transition('speaking');
    }
  });

  audioCapture.onSpeechEnded(() => {
    connection.sendAudioStreamEnd();
  });

  connection.onMessage((buffer) => {
    if (stateMachine.current === 'connected' || stateMachine.current === 'speaking') {
      stateMachine.transition('receiving');
    }

    player.play(buffer);
  });

  connection.onControlMessage((type) => {
    if (type === 'interrupt_playback') {
      player.interrupt();
    }
  });

  connection.onError(() => {
    moveToError('The relay connection failed. Please try again.');
  });

  connection.onClose((code) => {
    if (stopping) {
      finalizeStop();
      return;
    }

    if (code === 1000 || code === 1001) {
      finalizeStop();
      return;
    }

    moveToError('The session closed unexpectedly.');
  });

  primaryAction.addEventListener('click', () => {
    if (stateMachine.current === 'idle' || stateMachine.current === 'error') {
      void startSession();
      return;
    }

    void stopSession();
  });

  closeFrame.addEventListener('click', () => {
    void stopSession();
    notifyParentClose();
  });

  window.addEventListener('message', (event) => {
    if (event.data && typeof event.data === 'object' && 'type' in event.data) {
      const message = event.data as { type?: string };
      if (message.type === 'axonvoice:parent-close') {
        void stopSession();
      }
    }
  });

  window.addEventListener('beforeunload', () => {
    void stopSession();
  });

  async function startSession(): Promise<void> {
    try {
      if (stateMachine.current === 'error') {
        stateMachine.transition('idle');
      }

      timer.reset();
      timerDisplay.textContent = CountdownTimer.format(frameConfig.sessionTimeoutSeconds);
      stateMachine.transition('requesting-token');

      const audioStartTask = audioCapture.start();

      const connectionTask = connection.connect({
        agentId: frameConfig.agentId,
        gatewayUrl: frameConfig.gatewayUrl,
        channel: 'web',
      });

      await audioStartTask;
      stateMachine.transition('connecting');
      await connectionTask;
      stateMachine.transition('connected');
      timer.start();
    } catch (error) {
      audioCapture.stop();
      connection.close();
      moveToError(toErrorMessage(error));
    }
  }

  async function stopSession(): Promise<void> {
    if (stateMachine.current === 'idle') {
      return;
    }

    stopping = true;

    if (stateMachine.current !== 'error' && stateMachine.current !== 'ending') {
      stateMachine.transition('ending');
    }

    timer.stop();
    audioCapture.stop();
    connection.close();

    if (stateMachine.current === 'error') {
      stateMachine.transition('idle');
      stopping = false;
      return;
    }

    if (stateMachine.current === 'ending') {
      stateMachine.transition('idle');
      stopping = false;
    }
  }
}

function applyStateText(
  state: WidgetState,
  statusLabel: HTMLElement,
  sessionState: HTMLElement,
  primaryAction: HTMLButtonElement,
  orbCore: HTMLElement,
): void {
  switch (state) {
    case 'idle':
      statusLabel.textContent = 'Idle';
      sessionState.textContent = 'Ready';
      primaryAction.textContent = 'Start conversation';
      orbCore.textContent = 'O';
      return;
    case 'requesting-token':
      statusLabel.textContent = 'Starting';
      sessionState.textContent = 'Requesting a session token';
      primaryAction.textContent = 'Preparing';
      orbCore.textContent = '...';
      return;
    case 'connecting':
      statusLabel.textContent = 'Connecting';
      sessionState.textContent = 'Opening the relay socket';
      primaryAction.textContent = 'Connecting';
      orbCore.textContent = '...';
      return;
    case 'connected':
      statusLabel.textContent = 'Live';
      sessionState.textContent = 'Listening for your first question';
      primaryAction.textContent = 'End conversation';
      orbCore.textContent = 'O';
      return;
    case 'speaking':
      statusLabel.textContent = 'Listening';
      sessionState.textContent = 'Streaming your microphone audio';
      primaryAction.textContent = 'End conversation';
      orbCore.textContent = 'Mic';
      return;
    case 'receiving':
      statusLabel.textContent = 'Responding';
      sessionState.textContent = 'Playing the assistant audio response';
      primaryAction.textContent = 'End conversation';
      orbCore.textContent = 'AI';
      return;
    case 'ending':
      statusLabel.textContent = 'Ending';
      sessionState.textContent = 'Closing the live session';
      primaryAction.textContent = 'Closing';
      orbCore.textContent = '...';
      return;
    case 'error':
      statusLabel.textContent = 'Error';
      sessionState.textContent = 'The session needs to be restarted';
      primaryAction.textContent = 'Try again';
      orbCore.textContent = '!';
      return;
  }
}

function readFrameConfig(): FrameConfig | null {
  const params = new URLSearchParams(window.location.search);
  const agentId = params.get('agentId');
  const gatewayUrl = params.get('gatewayUrl');
  const buttonLabel = params.get('buttonLabel') ?? 'Talk to us';
  const sessionTimeoutSeconds = Number(params.get('sessionTimeoutSeconds') ?? '300');

  if (!agentId || !gatewayUrl) {
    return null;
  }

  return {
    agentId,
    gatewayUrl,
    buttonLabel,
    sessionTimeoutSeconds: Number.isFinite(sessionTimeoutSeconds) ? sessionTimeoutSeconds : 300,
  };
}

function renderConfigurationError(): void {
  document.body.innerHTML = `
    <main style="display:grid;place-items:center;min-height:100vh;padding:24px;font-family:Avenir Next, Segoe UI, sans-serif;background:#f8efe1;color:#201710;">
      <section style="width:min(420px,100%);padding:28px;border-radius:24px;background:rgba(255,250,244,0.92);border:1px solid rgba(100,68,42,0.14);box-shadow:0 28px 96px rgba(54,32,17,0.12);">
        <h1 style="margin:0 0 12px;font-size:2rem;line-height:1;letter-spacing:-0.05em;">Widget misconfigured</h1>
        <p style="margin:0;color:rgba(48,33,22,0.8);">The frame did not receive an agentId or gatewayUrl from the loader.</p>
      </section>
    </main>
  `;
}

function notifyParentClose(): void {
  window.parent.postMessage({ type: 'axonvoice:close' }, '*');
}

function escapeHtml(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

function toErrorMessage(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }

  return 'Unable to start the session.';
}