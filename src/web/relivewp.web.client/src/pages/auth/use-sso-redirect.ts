import { Signal, useSignal } from "@preact/signals";
import { useLocation } from "preact-iso";
import { useEffect } from "preact/hooks";

import { useAppState } from "~/state/app-state";
import { readReturnPath } from "~/util/sso";

export function useSsoRedirect(start: (returnPath?: string) => Promise<void>): Signal<string | null> {
    const error = useSignal<string | null>(null);
    const appState = useAppState();
    const { query, route } = useLocation();

    useEffect(() => {
        const returnPath = readReturnPath(query);
        if (appState.isAuthenticated.value) {
            route(returnPath ?? "/", true);
            return;
        }

        start(returnPath).catch((e: Error) => { error.value = e.message; });
    }, []);

    return error;
}
