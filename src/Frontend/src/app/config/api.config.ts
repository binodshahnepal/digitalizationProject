/**
 * Dynamic API Configuration for Hospital Records Digitization
 * Automatically adjusts baseUrl between local dev server (port 4200 -> port 5000)
 * and IIS production deployment (www.digital.com -> www.digital.com/api/v1).
 */
export function getApiBaseUrl(): string {
  if (typeof window !== 'undefined' && window.location) {
    const host = window.location.hostname;
    const port = window.location.port;
    if (host === 'www.digital.com' || host === 'digital.com' || port === '' || port === '80' || port === '443') {
      return `${window.location.origin}/api/api/v1`;
    }
  }
  return 'http://localhost:5000/api/v1';
}

export function getAiWorkerBaseUrl(): string {
  if (typeof window !== 'undefined' && window.location) {
    const host = window.location.hostname;
    const port = window.location.port;
    if (host === 'www.digital.com' || host === 'digital.com' || port === '' || port === '80' || port === '443') {
      return `${window.location.origin}/ai`;
    }
  }
  return 'http://localhost:5200';
}
