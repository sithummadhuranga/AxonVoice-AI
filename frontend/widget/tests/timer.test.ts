import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { CountdownTimer } from '../src/timer.js';

describe('CountdownTimer', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('starts with full duration remaining', () => {
    const timer = new CountdownTimer(60);
    expect(timer.remaining).toBe(60);
  });

  it('decrements by 1 each second', () => {
    const timer = new CountdownTimer(10);
    timer.start();
    vi.advanceTimersByTime(3_000);
    expect(timer.remaining).toBe(7);
  });

  it('fires onTick with current remaining value', () => {
    const ticks: number[] = [];
    const timer = new CountdownTimer(5);
    timer.onTick((remaining) => ticks.push(remaining));
    timer.start();
    vi.advanceTimersByTime(3_000);
    expect(ticks).toEqual([4, 3, 2]);
  });

  it('fires onExpired when remaining reaches 0', () => {
    let expired = false;
    const timer = new CountdownTimer(2);
    timer.onExpired(() => {
      expired = true;
    });
    timer.start();
    vi.advanceTimersByTime(2_000);
    expect(expired).toBe(true);
  });

  it('stops ticking after expiry', () => {
    const ticks: number[] = [];
    const timer = new CountdownTimer(1);
    timer.onTick((remaining) => ticks.push(remaining));
    timer.start();
    vi.advanceTimersByTime(3_000);
    expect(ticks).toHaveLength(1);
    expect(timer.isRunning).toBe(false);
  });

  it('does not go below 0', () => {
    const timer = new CountdownTimer(1);
    timer.start();
    vi.advanceTimersByTime(5_000);
    expect(timer.remaining).toBe(0);
  });

  it('resets to full duration and stops', () => {
    const timer = new CountdownTimer(10);
    timer.start();
    vi.advanceTimersByTime(4_000);
    timer.reset();
    expect(timer.remaining).toBe(10);
    expect(timer.isRunning).toBe(false);
  });

  it('unregisters tick callbacks', () => {
    const ticks: number[] = [];
    const timer = new CountdownTimer(5);
    const remove = timer.onTick((remaining) => ticks.push(remaining));
    remove();
    timer.start();
    vi.advanceTimersByTime(3_000);
    expect(ticks).toHaveLength(0);
  });

  describe('CountdownTimer.format', () => {
    it('formats 0 as 00:00', () => {
      expect(CountdownTimer.format(0)).toBe('00:00');
    });

    it('formats 65 as 01:05', () => {
      expect(CountdownTimer.format(65)).toBe('01:05');
    });

    it('formats 300 as 05:00', () => {
      expect(CountdownTimer.format(300)).toBe('05:00');
    });
  });
});