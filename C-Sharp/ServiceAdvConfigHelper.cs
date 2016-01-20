/*
 * (C) Vlad Hrybok, 2005-2016
 * Released under the terms of MIT license.
 * Available at https://github.com/vgribok/VladsOpenSourceBitsAndPieces
 */

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Diagnostics;
using System.ServiceProcess;

namespace Vlad.Framework.Windows
{
    #region Win32 API Declarations

    [Flags]
    enum ServiceControlAccessRights : int
    {
        SC_MANAGER_CONNECT = 0x0001, // Required to connect to the service control manager. 
        SC_MANAGER_CREATE_SERVICE = 0x0002, // Required to call the CreateService function to create a service object and add it to the database. 
        SC_MANAGER_ENUMERATE_SERVICE = 0x0004, // Required to call the EnumServicesStatusEx function to list the services that are in the database. 
        SC_MANAGER_LOCK = 0x0008, // Required to call the LockServiceDatabase function to acquire a lock on the database. 
        SC_MANAGER_QUERY_LOCK_STATUS = 0x0010, // Required to call the QueryServiceLockStatus function to retrieve the lock status information for the database
        SC_MANAGER_MODIFY_BOOT_CONFIG = 0x0020, // Required to call the NotifyBootConfigStatus function. 
        SC_MANAGER_ALL_ACCESS = 0xF003F // Includes STANDARD_RIGHTS_REQUIRED, in addition to all access rights in this table. 
    }

    [Flags]
    enum ServiceAccessRights : uint
    {
        SERVICE_NO_CHANGE = 0xFFFFFFFF,
        SERVICE_QUERY_CONFIG = 0x0001, // Required to call the QueryServiceConfig and QueryServiceConfig2 functions to query the service configuration. 
        SERVICE_CHANGE_CONFIG = 0x0002, // Required to call the ChangeServiceConfig or ChangeServiceConfig2 function to change the service configuration. Because this grants the caller the right to change the executable file that the system runs, it should be granted only to administrators. 
        SERVICE_QUERY_STATUS = 0x0004, // Required to call the QueryServiceStatusEx function to ask the service control manager about the status of the service. 
        SERVICE_ENUMERATE_DEPENDENTS = 0x0008, // Required to call the EnumDependentServices function to enumerate all the services dependent on the service. 
        SERVICE_START = 0x0010, // Required to call the StartService function to start the service. 
        SERVICE_STOP = 0x0020, // Required to call the ControlService function to stop the service. 
        SERVICE_PAUSE_CONTINUE = 0x0040, // Required to call the ControlService function to pause or continue the service. 
        SERVICE_INTERROGATE = 0x0080, // Required to call the ControlService function to ask the service to report its status immediately. 
        SERVICE_USER_DEFINED_CONTROL = 0x0100, // Required to call the ControlService function to specify a user-defined control code.
        SERVICE_ALL_ACCESS = 0xF01FF // Includes STANDARD_RIGHTS_REQUIRED in addition to all access rights in this table. 
    }

    enum ServiceConfig2InfoLevel : int
    {
        SERVICE_CONFIG_DESCRIPTION = 0x00000001, // The lpBuffer parameter is a pointer to a SERVICE_DESCRIPTION structure.
        SERVICE_CONFIG_FAILURE_ACTIONS = 0x00000002 // The lpBuffer parameter is a pointer to a SERVICE_FAILURE_ACTIONS structure.
    }

    public enum SC_ACTION_TYPE : uint
    {
        SC_ACTION_NONE = 0x00000000, // No action.
        SC_ACTION_RESTART = 0x00000001, // Restart the service.
        SC_ACTION_REBOOT = 0x00000002, // Reboot the computer.
        SC_ACTION_RUN_COMMAND = 0x00000003 // Run a command.
    }

    public struct SERVICE_FAILURE_ACTIONS
    {
        [MarshalAs(UnmanagedType.U4)]
        public UInt32 dwResetPeriod;
        [MarshalAs(UnmanagedType.LPStr)]
        public String lpRebootMsg;
        [MarshalAs(UnmanagedType.LPStr)]
        public String lpCommand;
        [MarshalAs(UnmanagedType.U4)]
        public UInt32 cActions;
        public IntPtr lpsaActions;
    }

    public struct SC_ACTION
    {
        public SC_ACTION(SC_ACTION_TYPE action, int delayMillisec)
        {
            this.Type = action;
            this.Delay = (UInt32)delayMillisec;
        }

        [MarshalAs(UnmanagedType.U4)]
        public SC_ACTION_TYPE Type;
        [MarshalAs(UnmanagedType.U4)]
        public UInt32 Delay;
    }

    #endregion

    #region Native Methods

    class NativeMethods
    {
        private NativeMethods() { }

        [DllImport("advapi32.dll", EntryPoint = "OpenSCManager")]
        public static extern IntPtr OpenSCManager(
            string machineName,
            string databaseName,
            ServiceControlAccessRights desiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "CloseServiceHandle")]
        public static extern int CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll", EntryPoint = "OpenService")]
        public static extern IntPtr OpenService(
            IntPtr hSCManager,
            string serviceName,
            ServiceAccessRights desiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "QueryServiceConfig2")]
        public static extern int QueryServiceConfig2(
            IntPtr hService,
            ServiceConfig2InfoLevel dwInfoLevel,
            IntPtr lpBuffer,
            int cbBufSize,
            out int pcbBytesNeeded);

        [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2")]
        public static extern int ChangeServiceConfig2(
            IntPtr hService,
            ServiceConfig2InfoLevel dwInfoLevel,
            IntPtr lpInfo);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern Boolean ChangeServiceConfig(
            IntPtr hService,
            UInt32 nServiceType,
            UInt32 nStartType,
            UInt32 nErrorControl,
            String lpBinaryPathName,
            String lpLoadOrderGroup,
            IntPtr lpdwTagId,
            [In] char[] lpDependencies,
            String lpServiceStartName,
            String lpPassword,
            String lpDisplayName);
    }

    #endregion

    public class ServiceControlManager : IDisposable
    {
        // Details at http://blogs.msdn.com/b/anlynes/archive/2006/07/30/683192.aspx

        public static void SetServiceImmediateAutorestartRecovery(string serviceName)
        {
            SC_ACTION restartImmediately = new SC_ACTION(SC_ACTION_TYPE.SC_ACTION_RESTART, 0);

            using(ServiceControlManager svcCtlMgr = new ServiceControlManager())
                svcCtlMgr.SetRestartOnFailure(serviceName, restartImmediately, restartImmediately, restartImmediately);
        }

        private IntPtr SCManager;
        private bool disposed;

        /// <summary>
        /// Calls the Win32 OpenService function and performs error checking.
        /// </summary>
        /// <exception cref="ComponentModel.Win32Exception">"Unable to open the requested Service."</exception>
        private IntPtr OpenService(string serviceName, ServiceAccessRights desiredAccess)
        {
            // Open the service
            IntPtr service = NativeMethods.OpenService(
                SCManager,
                serviceName,
                desiredAccess);

            // Verify if the service is opened
            if (service == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to open the requested Service.");
            }

            return service;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceControlManager"/> class.
        /// </summary>
        /// <exception cref="ComponentModel.Win32Exception">"Unable to open Service Control Manager."</exception>
        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public ServiceControlManager()
        {
            // Open the service control manager
            SCManager = NativeMethods.OpenSCManager(
                null,
                null,
                ServiceControlAccessRights.SC_MANAGER_CONNECT);

            // Verify if the SC is opened
            if (SCManager == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to open Service Control Manager.");
            }
        }

        /// <summary>
        /// Dertermines whether the nominated service is set to restart on failure.
        /// </summary>
        /// <exception cref="ComponentModel.Win32Exception">"Unable to query the Service configuration."</exception>
        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public bool HasRestartOnFailure(string serviceName)
        {
            const int bufferSize = 1024 * 8;

            IntPtr service = IntPtr.Zero;
            IntPtr bufferPtr = IntPtr.Zero;
            bool result = false;

            try
            {
                // Open the service
                service = OpenService(serviceName, ServiceAccessRights.SERVICE_QUERY_CONFIG);

                int dwBytesNeeded = 0;

                // Allocate memory for struct
                bufferPtr = Marshal.AllocHGlobal(bufferSize);
                int queryResult = NativeMethods.QueryServiceConfig2(
                    service,
                    ServiceConfig2InfoLevel.SERVICE_CONFIG_FAILURE_ACTIONS,
                    bufferPtr,
                    bufferSize,
                    out dwBytesNeeded);

                if (queryResult == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to query the Service configuration.");
                }

                // Cast the buffer to a QUERY_SERVICE_CONFIG struct
                SERVICE_FAILURE_ACTIONS config =
                    (SERVICE_FAILURE_ACTIONS)Marshal.PtrToStructure(bufferPtr, typeof(SERVICE_FAILURE_ACTIONS));

                // Determine whether the service is set to auto restart
                if (config.cActions != 0)
                {
                    SC_ACTION action = (SC_ACTION)Marshal.PtrToStructure(config.lpsaActions, typeof(SC_ACTION));
                    result = (action.Type == SC_ACTION_TYPE.SC_ACTION_RESTART);
                }

                return result;
            }
            finally
            {
                // Clean up
                if (bufferPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(bufferPtr);
                }

                if (service != IntPtr.Zero)
                {
                    NativeMethods.CloseServiceHandle(service);
                }
            }
        }

        /// <summary>
        /// Sets the nominated service to restart on failure.
        /// </summary>
        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public void SetRestartOnFailure(string serviceName, params SC_ACTION[] actions)
        {
            if (actions == null || actions.Length < 1 || actions.Length > 3)
                throw new ArgumentException("actions", "Service recovery actions argument must have from 1 to 3 action items.");

            IntPtr service = IntPtr.Zero;
            IntPtr failureActionsPtr = IntPtr.Zero;
            IntPtr actionPtr = IntPtr.Zero;

            int actionStructSize = Marshal.SizeOf(typeof(SC_ACTION));

            try
            {
                // Open the service
                service = this.OpenService(serviceName,
                    ServiceAccessRights.SERVICE_CHANGE_CONFIG |
                    ServiceAccessRights.SERVICE_START);

                // Allocate memory for the individual actions
                actionPtr = Marshal.AllocHGlobal(actionStructSize * actions.Length);

                for (int i = 0; i < actions.Length; i++)
                {
                    Marshal.StructureToPtr(actions[i], (IntPtr)((Int64)actionPtr + actionStructSize * i), false);
                }

                // Set up the failure actions
                SERVICE_FAILURE_ACTIONS failureActions = new SERVICE_FAILURE_ACTIONS();
                failureActions.dwResetPeriod = 0;
                failureActions.cActions = (uint)actions.Length;
                failureActions.lpsaActions = actionPtr;

                failureActionsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SERVICE_FAILURE_ACTIONS)));
                Marshal.StructureToPtr(failureActions, failureActionsPtr, false);

                // Make the change
                int changeResult = NativeMethods.ChangeServiceConfig2(
                    service,
                    ServiceConfig2InfoLevel.SERVICE_CONFIG_FAILURE_ACTIONS,
                    failureActionsPtr);

                // Check that the change occurred
                if (changeResult == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to change the Service configuration.");
                }
            }
            finally
            {
                // Clean up
                if (failureActionsPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(failureActionsPtr);
                }

                if (actionPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(actionPtr);
                }

                if (service != IntPtr.Zero)
                {
                    NativeMethods.CloseServiceHandle(service);
                }
            }
        }

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static void ChangeStartMode(string serviceName, ServiceStartMode mode)
        {
            var scManagerHandle = NativeMethods.OpenSCManager(null, null, ServiceControlAccessRights.SC_MANAGER_ALL_ACCESS);
            if (scManagerHandle == IntPtr.Zero)
            {
                throw new ExternalException("Open Service Manager Error");
            }

            var serviceHandle = NativeMethods.OpenService(
                scManagerHandle,
                serviceName,
                ServiceAccessRights.SERVICE_QUERY_CONFIG | ServiceAccessRights.SERVICE_CHANGE_CONFIG);

            if (serviceHandle == IntPtr.Zero)
            {
                throw new ExternalException("Open Service Error");
            }

            var result = NativeMethods.ChangeServiceConfig(
                serviceHandle,
                (uint)ServiceAccessRights.SERVICE_NO_CHANGE,
                (uint)mode,
                (uint)ServiceAccessRights.SERVICE_NO_CHANGE,
                null,
                null,
                IntPtr.Zero,
                null,
                null,
                null,
                null);

            if (result == false)
            {
                int nError = Marshal.GetLastWin32Error();
                var win32Exception = new Win32Exception(nError);
                throw new ExternalException("Could not change service start type: " + win32Exception.Message);
            }
        }

        #region IDisposable Members

        /// <summary>
        /// See <see cref="IDisposable.Dispose"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Implements the Dispose(bool) pattern outlined by MSDN and enforced by FxCop.
        /// </summary>
        private void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources here
                }

                // Unmanaged resources always need disposing
                if (SCManager != IntPtr.Zero)
                {
                    NativeMethods.CloseServiceHandle(SCManager);
                    SCManager = IntPtr.Zero;
                }
            }
            disposed = true;
        }

        /// <summary>
        /// Finalizer for the <see cref="ServiceControlManager"/> class.
        /// </summary>
        ~ServiceControlManager()
        {
            Dispose(false);
        }

        #endregion
    }
}
