import { DeviceSupport, DownloadDevice } from "~/util/download-types";

const SUPPORT_LABELS: Record<DeviceSupport, string> = {
    supported: "supported",
    partial: "partially supported",
    unsupported: "not supported",
    untested: "untested",
};

const SIZE_UNITS = ["bytes", "KB", "MB", "GB"];

export function formatSupport(support: DeviceSupport): string {
    return SUPPORT_LABELS[support] ?? support;
}

export function formatSize(bytes: number): string {
    let size = bytes;
    let unit = 0;
    while (size >= 1024 && unit < SIZE_UNITS.length - 1) {
        size /= 1024;
        unit++;
    }

    const digits = unit === 0 ? 0 : 1;
    return `${size.toFixed(digits)} ${SIZE_UNITS[unit]}`;
}

export function formatReleased(released: string): string {
    const date = new Date(`${released}T00:00:00Z`);
    if (isNaN(date.getTime()))
        return released;

    return date.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric", timeZone: "UTC" });
}

export function matchesRegisteredModel(device: DownloadDevice, registeredModels: string[]): boolean {
    return registeredModels.some(model => {
        const normalised = model.toUpperCase();
        return device.model_codes.some(code => normalised.includes(code.toUpperCase()));
    });
}
