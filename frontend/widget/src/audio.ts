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
  onSpeechEnded(callback: () => void): () => void;
}

const TARGET_SAMPLE_RATE = 16_000;
// 1 024 samples at 16 kHz = 64 ms per chunk. Previously 4 096 (256 ms) created coarse,
// infrequent audio updates that added up to ~256 ms of unnecessary capture latency before
// each chunk reached Gemini. 1 024 is a safe ScriptProcessorNode size that keeps the
// onaudioprocess callback fast enough on all modern browsers.
const BUFFER_SIZE = 1_024;
const MIN_SPEECH_RMS_THRESHOLD = 0.0045;
const MAX_SPEECH_RMS_THRESHOLD = 0.012;
const INITIAL_NOISE_FLOOR_RMS = 0.0015;
const NOISE_FLOOR_SMOOTHING_FACTOR = 0.15;
const NOISE_TO_SPEECH_RATIO = 2.8;
// 4 consecutive silent 64 ms chunks = 256 ms of silence before signalling end-of-turn.
// Down from 5 (320 ms): saves one full audio buffer of latency while remaining above the
// 200 ms Gemini server-side silence threshold, so the client signal still fires first.
const SILENCE_CHUNKS_BEFORE_STREAM_END = 4;

export interface VoiceActivityState {
  isSpeechActive: boolean;
  consecutiveSilentChunks: number;
  noiseFloorRms: number;
}

export interface VoiceActivityDecision {
  nextState: VoiceActivityState;
  shouldEmitAudio: boolean;
  shouldEmitSpeechEnd: boolean;
}

const INITIAL_VOICE_ACTIVITY_STATE: VoiceActivityState = {
  isSpeechActive: false,
  consecutiveSilentChunks: 0,
  noiseFloorRms: INITIAL_NOISE_FLOOR_RMS,
};

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

function calculateSpeechThreshold(noiseFloorRms: number): number {
  return clamp(
    noiseFloorRms * NOISE_TO_SPEECH_RATIO,
    MIN_SPEECH_RMS_THRESHOLD,
    MAX_SPEECH_RMS_THRESHOLD,
  );
}

function updateNoiseFloor(previousNoiseFloorRms: number, rootMeanSquare: number): number {
  return previousNoiseFloorRms
    + ((rootMeanSquare - previousNoiseFloorRms) * NOISE_FLOOR_SMOOTHING_FACTOR);
}

function createIdleVoiceActivityState(noiseFloorRms: number): VoiceActivityState {
  return {
    isSpeechActive: false,
    consecutiveSilentChunks: 0,
    noiseFloorRms,
  };
}

export function calculateRootMeanSquare(input: Float32Array): number {
  if (input.length === 0) {
    return 0;
  }

  let sumOfSquares = 0;
  for (let index = 0; index < input.length; index += 1) {
    const sample = input[index] ?? 0;
    sumOfSquares += sample * sample;
  }

  return Math.sqrt(sumOfSquares / input.length);
}

export function evaluateVoiceActivity(
  previousState: VoiceActivityState,
  input: Float32Array,
): VoiceActivityDecision {
  const rootMeanSquare = calculateRootMeanSquare(input);
  const speechThreshold = calculateSpeechThreshold(previousState.noiseFloorRms);
  const hasSpeech = rootMeanSquare >= speechThreshold;
  const nextNoiseFloorRms = hasSpeech
    ? previousState.noiseFloorRms
    : updateNoiseFloor(previousState.noiseFloorRms, rootMeanSquare);

  if (hasSpeech) {
    return {
      nextState: {
        isSpeechActive: true,
        consecutiveSilentChunks: 0,
        noiseFloorRms: previousState.noiseFloorRms,
      },
      shouldEmitAudio: true,
      shouldEmitSpeechEnd: false,
    };
  }

  if (!previousState.isSpeechActive) {
    return {
      nextState: createIdleVoiceActivityState(nextNoiseFloorRms),
      shouldEmitAudio: false,
      shouldEmitSpeechEnd: false,
    };
  }

  const consecutiveSilentChunks = previousState.consecutiveSilentChunks + 1;
  if (consecutiveSilentChunks >= SILENCE_CHUNKS_BEFORE_STREAM_END) {
    return {
      nextState: createIdleVoiceActivityState(nextNoiseFloorRms),
      shouldEmitAudio: false,
      shouldEmitSpeechEnd: true,
    };
  }

  return {
    nextState: {
      isSpeechActive: true,
      consecutiveSilentChunks,
      noiseFloorRms: nextNoiseFloorRms,
    },
    shouldEmitAudio: false,
    shouldEmitSpeechEnd: false,
  };
}

export class BrowserAudioCapture implements AudioCaptureInterface {
  private _context: AudioContext | null = null;
  private _source: MediaStreamAudioSourceNode | null = null;
  private _processor: ScriptProcessorNode | null = null;
  private _sink: GainNode | null = null;
  private _stream: MediaStream | null = null;
  private readonly _listeners: Array<(pcm: ArrayBuffer) => void> = [];
  private readonly _speechEndedListeners: Array<() => void> = [];
  private _voiceActivityState: VoiceActivityState = INITIAL_VOICE_ACTIVITY_STATE;

  async start(): Promise<void> {
    this._stream = await navigator.mediaDevices.getUserMedia({
      audio: {
        sampleRate: TARGET_SAMPLE_RATE,
        channelCount: 1,
        autoGainControl: true,
        echoCancellation: true,
        noiseSuppression: true,
      },
    });

    this._context = new AudioContext({ sampleRate: TARGET_SAMPLE_RATE });
    this._source = this._context.createMediaStreamSource(this._stream);

    // ScriptProcessorNode is deprecated but remains the most portable cross-browser
    // option for raw PCM access without an AudioWorklet service worker requirement.
    this._processor = this._context.createScriptProcessor(BUFFER_SIZE, 1, 1);
    this._sink = this._context.createGain();
    this._sink.gain.value = 0;
    this._processor.onaudioprocess = (event) => {
      const float32 = event.inputBuffer.getChannelData(0);
      const decision = evaluateVoiceActivity(this._voiceActivityState, float32);
      this._voiceActivityState = decision.nextState;

      if (decision.shouldEmitAudio) {
        const pcm = this._float32ToPcm16(float32);
        this._listeners.forEach(cb => cb(pcm));
      }

      if (decision.shouldEmitSpeechEnd) {
        this._speechEndedListeners.forEach(cb => cb());
      }
    };

    this._source.connect(this._processor);
    this._processor.connect(this._sink);
    this._sink.connect(this._context.destination);
  }

  stop(): void {
    this._processor?.disconnect();
    this._sink?.disconnect();
    this._source?.disconnect();
    this._context?.close();
    this._stream?.getTracks().forEach(t => t.stop());
    this._processor = null;
    this._sink = null;
    this._source = null;
    this._context = null;
    this._stream = null;
    this._voiceActivityState = INITIAL_VOICE_ACTIVITY_STATE;
  }

  onChunk(callback: (pcm: ArrayBuffer) => void): () => void {
    this._listeners.push(callback);
    return () => {
      const index = this._listeners.indexOf(callback);
      if (index !== -1) this._listeners.splice(index, 1);
    };
  }

  onSpeechEnded(callback: () => void): () => void {
    this._speechEndedListeners.push(callback);
    return () => {
      const index = this._speechEndedListeners.indexOf(callback);
      if (index !== -1) this._speechEndedListeners.splice(index, 1);
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
