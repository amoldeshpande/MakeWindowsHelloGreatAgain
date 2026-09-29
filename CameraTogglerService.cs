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
            webcamToLookFor = ConfigurationManager.AppSettings["WebcamName"];
            enableVerboseLogging = bool.Parse(ConfigurationManager.AppSettings["EnableVerboseLogging"] ?? "false");
            eventLog.WriteEntry($"CameraTogglerService started. Looking for webcam: {webcamToLookFor}", EventLogEntryType.Information);
        }
        private void enableOrDisableCamera(bool enable)
        {
            List<ManagementObject> devices = new List<ManagementObject>();
            ManagementObject deviceFound = null;

            ManagementObjectSearcher searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_PnPEntity WHERE Name LIKE '{webcamToLookFor}%'");
            foreach (ManagementObject device in searcher.Get())
            {
                devices.Add(device);
                //foreach (PropertyData property in device.Properties)
                //{
                //    Debug.WriteLine($"{property.Name}: {property.Value}");
                //}
            }
            if(devices.Count > 1)
            {
                if(enableVerboseLogging)
                {
                    eventLog.WriteEntry($"Found multiple camera devices matching '{webcamToLookFor}'. Looking for root device", EventLogEntryType.Warning);
                }
                foreach(ManagementObject device in devices)
                {
                    string id = device["DeviceID"]?.ToString();
                    if (id != null && id.EndsWith("00"))
                    {
                        deviceFound = device;
                        break;
                    }
                }
            }
            if(deviceFound == null )
            {
                eventLog.WriteEntry($"Did not find a root device for '{webcamToLookFor}'", EventLogEntryType.Error);
                return;
            }

            string deviceName = deviceFound["Name"]?.ToString();
            string deviceId = deviceFound["DeviceID"]?.ToString();
            string errorCode = deviceFound["ConfigManagerErrorCode"]?.ToString();
            Guid ClassGuid = (deviceFound["ClassGuid"] != null ? new Guid(deviceFound["ClassGuid"].ToString()) : Guid.Empty);

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

        protected override void OnStop()
        {
        }
        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            base.OnSessionChange(changeDescription);

            switch(changeDescription.Reason)
            {
                case SessionChangeReason.SessionLogon:
                    // Handle user logon event
                    if(enableVerboseLogging)
                    {
                        eventLog.WriteEntry($"User logged on: Session ID {changeDescription.SessionId}, enabling camera", EventLogEntryType.Information);
                    }
                    enableOrDisableCamera(true);
                    break;
                case SessionChangeReason.SessionLogoff:
                    // Handle user logoff event
                    if(enableVerboseLogging)
                    {
                        eventLog.WriteEntry($"User logged off: Session ID {changeDescription.SessionId}, disabling camera",EventLogEntryType.Information);
                    }
                    enableOrDisableCamera(false);
                    break;
                case SessionChangeReason.SessionLock:
                    // Handle session lock event
                    if(enableVerboseLogging)
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
