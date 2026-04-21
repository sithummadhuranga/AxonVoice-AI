/**
 * Counts down from a given duration in seconds.
 * Fires onTick every second and onExpired when the timer reaches zero.
 * Intended for displaying the remaining session time in the widget UI.
 */
export class CountdownTimer {
  private readonly _durationSeconds: number;
  private _remaining: number;
  private _intervalId: ReturnType<typeof setInterval> | null = null;
  private _tickCallbacks: Array<(remaining: number) => void> = [];
  private _expiredCallbacks: Array<() => void> = [];

  constructor(durationSeconds: number) {
    this._durationSeconds = durationSeconds;
    this._remaining = durationSeconds;
  }

  get remaining(): number {
    return this._remaining;
  }

  get isRunning(): boolean {
    return this._intervalId !== null;
  }

  start(): void {
    if (this._intervalId !== null) return;
    this._intervalId = setInterval(() => {
      this._remaining = Math.max(0, this._remaining - 1);
      this._tickCallbacks.forEach(cb => cb(this._remaining));
      if (this._remaining === 0) {
        this.stop();
        this._expiredCallbacks.forEach(cb => cb());
      }
    }, 1_000);
  }

  stop(): void {
    if (this._intervalId !== null) {
      clearInterval(this._intervalId);
      this._intervalId = null;
    }
  }

  reset(): void {
    this.stop();
    this._remaining = this._durationSeconds;
  }

  onTick(callback: (remaining: number) => void): () => void {
    this._tickCallbacks.push(callback);
    return () => {
      const i = this._tickCallbacks.indexOf(callback);
      if (i !== -1) this._tickCallbacks.splice(i, 1);
    };
  }

  onExpired(callback: () => void): () => void {
    this._expiredCallbacks.push(callback);
    return () => {
      const i = this._expiredCallbacks.indexOf(callback);
      if (i !== -1) this._expiredCallbacks.splice(i, 1);
    };
  }

  /** Format as MM:SS for display. */
  static format(seconds: number): string {
    const m = Math.floor(seconds / 60).toString().padStart(2, '0');
    const s = (seconds % 60).toString().padStart(2, '0');
    return `${m}:${s}`;
  }
}
