import { Link } from "@relivewp/ui";
import { Show } from "@preact/signals/utils";
import { useLocation } from "preact-iso";
import { useAppState } from "~/state/app-state";
import { signInHref } from "~/util/sso";

const NavLoginLink = () => {
    const appState = useAppState();
    const { url } = useLocation();

    return (
        <Show when={appState.isAuthenticated} fallback={<Link activeClass="active" href={signInHref(url)}>sign in</Link>}>
            <Link activeClass="active text-accent" href="/my/account">
                <Show when={appState.user} fallback={<span>hi there</span>}>
                    <span>hi, {appState.user.value?.username}</span>
                </Show>
            </Link>
        </Show>
    )
};

export default NavLoginLink;
