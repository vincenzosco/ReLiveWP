import { Route, RouteProps, useLocation } from "preact-iso"

import { useAppState } from "~/state/app-state";
import { useSignalEffect } from "@preact/signals";
import { signInHref } from "~/util/sso";

export default function AuthenticatedRoute<Props>(props: RouteProps<Props> & Partial<Props>) {
    const { isAuthenticated } = useAppState();
    const { url, route } = useLocation();

    useSignalEffect(() => {
        if (!isAuthenticated.value)
            route(signInHref(url), true);
    });

    return <Route {...props} />
}
