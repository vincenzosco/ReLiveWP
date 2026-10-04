import "./device-picker.scss";

import { Signal } from "@preact/signals";
import useDeviceImage from "~/util/device-image";
import { Device, Devices } from "~/util/device-types";

type DevicePickerProps = {
    deviceId: Signal<string | null>;
    devices: Devices
}

function DeviceOption({ device }: { device: Device }) {
    const [_, deviceImageSmall] = useDeviceImage(device.manufacturer, device.model);

    return (
        <option value={device.id}>
            <div class="device-option">
                <div class="device-image"><img src={deviceImageSmall} /></div>
                <span class="device-name">{device.friendly_name}</span>
                <span class="device-color" style={{ backgroundColor: device.accent_colour }}></span>
            </div>
        </option>
    )
}

export default function DevicePicker({ deviceId, devices }: DevicePickerProps) {
    return (
        <select class="device-picker" value={deviceId.value ?? undefined} onChange={e => deviceId.value = e.currentTarget.value}>
            <button>
                <selectedcontent></selectedcontent>
            </button>
            {devices.map(d => <DeviceOption key={d.id} device={d} />)}
        </select>
    )
}