/*
	Copyright (C) Vlad Hrybok 2010-2016
	This code is released under the terms of MIT license.
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Principal;
using System.Net;
using System.Runtime.InteropServices;

namespace Vlad.Framework
{
    using HttpConfig;
    //using Vlad.Configuration;

    /// <summary>
    /// User contexts for web app host processes.
    /// </summary>
    [ComVisible(true)]
    [Guid("84C096F7-D045-4C60-8D09-308F8B4F429E")]
    public enum ProcessIdentity : int
    {
        /// <summary>
        /// "NETWORK SERVICE" user - a low-privilege user account
        /// that should be used in most situations to achieve better
        /// security. This account does not have access to many 
        /// restricted folders on the file system. Use ServicedComponent
        /// of Server type to run components in a different user context.
        /// Alternatively, use SystemUtilites.AclFolder() methods to grant
        /// Network Service user access to small subset of your app folders.
        /// App_Data folder will be ACLed to allow full access to Network Service user.
        /// </summary>
        NetworkService = 0, 
        
        /// <summary>
        /// "LOCAL SYSTEM", a.k.a. "NT AUTHORITY\SYSTEM" - a high privilege
        /// user account that should not be used for hosting Internet-facing
        /// applications, as well as for hosting applications in other
        /// high threat environments. This account has access to most files 
        /// and folders on disk and to other resources.
        /// </summary>
        LocalSystem
    }

    public static class SecurityHelper
    {
        private static readonly string[] standardUserNames =
        {
            GetWellKnownWindowsAccountNameLocalized(WellKnownSidType.NetworkServiceSid),
            GetWellKnownWindowsAccountNameLocalized(WellKnownSidType.LocalSystemSid)
        };

        public static string GetWellKnownWindowsAccountNameLocalized(WellKnownSidType accountSidType)
        {
            SecurityIdentifier sid = new SecurityIdentifier(accountSidType, null);
            return GetLocalizedWindowsAccountNameBySid(sid);
        }

        public static string GetLocalizedWindowsAccountNameBySid(SecurityIdentifier sid)
        {
            IdentityReference idRef = sid.Translate(typeof(NTAccount));
            string fqun = idRef.ToString();
            return fqun;
        }

        public static string UserIdToUserName(ProcessIdentity userContext)
        {
            return standardUserNames[(int)userContext];
        }

        #region URL ACLing


        public static void ReAclListenUrls(ProcessIdentity userContext, string[] urlsToDeAcl, string[] urlsToAcl)
        {
            string userName = SecurityHelper.UserIdToUserName(userContext);

            ReAclListenUrls(userName, urlsToDeAcl, urlsToAcl);
        }

        public static void ReAclListenUrls(string userName, string[] urlsToDeAcl, string[] urlsToAcl)
        {
            using (HttpConfig.HttpApi httpApi = new HttpConfig.HttpApi())
            {
                ReAclListenUrlsInternal(userName, urlsToDeAcl, urlsToAcl);
            }
        }

        public static void AclListenUrls(ProcessIdentity userContext, params string[] urlsToAcl)
        {
            string userName = SecurityHelper.UserIdToUserName(userContext);

            AclListenUrls(userName, urlsToAcl);
        }

        public static void AclListenUrls(string userName, params string[] urlsToAcl)
        {
            using (HttpConfig.HttpApi httpApi = new HttpConfig.HttpApi())
            {
                ReAclListenUrlsInternal(userName, null, urlsToAcl);
            }
        }

        public static void DeAclListenUrls(ProcessIdentity userContext, params string[] urlsToDeAcl)
        {
            string userName = SecurityHelper.UserIdToUserName(userContext);

            DeAclListenUrls(userName, urlsToDeAcl);
        }

        public static void DeAclListenUrls(string userName, params string[] urlsToDeAcl)
        {
            using (HttpConfig.HttpApi httpApi = new HttpConfig.HttpApi())
            {
                ReAclListenUrlsInternal(userName, urlsToDeAcl, null);
            }
        }

        private static void ReAclListenUrlsInternal(string userName, string[] urlsToDeAcl, string[] urlsToAcl)
        {
            object state = null;

            if (urlsToDeAcl != null)
                foreach (string url in urlsToDeAcl)
                {
                    UrlAclConfigItem aclConfigItem = UrlAclConfigItem.LoadOrCreateConfigItem(url, userName, ref state);
                    aclConfigItem.UnregisterUrlItem();
                }

            if (urlsToAcl != null)
                foreach (string url in urlsToAcl)
                {
                    UrlAclConfigItem aclConfigItem = UrlAclConfigItem.LoadOrCreateConfigItem(url, userName, ref state);
                    if (aclConfigItem.NeedUpdate)
                        aclConfigItem.ReigsterUrlItem();
                }
        }

        #endregion URL ACLing
    }
}
