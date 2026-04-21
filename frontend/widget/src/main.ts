import './style.css';
import { init } from './loader.js';

const app = document.querySelector<HTMLDivElement>('#app');

if (!app) {
  throw new Error('Demo root element was not found.');
}

app.innerHTML = `
  <main class="demo-shell">
    <section class="demo-copy">
      <p class="demo-kicker">AxonVoice Widget</p>
      <h1>Cross-origin voice widget loader</h1>
      <p class="demo-body">
        This page acts as the host website. The floating launcher opens an isolated iframe that owns
        microphone access, session tokens, and the relay WebSocket lifecycle.
      </p>
      <div class="demo-grid">
        <article>
          <h2>Loader</h2>
          <p>Reads the host config, injects the launcher, and mounts the remote frame.</p>
        </article>
        <article>
          <h2>Frame</h2>
          <p>Captures audio, opens the session, plays model audio, and manages session state.</p>
        </article>
      </div>
    </section>
  </main>
`;

init({
  agentId: '00000000-0000-0000-0000-000000000000',
  gatewayUrl: window.location.origin,
  frameUrl: new URL('/widget-frame.html', window.location.href).toString(),
  buttonLabel: 'Talk to AxonVoice',
  buttonColor: '#bf6a2f',
});
