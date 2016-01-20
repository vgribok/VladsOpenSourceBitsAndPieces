/*
*	(C) Vlad Hrybok
*	Released under terms of MIT license. 
*	Available at https://github.com/vgribok/VladsOpenSourceBitsAndPieces	
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceProcess;

namespace Vlad.Framework.Windows
{
    public static class ServiceUtilities
    {
        public static ServiceController ServiceFromName(string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName))
                return null;

            try
            {
                return new ServiceController(serviceName);
            }
            catch
            {
                return null;
            }
        }

        public static bool ServiceExists(string serviceName)
        {
            try
            {
                ServiceController svc = new ServiceController(serviceName);
                svc.ServiceName.ToString();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool StartService(string serviceName, int secondsToWait)
        {
            return StartService(new ServiceController(serviceName), secondsToWait);
        }

        public static bool StartService(ServiceController service, int secondsToWait)
        {
            if (service.Status == ServiceControllerStatus.Running)
                return true;

            if (service.Status != ServiceControllerStatus.StartPending)
                service.Start();

            if (secondsToWait <= 0)
                return true;

            TimeSpan maxWaitTime = new TimeSpan(0, 0, 0, secondsToWait); // seconds
            service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, maxWaitTime);

            return service.Status == System.ServiceProcess.ServiceControllerStatus.Running;
        }

        public static void StopService(string serviceName, int secondsToWait)
        {
            try
            {
                ServiceController svc = new ServiceController(serviceName);
                if(CanStopService(svc))
                    StopService(svc, secondsToWait);
            }
            catch
            {
                // Service not found - ignore
            }
        }

        public static void StopService(ServiceController service, int secondsToWait)
        {
            if (service.Status == ServiceControllerStatus.Stopped)
                return;

            if (service.Status != ServiceControllerStatus.StopPending)
                service.Stop();

            if (secondsToWait != 0)
                try
                {
                    service.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 0, 0, secondsToWait));
                }
                catch (System.ServiceProcess.TimeoutException)
                {
                    // DO nothing
                }
        }

        private static bool CanChangeStatus(ServiceController service, params ServiceControllerStatus[] invalidStatuses)
        {
            if (service == null)
                return false;

            try
            {
                service.Status.ToString();
            }
            catch { return false; }

            foreach(ServiceControllerStatus invalidStatus in invalidStatuses)
                if (service.Status == invalidStatus)
                    return false;

            return true;
        }

        public static bool CanStartService(ServiceController service)
        {
            return CanChangeStatus(service, ServiceControllerStatus.Running, ServiceControllerStatus.StartPending);
        }

        public static bool CanStopService(ServiceController service)
        {
            return CanChangeStatus(service, ServiceControllerStatus.Stopped, ServiceControllerStatus.StopPending);
        }

        public static ServiceControllerStatus? GetServiceStatus(ServiceController service)
        {
            if (service == null)
                return null;

            try
            {
                return service.Status;
            }
            catch { return null; }
        }

        /// <summary>
        /// Returns true if reached the status within given time period.
        /// </summary>
        /// <param name="service"></param>
        /// <param name="milliseconds"></param>
        /// <returns></returns>
        public static bool WaitForStatus(ServiceController service, ServiceControllerStatus expectedStatus, int milliseconds)
        {
            if (service == null)
                return false;

            try
            {
                if (milliseconds < 0)
                    service.WaitForStatus(expectedStatus);
                else
                    service.WaitForStatus(expectedStatus, new TimeSpan(0, 0, 0, 0, milliseconds));
            }
            catch (System.ServiceProcess.TimeoutException)
            {
            }

            service.Refresh();
            return service.Status == expectedStatus;
        }
    }
}
