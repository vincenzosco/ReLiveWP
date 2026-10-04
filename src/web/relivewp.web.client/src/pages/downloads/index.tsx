import "./downloads.scss";

import { useAccentColor } from "@relivewp/ui";

import DeviceCatalog from "./device-catalog";
import DeviceDownloads from "./device-downloads";

export default function Downloads({ deviceId }: { deviceId?: string }) {
    useAccentColor("pink")

    return (
        <div class="downloads">
            {deviceId
                ? <DeviceDownloads key={deviceId} deviceId={deviceId} />
                : <DeviceCatalog />}
        </div>
    );
}
