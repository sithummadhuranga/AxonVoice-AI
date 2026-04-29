'use client';

import { useFormStatus } from 'react-dom';

type ActionSubmitButtonProps = {
  className: string;
  idleLabel: string;
  pendingLabel: string;
};

export function ActionSubmitButton({ className, idleLabel, pendingLabel }: ActionSubmitButtonProps) {
  const { pending } = useFormStatus();

  return (
    <button className={className} disabled={pending} type="submit">
      {pending ? pendingLabel : idleLabel}
    </button>
  );
}