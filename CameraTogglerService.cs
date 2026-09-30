using System;
using System.Configuration;
using System.Diagnostics;
using System.ServiceProcess;
using System.Management;
using System.Collections.Generic;

namespace MakeWindowsHelloGreatAgain
{
    public partial class CameraTogglerService : ServiceBase
    {
        private string webcamToLookFor = "Integrated Webcam";
        private string webcamDeviceId = null;
        private EventLog eventLog = new EventLog();
        private bool enableVerboseLogging = false;
        public CameraTogglerService()
        {
            CanPauseAndContinue = true;
            CanHandleSessionChangeEvent = true;
            ServiceName = "CameraTogglerService"; 
            if(!EventLog.SourceExists(ServiceName))
            {
                EventLog.CreateEventSource(ServiceName, "Application");
            }
            eventLog.Source = ServiceName;
            eventLog.Log = "Application";
        }
#if DEBUG
        public void Run(string[] args)
        {
            OnStart(args);
            Console.WriteLine("Service is running. Press Enter to stop...");
            Console.ReadLine();
            OnStop();
        }
#endif

        protected override void OnStart(string[] args)
        {
            webcamToLookFor = ConfigurationManager.AppSettings["WebcamName"] ?? null;
            webcamDeviceId = ConfigurationManager.AppSettings["WebcamDeviceId"] ?? null;
            enableVerboseLogging = bool.TryParse(ConfigurationManager.AppSettings["EnableVerboseLogging"] ?? "false", out bool verboseLogging) && verboseLogging;
            eventLog.WriteEntry($"CameraTogglerService started. Looking for webcam: {webcamToLookFor}", EventLogEntryType.Information);
        }
        private void enableOrDisableCamera(bool enable)
        {
            List<ManagementObject> devices = new List<ManagementObject>();
            ManagementObject deviceFound = null;

            if (string.IsNullOrEmpty(webcamToLookFor))
            {
                eventLog.WriteEntry($"No webcam name specified in configuration", EventLogEntryType.Error);
                return;
            }

            ManagementObjectSearcher searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_PnPEntity WHERE Name LIKE '{webcamToLookFor}%'");
            using (ManagementObjectCollection collection = searcher.Get())
            {
                foreach (ManagementObject device in collection)
                {
                    devices.Add(device);
                    // If there is a specific device instance ID specified, check if this device matches that ID.
                    if (!string.IsNullOrEmpty(webcamDeviceId) && device["DeviceID"]?.ToString() == webcamDeviceId)
                    {
                        deviceFound = device;
                        break;
                    }
                    //foreach (PropertyData property in device.Properties)
                    //{
                    //    Debug.WriteLine($"{property.Name}: {property.Value}");
                    //}
                }
            }
            try
            {
                // A device instance ID was specified, but no matching device was found. Log an error and return.
                if (!string.IsNullOrEmpty(webcamDeviceId) && deviceFound == null)
                {
                    eventLog.WriteEntry($"Did not find a camera device with Device ID '{webcamDeviceId}'", EventLogEntryType.Error);
                    return;
                }
                // If there is only one device found, use that one.
                if (deviceFound == null && devices.Count == 1)
                {
                    deviceFound = devices[0];
                }
                // If there are multiple devices a device instance ID has not been specified so look for the root device (the one with DeviceID ending in 00).
                if ((deviceFound == null) && devices.Count > 1)
                {
                    if (enableVerboseLogging)
                    {
                        eventLog.WriteEntry($"Found multiple camera devices matching '{webcamToLookFor}'. Looking for root device", EventLogEntryType.Warning);
                    }
                    foreach (ManagementObject device in devices)
                    {
                        string id = device["DeviceID"]?.ToString();
                        if (id != null && id.EndsWith("00"))
                        {
                            deviceFound = device;
                            break;
                        }
                    }
                }
                // No specific device instance ID was specified, and multiple devices were found, but no root device was found. Log an error and return.
                if (deviceFound == null)
                {
                    eventLog.WriteEntry($"Multiple camera devices found matching '{webcamToLookFor}', but no root device found. Disabling a camera in such a case will cause issues.", EventLogEntryType.Error);
                    return;
                }

                string deviceName = deviceFound["Name"]?.ToString();
                string deviceId = deviceFound["DeviceID"]?.ToString();
                string errorCode = deviceFound["ConfigManagerErrorCode"]?.ToString();

                bool isDisabled = (errorCode != "0");// (errorCode == "22"); 22 is actually disabled, but we'll treat any error code as disabled for safety.


                if (enableVerboseLogging)
                {
                    eventLog.WriteEntry($"Found camera device: {deviceName}, Device ID: {deviceId}, Error Code: {errorCode}, Is Disabled: {isDisabled}", EventLogEntryType.Information);
                }

                if (!enable && isDisabled)
                {
                    eventLog.WriteEntry($"Camera is already disabled, no action will be taken.", EventLogEntryType.Warning);
                    return;
                }
                if (enable && !isDisabled)
                {
                    eventLog.WriteEntry($"Camera is already enabled, no action will be taken.", EventLogEntryType.Warning);
                    return;
                }

                if (enable)
                {
                    // args can't be null, no matter what the interwebs tell you
                    object[] dummyargs = { "dummy", "wtf" };
                    deviceFound.InvokeMethod("Enable", dummyargs);
                }
                else
                {
                    // args can't be null, no matter what the interwebs tell you
                    object[] dummyargs = { "dummy", "wtf" };
                    deviceFound.InvokeMethod("Disable", dummyargs);
                }
            }
            catch(Exception ex)
            {
                eventLog.WriteEntry($"Error while trying to {(enable ? "enable" : "disable")} camera: {ex.ToString()}", EventLogEntryType.Error);
            }
            finally
            {
                foreach (ManagementObject device in devices)
                {
                    device.Dispose();
                }
                devices.Clear();
            }
        }

        protected override void OnStop()
        {
            eventLog.Dispose();
        }
        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            base.OnSessionChange(changeDescription);

            switch (changeDescription.Reason)
            {
                case SessionChangeReason.SessionLogon:
                    // Handle user logon event
                    if (enableVerboseLogging)
                    {
                        eventLog.WriteEntry($"User logged on: Session ID {changeDescription.SessionId}, enabling camera", EventLogEntryType.Information);
                    }
                    enableOrDisableCamera(true);
                    break;
                case SessionChangeReason.SessionLogoff:
                    // Handle user logoff event
                    if (enableVerboseLogging)
                    {
                        eventLog.WriteEntry($"User logged off: Session ID {changeDescription.SessionId}, disabling camera", EventLogEntryType.Information);
                    }
                    enableOrDisableCamera(false);
                    break;
                case SessionChangeReason.SessionLock:
                    // Handle session lock event
                    if (enableVerboseLogging)
                    {
                        eventLog.WriteEntry($"Session locked: Session ID {changeDescription.SessionId}, disabling camera", EventLogEntryType.Information);
                    }
                    enableOrDisableCamera(false);
                    break;
                case SessionChangeReason.SessionUnlock:
                    // Handle session unlock event
                    if (enableVerboseLogging)
                    {
                        eventLog.WriteEntry($"Session unlocked: Session ID {changeDescription.SessionId}, enabling camera", EventLogEntryType.Information);
                    }
                    enableOrDisableCamera(true);
                    break;
                default:
                    break;
            }
        }
    }
}
