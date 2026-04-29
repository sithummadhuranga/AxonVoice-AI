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
      { isSpeechActive: false, consecutiveSilentChunks: 0, noiseFloorRms: 0.0015 },
      new Float32Array([0.05, -0.05, 0.05, -0.05]),
    );

    expect(decision.shouldEmitAudio).toBe(true);
    expect(decision.shouldEmitSpeechEnd).toBe(false);
    expect(decision.nextState.isSpeechActive).toBe(true);
  });

  it('accepts quieter speech above the adaptive floor', () => {
    const decision = evaluateVoiceActivity(
      { isSpeechActive: false, consecutiveSilentChunks: 0, noiseFloorRms: 0.0015 },
      new Float32Array([0.006, -0.006, 0.006, -0.006]),
    );

    expect(decision.shouldEmitAudio).toBe(true);
    expect(decision.nextState.isSpeechActive).toBe(true);
  });

  it('tracks quiet background noise without falsely triggering speech', () => {
    let state = { isSpeechActive: false, consecutiveSilentChunks: 0, noiseFloorRms: 0.0015 };

    for (let index = 0; index < 8; index += 1) {
      const decision = evaluateVoiceActivity(state, new Float32Array([0.002, -0.002, 0.002, -0.002]));
      state = decision.nextState;
      expect(decision.shouldEmitAudio).toBe(false);
      expect(decision.shouldEmitSpeechEnd).toBe(false);
    }

    expect(state.isSpeechActive).toBe(false);
    expect(state.noiseFloorRms).toBeGreaterThan(0.0015);
  });

  it('flushes with speech end after sustained silence', () => {
    let state = { isSpeechActive: true, consecutiveSilentChunks: 0, noiseFloorRms: 0.0015 };
    let shouldEmitSpeechEnd = false;

    for (let index = 0; index < 4; index += 1) {
      const decision = evaluateVoiceActivity(state, new Float32Array([0.0005, -0.0005, 0.0005, -0.0005]));
      state = decision.nextState;
      shouldEmitSpeechEnd = decision.shouldEmitSpeechEnd;
    }

    expect(shouldEmitSpeechEnd).toBe(true);
    expect(state.isSpeechActive).toBe(false);
  });
});