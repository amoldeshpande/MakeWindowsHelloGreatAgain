using System;
using System.ServiceProcess;

namespace MakeWindowsHelloGreatAgain
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
#if DEBUG
            {
                CameraTogglerService service = new CameraTogglerService();
                service.Run(args);
            }
#else
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new CameraTogglerService()
            };
            ServiceBase.Run(ServicesToRun);
#endif
        }
    }
}
