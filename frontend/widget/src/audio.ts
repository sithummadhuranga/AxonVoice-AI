/**
 * Encapsulates all browser audio capture logic.
 * Uses getUserMedia → AudioContext → ScriptProcessorNode to produce
 * 16-bit PCM chunks at 16 kHz for transmission to the session relay.
 *
 * The interface is deliberately narrow so tests can replace this with
 * a mock that emits controlled audio frames without needing real hardware.
 */
export interface AudioCaptureInterface {
  start(): Promise<void>;
  stop(): void;
  onChunk(callback: (pcm: ArrayBuffer) => void): () => void;
}

const TARGET_SAMPLE_RATE = 16_000;
const BUFFER_SIZE = 4_096;

export class BrowserAudioCapture implements AudioCaptureInterface {
  private _context: AudioContext | null = null;
  private _source: MediaStreamAudioSourceNode | null = null;
  private _processor: ScriptProcessorNode | null = null;
  private _stream: MediaStream | null = null;
  private readonly _listeners: Array<(pcm: ArrayBuffer) => void> = [];

  async start(): Promise<void> {
    this._stream = await navigator.mediaDevices.getUserMedia({
      audio: {
        sampleRate: TARGET_SAMPLE_RATE,
        channelCount: 1,
        echoCancellation: true,
        noiseSuppression: true,
      },
    });

    this._context = new AudioContext({ sampleRate: TARGET_SAMPLE_RATE });
    this._source = this._context.createMediaStreamSource(this._stream);

    // ScriptProcessorNode is deprecated but remains the most portable cross-browser
    // option for raw PCM access without an AudioWorklet service worker requirement.
    this._processor = this._context.createScriptProcessor(BUFFER_SIZE, 1, 1);
    this._processor.onaudioprocess = (event) => {
      const float32 = event.inputBuffer.getChannelData(0);
      const pcm = this._float32ToPcm16(float32);
      this._listeners.forEach(cb => cb(pcm));
    };

    this._source.connect(this._processor);
    this._processor.connect(this._context.destination);
  }

  stop(): void {
    this._processor?.disconnect();
    this._source?.disconnect();
    this._context?.close();
    this._stream?.getTracks().forEach(t => t.stop());
    this._processor = null;
    this._source = null;
    this._context = null;
    this._stream = null;
  }

  onChunk(callback: (pcm: ArrayBuffer) => void): () => void {
    this._listeners.push(callback);
    return () => {
      const index = this._listeners.indexOf(callback);
      if (index !== -1) this._listeners.splice(index, 1);
    };
  }

  private _float32ToPcm16(input: Float32Array): ArrayBuffer {
    const buffer = new ArrayBuffer(input.length * 2);
    const view = new DataView(buffer);
    for (let i = 0; i < input.length; i++) {
      const clamped = Math.max(-1, Math.min(1, input[i]));
      view.setInt16(i * 2, clamped * 0x7fff, /* littleEndian */ true);
    }
    return buffer;
  }
}
