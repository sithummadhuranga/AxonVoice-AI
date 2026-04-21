export interface VoiceAgentConfig {
  agentId: string;
  gatewayUrl?: string;
  buttonLabel?: string;
  buttonColor?: string;
  position?: 'bottom-right' | 'bottom-left';
  language?: string;
  sessionTimeoutSeconds?: number;
  frameUrl?: string;
}

export interface WidgetController {
  open(): void;
  close(): void;
  destroy(): void;
}

interface WidgetApi {
  init(config?: VoiceAgentConfig): WidgetController;
  destroy(): void;
}

interface NormalizedVoiceAgentConfig {
  agentId: string;
  gatewayUrl: string;
  buttonLabel: string;
  buttonColor: string;
  position: 'bottom-right' | 'bottom-left';
  language?: string;
  sessionTimeoutSeconds?: number;
  frameUrl: string;
}

declare global {
  interface Window {
    VoiceAgent?: VoiceAgentConfig;
    AxonVoiceWidget?: WidgetApi;
  }
}

const loaderScriptUrl = resolveLoaderScriptUrl();
let activeController: WidgetController | null = null;

export function init(config?: VoiceAgentConfig): WidgetController {
  const normalized = normalizeConfig(config ?? window.VoiceAgent);

  activeController?.destroy();
  activeController = createWidgetController(normalized);

  return activeController;
}

export function destroy(): void {
  activeController?.destroy();
  activeController = null;
}

function createWidgetController(config: NormalizedVoiceAgentConfig): WidgetController {
  const root = document.createElement('div');
  root.setAttribute('data-axonvoice-loader', '');

  const style = document.createElement('style');
  style.textContent = `
    [data-axonvoice-loader] {
      position: fixed;
      inset: auto;
      z-index: 2147483647;
      font-family: Avenir Next, Segoe UI, sans-serif;
    }

    [data-axonvoice-loader] .axv-launcher {
      position: fixed;
      bottom: 24px;
      ${config.position === 'bottom-left' ? 'left: 24px;' : 'right: 24px;'}
      display: inline-flex;
      align-items: center;
      gap: 10px;
      padding: 12px 16px;
      border: none;
      border-radius: 999px;
      background: ${config.buttonColor};
      color: #fff9f2;
      cursor: pointer;
      box-shadow: 0 24px 48px rgba(41, 23, 10, 0.2);
      font: inherit;
    }

    [data-axonvoice-loader] .axv-launcher:hover {
      filter: brightness(1.04);
    }

    [data-axonvoice-loader] .axv-launcher:focus-visible {
      outline: 2px solid #fff;
      outline-offset: 2px;
    }

    [data-axonvoice-loader] .axv-launcher-dot {
      width: 10px;
      height: 10px;
      border-radius: 50%;
      background: rgba(255, 249, 242, 0.88);
      box-shadow: 0 0 0 8px rgba(255, 249, 242, 0.15);
    }

    [data-axonvoice-loader] .axv-overlay {
      position: fixed;
      inset: 0;
      display: none;
      background: rgba(26, 16, 10, 0.28);
      backdrop-filter: blur(8px);
    }

    [data-axonvoice-loader] .axv-overlay[data-open='true'] {
      display: block;
    }

    [data-axonvoice-loader] .axv-frame {
      width: 100%;
      height: 100%;
      border: 0;
      background: transparent;
    }
  `;

  const launcher = document.createElement('button');
  launcher.className = 'axv-launcher';
  launcher.type = 'button';
  launcher.innerHTML = `<span class="axv-launcher-dot" aria-hidden="true"></span><span>${escapeHtml(config.buttonLabel)}</span>`;
  launcher.setAttribute('aria-label', config.buttonLabel);

  const overlay = document.createElement('div');
  overlay.className = 'axv-overlay';
  overlay.setAttribute('data-open', 'false');

  const frame = document.createElement('iframe');
  frame.className = 'axv-frame';
  frame.title = config.buttonLabel;
  frame.allow = 'microphone';

  overlay.appendChild(frame);
  root.append(style, launcher, overlay);
  document.body.appendChild(root);

  const frameOrigin = new URL(config.frameUrl).origin;

  const open = (): void => {
    if (frame.src !== buildFrameUrl(config)) {
      frame.src = buildFrameUrl(config);
    }
    overlay.setAttribute('data-open', 'true');
  };

  const close = (): void => {
    overlay.setAttribute('data-open', 'false');
    frame.src = 'about:blank';
  };

  const handleMessage = (event: MessageEvent): void => {
    if (event.origin !== frameOrigin) {
      return;
    }

    if (event.data && typeof event.data === 'object' && 'type' in event.data) {
      const message = event.data as { type?: string };
      if (message.type === 'axonvoice:close') {
        close();
      }
    }
  };

  launcher.addEventListener('click', open);
  window.addEventListener('message', handleMessage);

  return {
    open,
    close,
    destroy() {
      window.removeEventListener('message', handleMessage);
      launcher.removeEventListener('click', open);
      root.remove();
      if (activeController === this) {
        activeController = null;
      }
    },
  };
}

function normalizeConfig(config?: VoiceAgentConfig): NormalizedVoiceAgentConfig {
  if (!config?.agentId) {
    throw new Error('AxonVoice widget requires an agentId.');
  }

  const gatewayUrl = config.gatewayUrl ?? resolveDefaultGatewayUrl();
  const frameUrl = config.frameUrl ?? resolveDefaultFrameUrl();

  return {
    agentId: config.agentId,
    gatewayUrl,
    buttonLabel: config.buttonLabel ?? 'Talk to us',
    buttonColor: config.buttonColor ?? '#bf6a2f',
    position: config.position ?? 'bottom-right',
    language: config.language,
    sessionTimeoutSeconds: config.sessionTimeoutSeconds,
    frameUrl,
  };
}

function buildFrameUrl(config: NormalizedVoiceAgentConfig): string {
  const url = new URL(config.frameUrl);
  url.searchParams.set('agentId', config.agentId);
  url.searchParams.set('gatewayUrl', config.gatewayUrl);
  url.searchParams.set('buttonLabel', config.buttonLabel);

  if (config.language) {
    url.searchParams.set('language', config.language);
  }

  if (config.sessionTimeoutSeconds) {
    url.searchParams.set('sessionTimeoutSeconds', String(config.sessionTimeoutSeconds));
  }

  return url.toString();
}

function resolveLoaderScriptUrl(): string | null {
  if (typeof document === 'undefined') {
    return null;
  }

  const currentScript = document.currentScript;
  if (currentScript instanceof HTMLScriptElement && currentScript.src) {
    return currentScript.src;
  }

  const fallback = document.querySelector<HTMLScriptElement>('script[src*="axonvoice-widget"]');
  return fallback?.src ?? null;
}

function resolveDefaultGatewayUrl(): string {
  if (loaderScriptUrl) {
    return new URL(loaderScriptUrl).origin;
  }

  return window.location.origin;
}

function resolveDefaultFrameUrl(): string {
  if (loaderScriptUrl) {
    return new URL('widget-frame.html', loaderScriptUrl).toString();
  }

  return new URL('/widget-frame.html', window.location.href).toString();
}

function escapeHtml(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

window.AxonVoiceWidget = { init, destroy };

if (window.VoiceAgent?.agentId) {
  init(window.VoiceAgent);
}