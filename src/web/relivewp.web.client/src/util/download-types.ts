export type DeviceSupport = "supported" | "partial" | "unsupported" | "untested";

export type DownloadDevice = {
    id: string;
    name: string;
    manufacturer: string;
    model_codes: string[];
    image_model: string;
    support: DeviceSupport;
    notes: string | null;
    rom_count: number;
};

export type DownloadRom = {
    id: string;
    name: string;
    version: string;
    os_version: string | null;
    released: string | null;
    size_bytes: number;
    sha256: string | null;
    file_name: string;
    notes: string | null;
};

export type DownloadDeviceDetail = {
    device: DownloadDevice;
    roms: DownloadRom[];
};

export type RomLink = {
    url: string;
    expires_at: string;
};
