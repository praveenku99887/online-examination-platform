// Browser-side session check (replaces the Next.js server-side helper).
// The Vite dev server proxies /api/* to the backend, so the ax_session cookie
// flows automatically; the JWT bearer token from localStorage also works.
const TOKEN_KEY = "ax_token";
export async function getSessionUser() {
  let token = null;
  try {
    token = localStorage.getItem(TOKEN_KEY);
  } catch {
    /* ignore */
  }
  try {
    const res = await fetch("/api/auth/me", {
      credentials: "include",
      cache: "no-store",
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    });
    if (!res.ok) return null;
    const data = await res.json();
    return data.user ?? null;
  } catch {
    return null;
  }
}