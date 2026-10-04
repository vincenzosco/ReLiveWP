import { useTitle } from "@relivewp/ui";

import { startSignIn } from "~/util/sso";
import { useSsoRedirect } from "./use-sso-redirect";

export default function Login() {
    useTitle("login");

    const error = useSsoRedirect(startSignIn);

    return (
        <div>
            <h1>sign in</h1>
            {error.value
                ? <p class="error">{error.value}</p>
                : <p>Taking you to sign in...</p>}
        </div>
    );
}
