const consoleDateTimeFormatter = new Intl.DateTimeFormat('en', {
  dateStyle: 'medium',
  timeStyle: 'short',
});

const consoleDateFormatter = new Intl.DateTimeFormat('en', {
  dateStyle: 'medium',
});

export function formatConsoleDateTime(value: Date | string | null | undefined): string {
  const date = normalizeDate(value);
  if (!date) {
    return 'Not available';
  }

  return consoleDateTimeFormatter.format(date);
}

export function formatConsoleDate(value: Date | string | null | undefined): string {
  const date = normalizeDate(value);
  if (!date) {
    return 'Not available';
  }

  return consoleDateFormatter.format(date);
}

export function formatDurationSeconds(value: number | null | undefined): string {
  if (typeof value !== 'number' || value < 0) {
    return 'In progress';
  }

  if (value < 60) {
    return `${value}s`;
  }

  const hours = Math.floor(value / 3600);
  const minutes = Math.floor((value % 3600) / 60);
  const seconds = value % 60;

  if (hours > 0) {
    return `${hours}h ${minutes}m`;
  }

  if (seconds === 0) {
    return `${minutes}m`;
  }

  return `${minutes}m ${seconds}s`;
}

export function maskContactValue(value: string | null | undefined): string {
  if (!value) {
    return 'Unknown';
  }

  const visiblePrefixLength = value.startsWith('+') ? 2 : 1;
  const visibleSuffixLength = 4;
  if (value.length <= visiblePrefixLength + visibleSuffixLength) {
    return value;
  }

  const hiddenLength = value.length - visiblePrefixLength - visibleSuffixLength;
  return `${value.slice(0, visiblePrefixLength)}${'*'.repeat(hiddenLength)}${value.slice(-visibleSuffixLength)}`;
}

export function formatTimeRemaining(value: Date | string | null | undefined): string {
  const date = normalizeDate(value);
  if (!date) {
    return 'Unknown';
  }

  const remainingMilliseconds = date.getTime() - Date.now();
  if (remainingMilliseconds <= 0) {
    return 'Expired';
  }

  const totalMinutes = Math.ceil(remainingMilliseconds / 60000);
  if (totalMinutes < 60) {
    return `${totalMinutes}m left`;
  }

  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;

  if (minutes === 0) {
    return `${hours}h left`;
  }

  return `${hours}h ${minutes}m left`;
}

function normalizeDate(value: Date | string | null | undefined): Date | null {
  if (!value) {
    return null;
  }

  const date = value instanceof Date ? value : new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}