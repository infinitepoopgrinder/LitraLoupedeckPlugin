namespace Loupedeck.LitraPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    using HidLibrary;

    class Constants
    {
        public static readonly Byte nMaxBrightness = 0xfa;
        public static readonly Byte nMinBrightness = 0x14;
        public static readonly UInt16 nMaxTemperatur = 6500;
        public static readonly UInt16 nMinTemperatur = 2700;

        public static readonly Int32 nVId = 0x046D;
        public static readonly Int32 nPIdGlow = 0xc900;
        public static readonly Int32 nPIdBeam = 0xc901;

    }


    // This class contains the plugin-level logic of the Loupedeck plugin.

    class LitraDevice
    {
        private static readonly Byte[] offCommandData = { 0x11, 0xff, 0x04, 0x1c, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        private static readonly Byte[] onCommandData = { 0x11, 0xff, 0x04, 0x1c, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        private static readonly HidReport offHidReport = new HidReport(20, new HidDeviceData(offCommandData, HidDeviceData.ReadStatus.NoDataRead));
        private static readonly HidReport onHidReport = new HidReport(20, new HidDeviceData(onCommandData, HidDeviceData.ReadStatus.NoDataRead));

        private readonly HidDevice m_hidDevice;
        private Boolean m_bShouldBeOn;
        private Byte    m_nBrightness;
        private UInt16  m_nTemperature;

        public LitraDevice(HidDevice hidDevice)
        {
            this.m_hidDevice = hidDevice;
            this.m_hidDevice.OpenDevice();
            this.m_hidDevice.MonitorDeviceEvents = true;
            this.m_hidDevice.Removed += this.OnDeviceRemoved;
            this.m_hidDevice.Inserted += this.OnDeviceInserted;
            this.m_hidDevice.ReadReport(this.OnReport);
            this.On = false;
        }

        private void OnReport(HidReport report)
        {
            try
            {
                report.GetBytes();
                var id = report.ReportId;
                // todo parse and interprete
            }
            finally
            {
                this.m_hidDevice.ReadReport(this.OnReport);
            }
        }

        private void OnDeviceRemoved() => this.m_bShouldBeOn = false;

        private void OnDeviceInserted() => this.On = false;  // for the same behavior as the ghub thingy;


        public Byte Brightness
        {
            get { return this.m_nBrightness; }
            set
            {
                if (value > Constants.nMaxBrightness)
                {
                    this.m_nBrightness = Constants.nMaxBrightness;
                }
                else if (value < Constants.nMinBrightness)
                {
                    this.m_nBrightness = Constants.nMinBrightness;
                }
                else
                {
                    this.m_nBrightness = value;
                }

                if (!this.m_hidDevice.IsOpen)
                {
                    this.m_hidDevice.OpenDevice();
                    if (!this.m_hidDevice.IsOpen)
                    {
                        return;
                    }
                }
                Byte[] data = { 0x11, 0xff, 0x04, 0x4c, 0x00, this.m_nBrightness, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                this.m_hidDevice.WriteReport(new HidReport(20, new HidDeviceData(data, HidDeviceData.ReadStatus.NoDataRead)));
            }
        }

        public UInt16 Temperatur
        {
            get { return this.m_nTemperature; }
            set
            {
                if (value > Constants.nMaxTemperatur)
                {
                    this.m_nTemperature = Constants.nMaxTemperatur;
                }
                else if (value < Constants.nMinTemperatur)
                {
                    this.m_nTemperature = Constants.nMinTemperatur;
                }
                else
                {
                    this.m_nTemperature = value;
                }

                if (!this.m_hidDevice.IsConnected)
                {
                    return;
                }

                if (!this.m_hidDevice.IsOpen)
                {
                    this.m_hidDevice.OpenDevice();
                    if (!this.m_hidDevice.IsOpen)
                    {
                        return;
                    }
                }
                var val = BitConverter.GetBytes(this.m_nTemperature);
                Byte[] data = { 0x11, 0xff, 0x04, 0x9c, val[1], val[0], 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                this.m_hidDevice.WriteReport(new HidReport(20, new HidDeviceData(data, HidDeviceData.ReadStatus.NoDataRead)));
            }
        }

        public Boolean On
        {
            get { return this.m_bShouldBeOn;}
            set
            {
                this.m_bShouldBeOn = value;

                if (!this.m_hidDevice.IsConnected)
                {
                    return;
                }

                if (!this.m_hidDevice.IsOpen)
                {
                    this.m_hidDevice.OpenDevice();
                    if (!this.m_hidDevice.IsOpen)
                    {
                        return;
                    }
                }

                if (this.m_bShouldBeOn)
                {
                    this.m_hidDevice.WriteReport(onHidReport);
                }
                else
                {
                    this.m_hidDevice.WriteReport(offHidReport);
                }
            }
        }



    }

    public class LitraPlugin : Plugin
    {
        Byte    m_nGlobalBrightness;
        UInt16  m_nGlobalTemperature;
        Boolean m_bGlobalIsOn;

        private ObservableCollection<LitraDevice> m_devices;
        // Gets a value indicating whether this is an Universal plugin or an Application plugin.
        public override Boolean UsesApplicationApiOnly => true;

        // Gets a value indicating whether this is an API-only plugin.
        public override Boolean HasNoApplication => true;

        // This method is called when the plugin is loaded during the Loupedeck service start-up.
        public override void Load()
        {
            this.m_nGlobalBrightness = Constants.nMinBrightness;
            this.m_nGlobalTemperature = (UInt16)((Constants.nMaxTemperatur + Constants.nMinTemperatur) / 2);
            this.m_bGlobalIsOn= false;

            this.m_devices = new ObservableCollection<LitraDevice>();

            IEnumerable<HidDevice> devices = HidDevices.Enumerate(Constants.nVId, new int[]{Constants.nPIdGlow, Constants.nPIdBeam} );

            foreach (HidDevice device in devices)
            {
                if (device == null)
                {
                    continue;
                }

                if (!device.IsConnected)
                {
                    continue;
                }

                if (20 != device.Capabilities.InputReportByteLength)
                {
                    continue;
                }

                this.m_devices.Add(new LitraDevice(device));
            }
        }

        // This method is called when the plugin is unloaded during the Loupedeck service shutdown.
        public override void Unload()
        {
        }

        public Byte Brightness
        {
            get { return this.m_nGlobalBrightness; }
            set
            {
                if (value > Constants.nMaxBrightness)
                {
                    this.m_nGlobalBrightness = Constants.nMaxBrightness;
                }
                else if (value < Constants.nMinBrightness)
                {
                    this.m_nGlobalBrightness = Constants.nMinBrightness;
                }
                else
                {
                    this.m_nGlobalBrightness = value;
                }

                foreach (LitraDevice litraDevice in this.m_devices)
                {
                    var nBrightness = litraDevice.Brightness;
                    litraDevice.Brightness = this.m_nGlobalBrightness;
                }
            }
        }

        public UInt16 Temperatur
        {
            get { return this.m_nGlobalTemperature; }
            set
            {
                if (value > Constants.nMaxTemperatur)
                {
                    this.m_nGlobalTemperature = Constants.nMaxTemperatur;
                }
                else if (value < Constants.nMinTemperatur)
                {
                    this.m_nGlobalTemperature = Constants.nMinTemperatur;
                }
                else
                {
                    this.m_nGlobalTemperature = value;
                }

                foreach (LitraDevice litraDevice in this.m_devices)
                {
                    var nTemperatur = litraDevice.Temperatur;
                    litraDevice.Temperatur = this.m_nGlobalTemperature;
                }
            }
        }

        public Boolean On
        {
            get { return this.m_bGlobalIsOn; }
            set
            {
                this.m_bGlobalIsOn = value;

                foreach (LitraDevice litraDevice in this.m_devices)
                {
                    litraDevice.On = this.m_bGlobalIsOn;
                }
            }
        }
    }

    class ToggleLitraCommand : PluginDynamicCommand
    {
        public ToggleLitraCommand()
            : base(displayName: "Toggle all Litra", description: "Toggles all Litra on/off", groupName: "Light")
        {
            this.AddParameter("device", "Device", "Target");
            if (this.Plugin is LitraPlugin litraPlugin)
            {
                this.DisplayName = $"{(litraPlugin.On ? "On" : "Off")}";
                this.ActionImageChanged();
            }
        }

        protected override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            if (this.Plugin is LitraPlugin litraPlugin)
            {
                return $"Turn\nLitra\n{(!litraPlugin.On ? "On" : "Off")}";
            }
            else
            {
                return null;
            }
        }

        protected override void RunCommand(String actionParameter)
        {
            if (this.Plugin is LitraPlugin litraPlugin)
            {
                litraPlugin.On = !litraPlugin.On;
                this.ActionImageChanged();
            }
        }
    }

    class AdjustBrightnessCommand : PluginDynamicAdjustment
    {

        public AdjustBrightnessCommand()
            : base(displayName: "Brightness", description: "Brightness", groupName:"Light", hasReset: true, supportedDevices: DeviceType.All)
        {
            //this.ActionImageChanged();
        }

        protected override string GetAdjustmentDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            return GetCommandDisplayName(actionParameter, imageSize);
        }

        protected override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            if (this.Plugin is LitraPlugin litraPlugin)
            {
                return $"{litraPlugin.Brightness}";
            }
            else
            {
                return null;
            }
        }

        protected override void RunCommand(string actionParameter)
        {
            if (this.Plugin is LitraPlugin litraPlugin)
            {
                litraPlugin.On = !litraPlugin.On;
            }

        }

        protected override void ApplyAdjustment(string actionParameter, int diff)
        {
            if (this.Plugin is LitraPlugin litraPlugin)
            {
                litraPlugin.Brightness = (Byte)(litraPlugin.Brightness + diff);
                this.ActionImageChanged();
            }
        }
    }

    class AdjustTemperaturCommand : PluginDynamicAdjustment
    {

        public AdjustTemperaturCommand()
            : base(displayName: "Temperature", description: "Temperature", groupName: "Light", hasReset: true, supportedDevices: DeviceType.All)
        {
            if (this.Plugin is LitraPlugin litraPlugin)
            {
                this.ActionImageChanged();
            }
        }

        protected override string GetAdjustmentDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            return GetCommandDisplayName(actionParameter, imageSize);
        }

        protected override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            if (this.Plugin is LitraPlugin litraPlugin)
            {
                return $"{litraPlugin.Temperatur}K";
            }
            else
            {
                return null;
            }
        }

        protected override void RunCommand(string actionParameter)
        {
            if (this.Plugin is LitraPlugin litraPlugin)
            {
                litraPlugin.On = !litraPlugin.On;
            }

        }

        protected override void ApplyAdjustment(string actionParameter, int diff)
        {
            if (this.Plugin is LitraPlugin litraPlugin)
            {
                litraPlugin.Temperatur = (UInt16)(litraPlugin.Temperatur + diff * 100);
                this.ActionImageChanged();
            }
        }
    }

}
