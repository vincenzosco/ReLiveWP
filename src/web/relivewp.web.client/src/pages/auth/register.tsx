import { useTitle } from "@relivewp/ui";

import { startSignUp } from "~/util/sso";
import { useSsoRedirect } from "./use-sso-redirect";

export default function Register() {
    useTitle("sign up");

    const error = useSsoRedirect(startSignUp);

    return (
        <div>
            <h1>sign up</h1>
            {error.value
                ? <p class="error">{error.value}</p>
                : <p>Taking you to sign up...</p>}
        </div>
    );
}
