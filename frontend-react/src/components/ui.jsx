import { Loader2 } from "lucide-react";
export function cn(...parts) {
  return parts.filter(Boolean).join(" ");
}

/* ---------------- Logo ---------------- */
export function Logo({
  size = "md"
}) {
  const dims = size === "lg" ? "h-11 w-11" : size === "sm" ? "h-7 w-7" : "h-9 w-9";
  const text = size === "lg" ? "text-2xl" : size === "sm" ? "text-base" : "text-xl";
  return <span className="inline-flex items-center gap-2.5 select-none">
      <span className={`relative grid place-items-center ${dims}`}>
        <svg viewBox="0 0 40 40" className="absolute inset-0 spin-slow">
          <circle cx="20" cy="20" r="18" fill="none" stroke="rgba(216,255,61,0.35)" strokeWidth="1" strokeDasharray="4 6" />
        </svg>
        <svg viewBox="0 0 40 40" className="h-[58%] w-[58%]">
          <path d="M20 6 L34 32 L26 32 L20 18 L14 32 L6 32 Z" fill="#D8FF3D" />
        </svg>
      </span>
      <span className={`font-display font-bold tracking-tight ${text}`}>
        Ascend
      </span>
    </span>;
}

/* ---------------- Buttons ---------------- */

export function Button({
  variant = "primary",
  size = "md",
  loading,
  icon,
  className,
  children,
  disabled,
  ...rest
}) {
  return <button className={cn("btn", variant === "primary" && "btn-primary", variant === "ghost" && "btn-ghost", variant === "danger" && "btn-danger", size === "lg" && "btn-lg", size === "md" && "btn-md", size === "sm" && "btn-sm", className)} disabled={disabled || loading} {...rest}>
      {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : icon}
      {children}
    </button>;
}

/* ---------------- Fields ---------------- */
export function Field({
  label,
  hint,
  children,
  className
}) {
  return <label className={cn("block", className)}>
      <span className="label-caps mb-2 flex items-baseline justify-between">
        <span>{label}</span>
        {hint && <span className="normal-case tracking-normal text-[11px] text-faint">{hint}</span>}
      </span>
      {children}
    </label>;
}
export function Input(props) {
  return <input {...props} className={cn("input-base", props.className)} />;
}
export function Textarea(props) {
  return <textarea {...props} className={cn("input-base min-h-[90px] resize-y leading-relaxed", props.className)} />;
}

/* ---------------- Misc ---------------- */
export function Badge({
  children,
  tone = "neutral",
  className
}) {
  const tones = {
    neutral: "border-white/12 text-dim bg-white/4",
    acid: "border-acid/30 text-acid bg-acid/8",
    vio: "border-vio/35 text-vio bg-vio/10",
    cyan: "border-cyan2/35 text-cyan2 bg-cyan2/10",
    rose: "border-rose2/35 text-rose2 bg-rose2/10",
    mint: "border-mint/35 text-mint bg-mint/10"
  };
  return <span className={cn("inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-[11px] font-semibold tracking-wide", tones[tone], className)}>
      {children}
    </span>;
}
export function EmptyState({
  icon,
  title,
  body,
  action
}) {
  return <div className="glass card-hover relative overflow-hidden rounded-3xl px-8 py-14 text-center">
      <div className="mx-auto mb-5 grid h-14 w-14 place-items-center rounded-2xl bg-acid/10 text-acid">
        {icon}
      </div>
      <h3 className="font-display text-xl font-semibold">{title}</h3>
      <p className="mx-auto mt-2 max-w-sm text-sm leading-relaxed text-dim">{body}</p>
      {action && <div className="mt-6 flex justify-center">{action}</div>}
    </div>;
}
export function ErrorBanner({
  message
}) {
  if (!message) return null;
  return <div className="rounded-2xl border border-rose2/30 bg-rose2/10 px-4 py-3 text-sm text-rose2">
      {message}
    </div>;
}
export function Skeleton({
  className
}) {
  return <div className={cn("shimmer rounded-2xl bg-white/4", className)} />;
}

/* ---------------- API helper ---------------- */
const TOKEN_KEY = "ax_token";
export function getToken() {
  try {
    return localStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
}
export function setToken(token) {
  try {
    if (token) localStorage.setItem(TOKEN_KEY, token);
  } catch {
    /* ignore */
  }
}
export function clearToken() {
  try {
    localStorage.removeItem(TOKEN_KEY);
  } catch {
    /* ignore */
  }
}
function authHeaders() {
  const token = getToken();
  return token ? { Authorization: `Bearer ${token}` } : {};
}
export async function api(path, opts = {}) {
  const res = await fetch(path, {
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...authHeaders(),
      ...(opts.headers || {})
    },
    ...opts
  });
  let data = null;
  try {
    data = await res.json();
  } catch {
    /* non-json */
  }
  // Capture JWTs issued by the backend (login/signup/demo return { token }).
  if (data && typeof data.token === "string" && data.token.length > 10) {
    setToken(data.token);
  }
  if (!res.ok) {
    const msg = data?.error || `Request failed (${res.status})`;
    const err = new Error(msg);
    err.status = res.status;
    throw err;
  }
  return data;
}
