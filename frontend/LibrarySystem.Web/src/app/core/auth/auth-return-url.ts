const fallbackRoute = '/dashboard';

export function getSafeReturnUrl(returnUrl: string | null | undefined): string {
  if (!returnUrl) {
    return fallbackRoute;
  }

  const trimmedReturnUrl = returnUrl.trim();

  if (
    !trimmedReturnUrl.startsWith('/') ||
    trimmedReturnUrl.startsWith('//') ||
    trimmedReturnUrl.includes('://') ||
    trimmedReturnUrl.includes('\\')
  ) {
    return fallbackRoute;
  }

  return trimmedReturnUrl;
}
