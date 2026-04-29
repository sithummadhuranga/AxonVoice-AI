'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

type SessionPhase =
  | 'loading-script'
  | 'ready'
  | 'connecting'
  | 'live'
  | 'ended'
  | 'error';

type MicState = 'unknown' | 'granted' | 'denied' | 'checking';

interface AgentTestConsoleProps {
  agentId: string;
  agentName: string;
  isActive: boolean;
  primaryLanguage: string;
  geminiModel: string;
  toolsEnabled: string[];
}

// Minimal type declaration for the widget API loaded via external script.
interface WidgetController {
  open(): void;
  close(): void;
  destroy(): void;
}

declare global {
  interface Window {
    AxonVoiceWidget?: {
      init(config: {
        agentId: string;
        buttonLabel?: string;
        buttonColor?: string;
        position?: 'bottom-right' | 'bottom-left';
      }): WidgetController;
    };
  }
}

export function AgentTestConsole({
  agentId,
  agentName,
  isActive,
  primaryLanguage,
  geminiModel,
  toolsEnabled,
}: AgentTestConsoleProps) {
  const [phase, setPhase] = useState<SessionPhase>('loading-script');
  const [mic, setMic] = useState<MicState>('unknown');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const controllerRef = useRef<WidgetController | null>(null);

  // Load the widget script once from the same gateway that serves this page.
  useEffect(() => {
    const scriptSrc = `${window.location.origin}/widget/axonvoice-widget.js?testConsoleVersion=${Date.now()}`;

    controllerRef.current?.destroy();
    controllerRef.current = null;
    window.AxonVoiceWidget = undefined;

    document
      .querySelectorAll('script[data-axonvoice-test-console-widget="true"]')
      .forEach((existingScript) => existingScript.remove());

    const script = document.createElement('script');
    script.dataset.axonvoiceTestConsoleWidget = 'true';
    script.src = scriptSrc;
    script.onload = () => setPhase('ready');
    script.onerror = () => {
      setPhase('error');
      setErrorMessage(
        'Widget script could not be loaded from the gateway. Ensure the gateway and widget containers are running.'
      );
    };
    document.head.appendChild(script);

    return () => {
      script.remove();
    };
  }, []);

  // Probe mic permission without triggering a prompt.
  useEffect(() => {
    navigator.permissions
      ?.query({ name: 'microphone' as PermissionName })
      .then((status) => {
        const map = (s: string): MicState =>
          s === 'granted' ? 'granted' : s === 'denied' ? 'denied' : 'unknown';
        setMic(map(status.state));
        status.onchange = () => setMic(map(status.state));
      })
      .catch(() => setMic('unknown'));
  }, []);

  // Listen for the widget's close message to track session end.
  useEffect(() => {
    const handle = (event: MessageEvent) => {
      if (
        typeof event.data === 'object' &&
        event.data !== null &&
        event.data.type === 'axonvoice:close'
      ) {
        controllerRef.current = null;
        setPhase('ended');
      }
    };
    window.addEventListener('message', handle);
    return () => window.removeEventListener('message', handle);
  }, []);

  const startSession = useCallback(async () => {
    setErrorMessage(null);

    // Explicitly request mic before handing off to the widget so the browser
    // permission prompt appears in this tab context, not inside the iframe.
    if (mic !== 'granted') {
      setMic('checking');
      try {
        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        stream.getTracks().forEach((t) => t.stop());
        setMic('granted');
      } catch {
        setMic('denied');
        setErrorMessage(
          'Microphone access was denied. Allow microphone access in your browser settings and try again.'
        );
        return;
      }
    }

    if (!window.AxonVoiceWidget) {
      setPhase('error');
      setErrorMessage('Widget script is not loaded. Refresh the page and try again.');
      return;
    }

    setPhase('connecting');

    try {
      controllerRef.current?.destroy();
      const controller = window.AxonVoiceWidget.init({
        agentId,
        buttonLabel: `${agentName} — Test`,
        buttonColor: '#bf6a2f',
        position: 'bottom-right',
      });
      controllerRef.current = controller;
      controller.open();
      setPhase('live');
    } catch (err) {
      setPhase('error');
      setErrorMessage(err instanceof Error ? err.message : 'Failed to initialise session.');
    }
  }, [agentId, agentName, mic]);

  const endSession = useCallback(() => {
    controllerRef.current?.destroy();
    controllerRef.current = null;
    setPhase('ended');
  }, []);

  const resetConsole = useCallback(() => {
    setPhase('ready');
    setErrorMessage(null);
  }, []);

  const gatewayOrigin =
    typeof window !== 'undefined' ? window.location.origin : 'http://localhost:8080';

  const embedSnippet = `<!-- AxonVoice widget embed -->
<script>
  window.VoiceAgent = { agentId: '${agentId}' };
<\/script>
<script src="${gatewayOrigin}/widget/axonvoice-widget.js"><\/script>`;

  const copyEmbed = async () => {
    try {
      await navigator.clipboard.writeText(embedSnippet);
      setCopied(true);
      setTimeout(() => setCopied(false), 2500);
    } catch {
      // Clipboard not available — ignore.
    }
  };

  const preflightReady =
    phase !== 'loading-script' &&
    phase !== 'error' &&
    isActive &&
    mic !== 'denied' &&
    mic !== 'checking';

  return (
    <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
      {/* ── Left: Live test console ─────────────────────────────── */}
      <div className="grid gap-4">
        {/* Pre-flight checklist */}
        <section className="surface-card p-5">
          <p className="eyebrow text-muted">Pre-flight checks</p>
          <div className="mt-4 grid gap-3">
            <CheckItem
              label="Agent is active"
              pass={isActive}
              failNote="Set this agent to Active in the agent editor before testing."
            />
            <CheckItem
              label="Widget script loaded"
              pass={phase !== 'loading-script' && phase !== 'error'}
              failNote="Gateway or widget container may be stopped. Run docker compose up."
            />
            <CheckItem
              label="Microphone permission"
              pass={mic === 'granted'}
              pending={mic === 'unknown' || mic === 'checking'}
              failNote={
                mic === 'denied'
                  ? 'Blocked in browser. Open site settings and allow microphone.'
                  : 'Will be requested when you start the session.'
              }
            />
          </div>
        </section>

        {/* Session stage */}
        <section className="surface-card-strong relative overflow-hidden p-6 lg:p-8">
          <div
            aria-hidden="true"
            className="pointer-events-none absolute inset-0"
            style={{
              background:
                phase === 'live'
                  ? 'radial-gradient(circle at 50% 40%, rgba(20,115,230,0.12) 0%, transparent 60%)'
                  : 'radial-gradient(circle at 86% 14%, rgba(20,115,230,0.06) 0%, transparent 40%)',
              transition: 'background 1s ease',
            }}
          />

          <div className="relative flex flex-col items-center gap-6 py-4 text-center">
            <SessionStatusOrb phase={phase} />

            <div>
              <p className="text-lg font-semibold tracking-[-0.03em] text-foreground">
                {phaseLabel(phase)}
              </p>
              {phase === 'live' && (
                <p className="mt-1 text-sm text-muted">
                  Speaking with <span className="font-medium text-foreground">{agentName}</span>.
                  Close the overlay when done.
                </p>
              )}
              {phase === 'ended' && (
                <p className="mt-1 text-sm text-muted">Session ended. Start a new one whenever you&apos;re ready.</p>
              )}
              {phase === 'loading-script' && (
                <p className="mt-1 text-sm text-muted">Loading widget from gateway…</p>
              )}
              {phase === 'connecting' && (
                <p className="mt-1 text-sm text-muted">Requesting session token and opening relay…</p>
              )}
              {(phase === 'ready' || phase === 'error') && !errorMessage && (
                <p className="mt-1 text-sm text-muted">
                  {preflightReady
                    ? 'All checks passed. Start a live voice session below.'
                    : 'Resolve the failing checks above before testing.'}
                </p>
              )}
            </div>

            {errorMessage && (
              <div className="w-full rounded-[1.15rem] border border-red-200 bg-red-50 px-4 py-3 text-left">
                <p className="text-sm font-medium text-red-700">Session error</p>
                <p className="mt-1 text-sm text-red-600">{errorMessage}</p>
              </div>
            )}

            {/* Action button */}
            {(phase === 'ready' || phase === 'error') && (
              <button
                onClick={phase === 'error' ? resetConsole : startSession}
                disabled={phase !== 'error' && !preflightReady}
                className="primary-button disabled:opacity-40 disabled:cursor-not-allowed"
              >
                {phase === 'error' ? 'Try again' : 'Start live session'}
              </button>
            )}

            {phase === 'live' && (
              <button onClick={endSession} className="secondary-button">
                End session
              </button>
            )}

            {phase === 'ended' && (
              <button onClick={resetConsole} className="secondary-button">
                Start another session
              </button>
            )}
          </div>
        </section>

        {/* Quota notice */}
        <p className="px-1 text-xs leading-5 text-muted">
          Test sessions use your tenant&apos;s Gemini API key and consume real quota. Each session
          is recorded in the Sessions log with the tag <span className="font-mono">dashboard-test</span>.
        </p>
      </div>

      {/* ── Right: Contract details + embed code ────────────────── */}
      <div className="grid gap-4 self-start xl:sticky xl:top-4">
        <section className="surface-card p-5">
          <p className="eyebrow text-muted">Agent contract</p>
          <div className="mt-4 grid gap-3">
            <InfoRow label="Agent ID" value={agentId} mono />
            <InfoRow label="Primary language" value={primaryLanguage} />
            <InfoRow label="Gemini model" value={geminiModel} />
            <InfoRow
              label="Enabled tools"
              value={toolsEnabled.length > 0 ? toolsEnabled.join(', ') : 'None'}
            />
          </div>
        </section>

        <section className="surface-card p-5">
          <div className="flex items-center justify-between">
            <p className="eyebrow text-muted">Embed code</p>
            <button
              onClick={copyEmbed}
              className="rounded-full border border-line bg-white/84 px-2.5 py-1 text-[0.68rem] font-medium uppercase tracking-[0.1em] text-muted transition-colors hover:border-accent hover:text-accent"
            >
              {copied ? 'Copied' : 'Copy'}
            </button>
          </div>
          <pre className="mt-3 overflow-x-auto rounded-xl bg-black/[0.04] p-3 text-[0.7rem] leading-5 text-foreground/80">
            {embedSnippet}
          </pre>
          <p className="mt-3 text-xs leading-5 text-muted">
            Paste this into any HTML page to embed the voice widget. No framework required.
          </p>
        </section>
      </div>
    </div>
  );
}

function phaseLabel(phase: SessionPhase): string {
  switch (phase) {
    case 'loading-script': return 'Loading widget…';
    case 'ready':          return 'Ready to test';
    case 'connecting':     return 'Connecting…';
    case 'live':           return 'Session live';
    case 'ended':          return 'Session ended';
    case 'error':          return 'Session failed';
  }
}

function SessionStatusOrb({ phase }: { phase: SessionPhase }) {
  const color =
    phase === 'live'       ? '#22c55e' :
    phase === 'connecting' ? '#3b82f6' :
    phase === 'ended'      ? '#94a3b8' :
    phase === 'error'      ? '#ef4444' :
    '#e2e8f0';

  return (
    <div
      className="relative flex h-16 w-16 items-center justify-center rounded-full"
      style={{ background: `${color}20` }}
    >
      {phase === 'live' && (
        <span
          className="absolute inset-0 rounded-full animate-ping"
          style={{ background: `${color}30` }}
        />
      )}
      <span className="h-6 w-6 rounded-full" style={{ background: color }} />
    </div>
  );
}

function CheckItem({
  label,
  pass,
  pending = false,
  failNote,
}: {
  label: string;
  pass: boolean;
  pending?: boolean;
  failNote: string;
}) {
  return (
    <div className="flex items-start gap-3 rounded-[1.15rem] border border-line bg-white/78 px-4 py-3">
      <span
        className="mt-0.5 flex h-4 w-4 shrink-0 items-center justify-center rounded-full text-[0.6rem]"
        style={{
          background: pass ? '#22c55e20' : pending ? '#f59e0b20' : '#ef444420',
          color:      pass ? '#16a34a'   : pending ? '#b45309'   : '#dc2626',
        }}
      >
        {pass ? '✓' : pending ? '?' : '✗'}
      </span>
      <div className="min-w-0">
        <p className="text-sm font-medium text-foreground">{label}</p>
        {!pass && <p className="mt-0.5 text-xs leading-4 text-muted">{failNote}</p>}
      </div>
    </div>
  );
}

function InfoRow({ label, mono = false, value }: { label: string; mono?: boolean; value: string }) {
  return (
    <div className="rounded-[1.15rem] border border-line bg-white/78 px-4 py-3">
      <p className="text-xs font-medium uppercase tracking-[0.12em] text-muted">{label}</p>
      <p className={`mt-2 text-sm text-foreground ${mono ? 'break-all font-mono' : 'font-medium'}`}>
        {value}
      </p>
    </div>
  );
}
