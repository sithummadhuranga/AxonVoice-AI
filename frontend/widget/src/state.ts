/**
 * All states the widget can occupy during its lifecycle.
 * Transitions are enforced by WidgetStateMachine — no state is mutated directly.
 */
export type WidgetState =
  | 'idle'
  | 'requesting-token'
  | 'connecting'
  | 'connected'
  | 'speaking'      // user is transmitting audio
  | 'receiving'     // agent is responding with audio
  | 'ending'        // graceful close in progress
  | 'error';

type StateTransitions = Record<WidgetState, ReadonlyArray<WidgetState>>;

const ALLOWED_TRANSITIONS: StateTransitions = {
  'idle':             ['requesting-token'],
  'requesting-token': ['connecting', 'error'],
  'connecting':       ['connected', 'error'],
  'connected':        ['speaking', 'receiving', 'ending', 'error'],
  'speaking':         ['connected', 'receiving', 'ending', 'error'],
  'receiving':        ['connected', 'speaking', 'ending', 'error'],
  'ending':           ['idle', 'error'],
  'error':            ['idle'],
};

export type StateChangeCallback = (previous: WidgetState, next: WidgetState) => void;

export class WidgetStateMachine {
  private _current: WidgetState = 'idle';
  private readonly _listeners: StateChangeCallback[] = [];

  get current(): WidgetState {
    return this._current;
  }

  transition(next: WidgetState): void {
    const allowed = ALLOWED_TRANSITIONS[this._current];
    if (!allowed.includes(next)) {
      throw new Error(
        `Invalid state transition: '${this._current}' → '${next}'`
      );
    }
    const previous = this._current;
    this._current = next;
    this._listeners.forEach(cb => cb(previous, next));
  }

  onStateChange(callback: StateChangeCallback): () => void {
    this._listeners.push(callback);
    return () => {
      const index = this._listeners.indexOf(callback);
      if (index !== -1) this._listeners.splice(index, 1);
    };
  }
}
