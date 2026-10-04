import { ComponentChildren } from "preact";
import { useSignal } from "@preact/signals";
import { useLocation } from "preact-iso";
import { Link, useTitle } from "@relivewp/ui";

import { ActionLink } from "~/pages/my/account/components/AccountEntry/ActionLink";
import { useAppState } from "~/state/app-state";
import useDeviceImage from "~/util/device-image";
import useVersion from "~/util/version";
import { DownloadDevice, DownloadDeviceDetail, DownloadRom, RomLink } from "~/util/download-types";
import { ENDPOINT_DOWNLOAD_DEVICE, ENDPOINT_DOWNLOAD_ROM_LINK, ENDPOINT_SUPPORT, populateEndpoint } from "~/util/endpoints";
import { startSignIn } from "~/util/sso";
import { usePublicFetchSignal } from "~/util/use-fetch";
import { formatReleased, formatSize, formatSupport } from "./format";

const DEVICE_SUPPORT_KB = `${ENDPOINT_SUPPORT}kb/100002`;

function DeviceHeader({ device }: { device: DownloadDevice }) {
    const [deviceImage] = useDeviceImage(device.manufacturer, device.image_model);

    return (
        <div class="device-header">
            <div class="device-image"><img src={deviceImage} alt="" /></div>
            <div>
                <h1>{device.manufacturer} {device.name}</h1>
                <p class={`device-support support-${device.support}`}>{formatSupport(device.support)}</p>
                {device.notes && <p class="device-notes">{device.notes}</p>}
            </div>
        </div>
    );
}

function NoRoms() {
    return (
        <div class="no-roms">
            <p>We don't have any downloads available for this device yet! Check back later.</p>
            <p><a href={DEVICE_SUPPORT_KB}>which devices does ReLiveWP support?</a></p>
        </div>
    );
}

function DownloadAction({ rom }: { rom: DownloadRom }) {
    const appState = useAppState();
    const location = useLocation();
    const busy = useSignal(false);
    const error = useSignal<string | null>(null);

    if (!appState.isAuthenticated.value) {
        return <ActionLink onClick={() => startSignIn(location.url)}>sign in to download</ActionLink>;
    }

    const download = async () => {
        const authenticatedFetch = appState.authenticatedFetch.value;
        if (authenticatedFetch === undefined) return;

        busy.value = true;
        error.value = null;
        try {
            const url = populateEndpoint(ENDPOINT_DOWNLOAD_ROM_LINK, { romId: rom.id });
            const response = await authenticatedFetch(url, {
                method: "POST",
                headers: { "Accept": "application/json" },
            });

            if (response.status === 429) {
                error.value = "you are being ratelimited! try again later";
                return;
            }

            if (!response.ok) {
                error.value = `failed to generate download link: ${response.status} ${response.statusText}`;
                return;
            }

            const link = await response.json() as RomLink;
            window.location.assign(link.url);
        }
        catch (e) {
            error.value = (e as Error).message;
        }
        finally {
            busy.value = false;
        }
    };

    return (
        <>
            {busy.value
                ? <span class="rom-busy">getting link...</span>
                : <ActionLink onClick={download}>download</ActionLink>}
            {error.value && <p class="error">{error.value}</p>}
        </>
    );
}

function RomDetailRow({ name, children }: { name: string, children: ComponentChildren }) {
    return (
        <dd class="rom-detail">
            <span class="rom-detail-name">{name}</span>
            <div class="rom-detail-value">{children}</div>
        </dd>
    );
}

function RomEntry({ rom }: { rom: DownloadRom }) {
    const [osName, , osImage] = useVersion(rom.os_version ?? "");

    return (
        <>
            <dt class="header">
                {osImage ? <img class="os-icon" src={osImage} alt="" /> : <span class="os-icon" />}
                <div>
                    <h4>{rom.name}</h4>
                    <p>version {rom.version}</p>
                </div>
                <div class="rom-actions">
                    <DownloadAction rom={rom} />
                </div>
            </dt>
            {rom.notes && <RomDetailRow name="notes">{rom.notes}</RomDetailRow>}
            {rom.os_version && (
                <RomDetailRow name="os">
                    <div>Windows Phone {osName}</div>
                    <div class="rom-detail-sub">{rom.os_version}</div>
                </RomDetailRow>
            )}
            {rom.released && <RomDetailRow name="released">{formatReleased(rom.released)}</RomDetailRow>}
            <RomDetailRow name="size">
                <div>{formatSize(rom.size_bytes)}</div>
                <div class="rom-detail-sub">{rom.file_name}</div>
            </RomDetailRow>
            {rom.sha256 && <RomDetailRow name="sha256"><code class="rom-hash">{rom.sha256}</code></RomDetailRow>}
        </>
    );
}

export default function DeviceDownloads({ deviceId }: { deviceId: string }) {
    const url = populateEndpoint(ENDPOINT_DOWNLOAD_DEVICE, { deviceId });
    const { data: detail, error } = usePublicFetchSignal<DownloadDeviceDetail>(url);

    useTitle(detail.value ? `${detail.value.device.name} downloads` : "downloads");

    return (
        <>
            <p><Link href="/downloads">all devices</Link></p>

            {error.value && <p class="error">{error.value}</p>}
            {!detail.value && !error.value && <p>fetching downloads...</p>}
            {detail.value && (
                <>
                    <DeviceHeader device={detail.value.device} />
                    {detail.value.roms.length === 0
                        ? <NoRoms />
                        : (
                            <dl class="rom-list">
                                {detail.value.roms.map(rom => <RomEntry key={rom.id} rom={rom} />)}
                            </dl>
                        )}
                </>
            )}
        </>
    );
}
