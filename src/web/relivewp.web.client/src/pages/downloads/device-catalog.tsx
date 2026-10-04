import { Signal, useSignal } from "@preact/signals";
import { Link, useTitle } from "@relivewp/ui";

import { useAppState } from "~/state/app-state";
import useDeviceImage from "~/util/device-image";
import { Devices } from "~/util/device-types";
import { DownloadDevice } from "~/util/download-types";
import { ENDPOINT_DOWNLOAD_DEVICES, ENDPOINT_GET_DEVICES } from "~/util/endpoints";
import { useFetchSignal, usePublicFetchSignal } from "~/util/use-fetch";
import { formatSupport, matchesRegisteredModel } from "./format";

function RegisteredModels({ models }: { models: Signal<string[]> }) {
    useFetchSignal<Devices>(ENDPOINT_GET_DEVICES, devices => {
        models.value = devices.map(d => d.model).filter(m => !!m);
    });

    return null;
}

function DeviceTile({ device, isYours }: { device: DownloadDevice, isYours: boolean }) {
    const [deviceImage] = useDeviceImage(device.manufacturer, device.image_model);

    return (
        <Link class={`device-tile support-${device.support}`} href={`/downloads/${encodeURIComponent(device.id)}`}>
            <div class="device-image"><img src={deviceImage} alt="" /></div>
            <span class="device-name">{device.manufacturer} {device.name}</span>
            <p>
                <span class="device-support">{formatSupport(device.support)}</span>
                {isYours && <> - <span class="device-yours text-accent">your device</span></>}
            </p>

        </Link>
    );
}

export default function DeviceCatalog() {
    useTitle("downloads");

    const appState = useAppState();
    const registeredModels = useSignal<string[]>([]);
    const { data: devices, error } = usePublicFetchSignal<DownloadDevice[]>(ENDPOINT_DOWNLOAD_DEVICES);

    return (
        <>
            <h1>downloads</h1>

            {appState.isAuthenticated.value && <RegisteredModels models={registeredModels} />}

            {error.value && <p class="error">{error.value}</p>}
            {!devices.value && !error.value && <p>fetching devices...</p>}
            {devices.value && (
                <div class="device-catalog">
                    {devices.value.map(device => (
                        <DeviceTile
                            key={device.id}
                            device={device}
                            isYours={matchesRegisteredModel(device, registeredModels.value)} />
                    ))}
                </div>
            )}
        </>
    );
}
