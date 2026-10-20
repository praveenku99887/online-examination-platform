import Link from "@/lib/rr";
import { usePathname, useRouter } from "@/lib/rr";
import { useState } from "react";
import { LogOut, Sparkles, LayoutDashboard, Plus, GraduationCap, BookOpenCheck } from "lucide-react";
import { Logo, api, clearToken, cn } from "@/components/ui";
export function Nav({
  user
}) {
  const pathname = usePathname();
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  async function logout() {
    setBusy(true);
    try {
      await api("/api/auth/logout", {
        method: "POST"
      });
    } catch {
      /* ignore */
    }
    clearToken();
    router.push("/");
    router.refresh();
  }
  const links = [{
    href: "/dashboard",
    label: "Dashboard",
    icon: <LayoutDashboard className="h-3.5 w-3.5" />
  }, ...(user.role === "teacher" ? [{
    href: "/assessments/new",
    label: "Create",
    icon: <Plus className="h-3.5 w-3.5" />
  }] : []), {
    href: "/ai",
    label: "AI Studio",
    icon: <Sparkles className="h-3.5 w-3.5" />
  }];
  return <header className="fixed inset-x-0 top-0 z-50 border-b border-white/6 bg-bg/70 backdrop-blur-xl">
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between px-5">
        <Link href="/dashboard" className="transition-opacity hover:opacity-80">
          <Logo size="sm" />
        </Link>
        <nav className="hidden items-center gap-1 rounded-full border border-white/8 bg-white/3 p-1 md:flex">
          {links.map(l => <Link key={l.href} href={l.href} className={cn("flex items-center gap-1.5 rounded-full px-3.5 py-1.5 text-[13px] font-medium transition-all duration-300", pathname.startsWith(l.href) ? "bg-acid text-[#0a0a10]" : "text-dim hover:bg-white/6 hover:text-ink")}>
              {l.icon}
              {l.label}
            </Link>)}
        </nav>
        <div className="flex items-center gap-3">
          <div className="hidden items-center gap-2.5 sm:flex">
            <span className="grid h-8 w-8 place-items-center rounded-full bg-gradient-to-br from-vio to-cyan2 font-display text-xs font-bold text-[#0a0a10]">
              {user.name.slice(0, 2).toUpperCase()}
            </span>
            <div className="leading-tight">
              <p className="max-w-[130px] truncate text-[13px] font-semibold">{user.name}</p>
              <p className="flex items-center gap-1 text-[11px] text-faint">
                {user.role === "teacher" ? <BookOpenCheck className="h-3 w-3 text-acid" /> : <GraduationCap className="h-3 w-3 text-cyan2" />}
                {user.role}
              </p>
            </div>
          </div>
          <button onClick={logout} disabled={busy} className="btn btn-ghost btn-sm" title="Sign out">
            <LogOut className="h-3.5 w-3.5" />
            <span className="hidden sm:inline">Exit</span>
          </button>
        </div>
      </div>
      {/* mobile links */}
      <div className="flex gap-1 overflow-x-auto border-t border-white/6 px-4 py-2 md:hidden no-scrollbar">
        {links.map(l => <Link key={l.href} href={l.href} className={cn("flex items-center gap-1.5 rounded-full px-3 py-1.5 text-[12px] font-medium", pathname.startsWith(l.href) ? "bg-acid text-[#0a0a10]" : "text-dim")}>
            {l.icon}
            {l.label}
          </Link>)}
      </div>
    </header>;
}
