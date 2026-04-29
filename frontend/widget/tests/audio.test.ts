import { describe, expect, it } from 'vitest';
import { calculateRootMeanSquare, evaluateVoiceActivity } from '../src/audio.js';

describe('calculateRootMeanSquare', () => {
  it('returns zero for an empty buffer', () => {
    expect(calculateRootMeanSquare(new Float32Array())).toBe(0);
  });

  it('returns a larger value for louder audio', () => {
    const quietBuffer = new Float32Array([0.001, -0.001, 0.001, -0.001]);
    const loudBuffer = new Float32Array([0.2, -0.2, 0.2, -0.2]);

    expect(calculateRootMeanSquare(loudBuffer)).toBeGreaterThan(calculateRootMeanSquare(quietBuffer));
  });
});

describe('evaluateVoiceActivity', () => {
  it('emits audio when speech energy crosses the threshold', () => {
    const decision = evaluateVoiceActivity(
      { isSpeechActive: false, consecutiveSilentChunks: 0 },
      new Float32Array([0.05, -0.05, 0.05, -0.05]),
    );

    expect(decision.shouldEmitAudio).toBe(true);
    expect(decision.shouldEmitSpeechEnd).toBe(false);
    expect(decision.nextState.isSpeechActive).toBe(true);
  });

  it('flushes with speech end after sustained silence', () => {
    let state = { isSpeechActive: true, consecutiveSilentChunks: 0 };
    let shouldEmitSpeechEnd = false;

    for (let index = 0; index < 6; index += 1) {
      const decision = evaluateVoiceActivity(state, new Float32Array([0.0005, -0.0005, 0.0005, -0.0005]));
      state = decision.nextState;
      shouldEmitSpeechEnd = decision.shouldEmitSpeechEnd;
    }

    expect(shouldEmitSpeechEnd).toBe(true);
    expect(state.isSpeechActive).toBe(false);
  });
});