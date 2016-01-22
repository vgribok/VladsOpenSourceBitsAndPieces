/*
	This code is released under the terms of MIT license.
*/
using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;

namespace HttpConfig
{
	internal class HttpApi : IDisposable
	{
        private static volatile int refCount = 0;
        private static object locker = new object();
        private static volatile bool initialized = false;

        #region IDisposable Members

        public HttpApi()
        {
            lock (locker)
            {
                if (!initialized)
                {
                    HttpApi.Error error = HttpApi.HttpInitialize(
                                                new HttpApi.HTTPAPI_VERSION(1, 0),
                                                HttpApi.InitFlag.HTTP_INITIALIZE_CONFIG,
                                                IntPtr.Zero);
                    ErrorCheck.VerifySuccess(error, "HttpAPI Initialization");

                    initialized = true;
                }

                refCount++;
            }

        }

        public void Dispose()
        {
            lock (locker)
            {
                refCount--;
                if (refCount == 0 && initialized)
                {
                    Error err = HttpApi.HttpTerminate(HttpApi.InitFlag.HTTP_INITIALIZE_CONFIG, IntPtr.Zero);
                    if (err == Error.NO_ERROR)
                        initialized = false;
                }
            }
        }

        #endregion

        #region Methods
        [DllImport("Httpapi.dll")]
        internal static extern HttpApi.Error HttpInitialize(
	        HTTPAPI_VERSION version,
	        InitFlag flags,
	        IntPtr reserved);

        [DllImport("Httpapi.dll")]
        internal static extern HttpApi.Error HttpQueryServiceConfiguration(
            IntPtr                  ServiceHandle,
            HTTP_SERVICE_CONFIG_ID  ConfigId,
            IntPtr                  pInputConfigInfo,
            int                     InputConfigInfoLength,
            IntPtr                  pOutputConfigInfo,
            int                     OutputConfigInfoLength,
            out int                 pReturnLength,
            IntPtr                  pOverlapped);

        [DllImport("Httpapi.dll")]
        internal static extern HttpApi.Error HttpSetServiceConfiguration(
            IntPtr                  ServiceHandle,
            HTTP_SERVICE_CONFIG_ID  ConfigId,
            IntPtr                  pConfigInformation,
            int                     ConfigInformationLength,
            IntPtr                  pOverlapped);

        [DllImport("Httpapi.dll")]
        internal static extern HttpApi.Error HttpDeleteServiceConfiguration(
            IntPtr                  ServiceHandle,
            HTTP_SERVICE_CONFIG_ID  ConfigId,
            IntPtr                  pConfigInformation,
            int                     ConfigInformationLength,
            IntPtr                  pOverlapped);

        [DllImport("Httpapi.dll")]
        internal static extern HttpApi.Error HttpTerminate(
	        InitFlag flags,
	        IntPtr reserved);
        
        [DllImport("Kernel32.dll", EntryPoint="RtlZeroMemory")]
        internal static extern void ZeroMemory(
            IntPtr dest,
            int size);
		#endregion Methods

		#region Structures
        [StructLayout(LayoutKind.Sequential)]
        internal struct HTTPAPI_VERSION
        {
	        internal HTTPAPI_VERSION(ushort majorVersion, ushort minorVersion)
	        {
		        major = majorVersion;
		        minor = minorVersion;
	        }

	        internal ushort major;
	        internal ushort minor;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SOCKADDR_STORAGE
        {
            internal short sa_family;
            
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType=UnmanagedType.U1, SizeConst=6)]
            internal byte[] __ss_pad1;

            internal long __ss_align;

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType=UnmanagedType.U1, SizeConst=112)]
            internal byte[] __ss_pad2;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HTTP_SERVICE_CONFIG_IP_LISTEN_QUERY
        {
            internal int                AddrCount;
            internal SOCKADDR_STORAGE   AddrList; // [ANYSIZE_ARRAY];
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HTTP_SERVICE_CONFIG_IP_LISTEN_PARAM
        {
            internal short    AddrLength;
            internal IntPtr   pAddress; // PSOCKADDR
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HTTP_SERVICE_CONFIG_SSL_KEY
        {
            internal IntPtr pIpPort; // PSOCKADDR
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HTTP_SERVICE_CONFIG_SSL_QUERY
        {
            internal HTTP_SERVICE_CONFIG_QUERY_TYPE  QueryDesc;
            internal HTTP_SERVICE_CONFIG_SSL_KEY     KeyDesc;
            internal int                             dwToken;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HTTP_SERVICE_CONFIG_URLACL_KEY
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string pUrlPrefix;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        internal struct sockaddr 
        {
            internal short sa_family;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst=14, ArraySubType=UnmanagedType.U1)]
            char[]  sa_data;
        };

        [StructLayout(LayoutKind.Sequential)]
        internal struct HTTP_SERVICE_CONFIG_URLACL_QUERY
        {
            internal HTTP_SERVICE_CONFIG_QUERY_TYPE  QueryDesc;
            internal HTTP_SERVICE_CONFIG_URLACL_KEY  KeyDesc;
            internal int                             dwToken;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HTTP_SERVICE_CONFIG_SSL_PARAM 
        {
            internal int SslHashLength;
            
            internal IntPtr pSslHash;
            
            internal Guid AppId;
            
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string pSslCertStoreName;
            
            internal ClientCertCheckMode CertCheckMode;
            
            internal int RevocationFreshnessTime;
            
            internal int RevocationUrlRetrievalTimeout;
            
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string pSslCtlIdentifier;
            
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string pSslCtlStoreName;
            
            internal SslConfigFlag Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HTTP_SERVICE_CONFIG_SSL_SET 
        {
            internal HTTP_SERVICE_CONFIG_SSL_KEY     KeyDesc;
            internal HTTP_SERVICE_CONFIG_SSL_PARAM   ParamDesc;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HTTP_SERVICE_CONFIG_URLACL_PARAM 
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string pStringSecurityDescriptor;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HTTP_SERVICE_CONFIG_URLACL_SET 
        {
            internal HTTP_SERVICE_CONFIG_URLACL_KEY     KeyDesc;
            internal HTTP_SERVICE_CONFIG_URLACL_PARAM   ParamDesc;
        }
        #endregion Structures

		#region Enumerations
		internal enum InitFlag : int
		{
			HTTP_INITIALIZE_SERVER = 1,
			HTTP_INITIALIZE_CONFIG = 2
		}

		internal enum HTTP_SERVICE_CONFIG_ID : int
		{
			HttpServiceConfigIPListenList = 0,
			HttpServiceConfigSSLCertInfo  = 1,
			HttpServiceConfigUrlAclInfo   = 2
		}

        internal enum HTTP_SERVICE_CONFIG_QUERY_TYPE : int
        {
	        HttpServiceConfigQueryExact = 0,
	        HttpServiceConfigQueryNext  = 1
        }

        internal enum Error : int
        {
	        NO_ERROR				  = 0,
	        ERROR_FILE_NOT_FOUND	  = 2,
            ERROR_INVALID_HANDLE      = 6,  // HttpAPI was not initialized.
	        ERROR_INVALID_DATA		  = 13,
	        ERROR_HANDLE_EOF		  = 38,
	        ERROR_INVALID_PARAMETER   = 87,
            ERROR_INSUFFICIENT_BUFFER = 122,
            ERROR_ALREADY_EXISTS      = 183,
            ERROR_NO_MORE_ITEMS       = 259,
	        ERROR_INVALID_DLL		  = 1154,
            ERROR_NOT_FOUND           = 1168
        }

		internal enum ClientCertCheckMode
		{
            EnableRevocation           = 0,
			NoVerifyRevocation         = 1,
			CachedRevocationOnly       = 2,
			UseRevocationFreshnessTime = 4,
			NoUsageCheck               = 0x10000,
		}

        [Flags]
        internal enum SslConfigFlag : int
        {
            None                        = 0,
            UseDSMapper                 = 1,
			NegotiateClientCertificates = 2,
			DoNotRouteToRawIsapiFilters = 4,
        }
		#endregion Enumerations

        internal static IntPtr IncIntPtr(IntPtr ptr, int count)
        {
            return (IntPtr)((int)ptr + count);
        }

        internal static IntPtr BuildSockaddr(short family, ushort port, IPAddress address)
        {
            int sockaddrSize = Marshal.SizeOf(typeof(HttpApi.sockaddr));

            IntPtr pSockaddr = Marshal.AllocHGlobal(sockaddrSize);

            HttpApi.ZeroMemory(pSockaddr, sockaddrSize);

            Marshal.WriteInt16(pSockaddr, family);

            ushort p = (ushort)IPAddress.HostToNetworkOrder((short)port);

            Marshal.WriteInt16(pSockaddr, 2, (short)p);

            byte[] addr = address.GetAddressBytes();

            IntPtr pAddr = HttpApi.IncIntPtr(pSockaddr, 4);

            Marshal.Copy(addr, 0, pAddr, addr.Length);

            return pSockaddr;
        }
    }

    internal static class ErrorCheck
    {
        internal static void VerifySuccess(IntPtr pointer, string format, params object[] args)
        {
            VerifySuccess(pointer.ToInt32() != 0, format, args);
        }
        internal static void VerifySuccess(int success, string format, params object[] args)
        {
            VerifySuccess(success != 0, format, args);
        }
        internal static void VerifySuccess(bool success, string format, params object[] args)
        {
            if (success)
                return;

            string msg = args == null || args.Length == 0 ? format : string.Format(format, args);

            int errorCode = Marshal.GetLastWin32Error();
            Exception inner = new System.ComponentModel.Win32Exception(errorCode);
            string errMessage = string.Format("{2} while {0}: LastError = {1} (0x{1:X})",
                                                msg, errorCode, inner.Message);

            throw new ApplicationException(errMessage, inner);
        }

        internal static void VerifySuccess(HttpApi.Error error, string format, params object[] args)
        {
            if (error == HttpApi.Error.NO_ERROR)
                return;

            format = string.Format("Error {0}. ", error) + format;

            VerifySuccess(false, format, args);
        }
    }
}
