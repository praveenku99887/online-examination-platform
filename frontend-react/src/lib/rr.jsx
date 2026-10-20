// Minimal Next.js-compatible routing layer built on react-router.
// Components imported from the original app call Link/useRouter/usePathname/
// useParams exactly like before — only this shim changed.
import { useRef } from "react";
import { Link as RRLink, useNavigate, useLocation, useParams as useRRParams } from "react-router-dom";

// Session refresh pub/sub: router.refresh() in Next re-ran server components.
// Here it tells guards/shells to re-fetch /api/auth/me.
const listeners = new Set();
export function onSessionRefresh(fn) {
  listeners.add(fn);
  return () => listeners.delete(fn);
}
function fireSessionRefresh() {
  listeners.forEach(fn => fn());
}

function Link({ href, ...rest }) {
  return <RRLink to={href} {...rest} />;
}
export { Link };
export default Link;

// Next.js's useRouter() returns a stable identity, and pages (e.g. the exam
// client) legitimately list it in effect deps. Return one stable object whose
// methods always call the latest navigate, so effects only run when they should.
export function useRouter() {
  const navigate = useNavigate();
  const navRef = useRef(navigate);
  navRef.current = navigate;
  const routerRef = useRef(null);
  if (routerRef.current === null) {
    routerRef.current = {
      push: path => navRef.current(path),
      replace: path => navRef.current(path, { replace: true }),
      back: () => navRef.current(-1),
      refresh: () => fireSessionRefresh(),
      prefetch: () => {},
    };
  }
  return routerRef.current;
}

export function usePathname() {
  const { pathname } = useLocation();
  return pathname;
}

export const useParams = useRRParams;