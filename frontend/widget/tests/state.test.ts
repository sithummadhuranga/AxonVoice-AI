import { describe, expect, it } from 'vitest';
import { WidgetStateMachine } from '../src/state.js';

describe('WidgetStateMachine', () => {
  it('starts in idle state', () => {
    const stateMachine = new WidgetStateMachine();
    expect(stateMachine.current).toBe('idle');
  });

  it('transitions idle → requesting-token', () => {
    const stateMachine = new WidgetStateMachine();
    stateMachine.transition('requesting-token');
    expect(stateMachine.current).toBe('requesting-token');
  });

  it('fires state change callbacks on valid transition', () => {
    const stateMachine = new WidgetStateMachine();
    const calls: Array<[string, string]> = [];
    stateMachine.onStateChange((previous, next) => calls.push([previous, next]));
    stateMachine.transition('requesting-token');
    expect(calls).toEqual([['idle', 'requesting-token']]);
  });

  it('unregisters callback when cleanup function is called', () => {
    const stateMachine = new WidgetStateMachine();
    const calls: string[] = [];
    const unregister = stateMachine.onStateChange((_, next) => calls.push(next));
    unregister();
    stateMachine.transition('requesting-token');
    expect(calls).toHaveLength(0);
  });

  it('throws on invalid state transition', () => {
    const stateMachine = new WidgetStateMachine();
    expect(() => stateMachine.transition('connected')).toThrow(
      "Invalid state transition: 'idle' → 'connected'",
    );
  });

  it('full happy-path session reaches idle after ending', () => {
    const stateMachine = new WidgetStateMachine();
    stateMachine.transition('requesting-token');
    stateMachine.transition('connecting');
    stateMachine.transition('connected');
    stateMachine.transition('speaking');
    stateMachine.transition('receiving');
    stateMachine.transition('ending');
    stateMachine.transition('idle');
    expect(stateMachine.current).toBe('idle');
  });

  it('transitions to error from any active state', () => {
    for (const state of ['requesting-token', 'connecting', 'connected', 'speaking', 'receiving'] as const) {
      const stateMachine = new WidgetStateMachine();
      const paths: Record<string, string[]> = {
        'requesting-token': ['requesting-token'],
        connecting: ['requesting-token', 'connecting'],
        connected: ['requesting-token', 'connecting', 'connected'],
        speaking: ['requesting-token', 'connecting', 'connected', 'speaking'],
        receiving: ['requesting-token', 'connecting', 'connected', 'receiving'],
      };

      paths[state].forEach((nextState) => stateMachine.transition(nextState as never));
      stateMachine.transition('error');
      expect(stateMachine.current).toBe('error');
    }
  });

  it('can recover from error back to idle', () => {
    const stateMachine = new WidgetStateMachine();
    stateMachine.transition('requesting-token');
    stateMachine.transition('error');
    stateMachine.transition('idle');
    expect(stateMachine.current).toBe('idle');
  });
});