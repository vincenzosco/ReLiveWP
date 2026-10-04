import { beginSignIn, type SsoConfig } from "@relivewp/auth";

import {
    SERVICE_TARGET_PORTAL,
    SSO_AUTHORITY,
    SSO_CLIENT_ID,
    SSO_REDIRECT_URI,
} from "./endpoints";

const RETURN_PATH_KEY = "relivewp.portal.return-path";
const RETURN_PATH_QUERY = "return";

export function ssoConfig(): SsoConfig {
    return {
        authority: SSO_AUTHORITY,
        clientId: SSO_CLIENT_ID,
        redirectUri: SSO_REDIRECT_URI,
        serviceTargets: [SERVICE_TARGET_PORTAL],
    };
}

export function startSignIn(returnPath?: string): Promise<void> {
    rememberReturnPath(returnPath);
    return beginSignIn(ssoConfig());
}

export function startSignUp(returnPath?: string): Promise<void> {
    rememberReturnPath(returnPath);
    return beginSignIn(ssoConfig(), "create");
}

export function signInHref(returnPath?: string): string {
    return withReturnPath("/auth/login", returnPath);
}

export function readReturnPath(query: Record<string, string>): string | undefined {
    const path = query[RETURN_PATH_QUERY];
    return path !== undefined && isReturnablePath(path) ? path : undefined;
}

export function takeReturnPath(): string | undefined {
    let path: string | null;
    try {
        path = sessionStorage.getItem(RETURN_PATH_KEY);
        sessionStorage.removeItem(RETURN_PATH_KEY);
    } catch {
        return undefined;
    }

    return path !== null && isReturnablePath(path) ? path : undefined;
}

function withReturnPath(page: string, returnPath: string | undefined): string {
    if (returnPath === undefined || !isReturnablePath(returnPath))
        return page;

    return `${page}?${RETURN_PATH_QUERY}=${encodeURIComponent(returnPath)}`;
}

function rememberReturnPath(path: string | undefined): void {
    try {
        if (path !== undefined && isReturnablePath(path))
            sessionStorage.setItem(RETURN_PATH_KEY, path);
        else
            sessionStorage.removeItem(RETURN_PATH_KEY);
    } catch {
        // private windows, they just land on the account page
    }
}

// home and the auth pages fall back to the account page
function isReturnablePath(path: string): boolean {
    if (!path.startsWith("/") || path.startsWith("//") || path.includes("\\"))
        return false;

    const pathname = path.split(/[?#]/)[0];
    return pathname !== "/" && pathname !== "/auth" && !pathname.startsWith("/auth/");
}
