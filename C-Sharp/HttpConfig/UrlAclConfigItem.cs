/*
	This code is released under the terms of MIT license.
*/

using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;

namespace HttpConfig
{
    internal enum ConfigItemAction
    {
        Create, Update, Delete
    }

    internal class UrlAclConfigItem
    {
        private bool presentInHttpCfg = false;
        private bool needUpdate = true;

        private UrlAclConfigItem() 
        {
            this.Dacl = new Acl();
        }

        private UrlAclConfigItem(string url, string user)
                : this()
        {
            this.Url = url;
            this.Dacl.SetUser(user);
        }

        internal static UrlAclConfigItem LoadOrCreateConfigItem(string url, string user, ref object allUrlsRaw)
        {
            string loweredUrl = url.ToLowerInvariant();

            UrlAclConfigItem urlItem;

            Dictionary<string, UrlAclConfigItem> allUrls = null;

            if (allUrlsRaw != null)
                allUrls = (Dictionary<string, UrlAclConfigItem>)allUrlsRaw;
            else
            {
                allUrls = QueryConfig();
                allUrlsRaw = allUrls;
            }

            if (!allUrls.TryGetValue(loweredUrl, out urlItem))
            {
                urlItem = new UrlAclConfigItem(url, user);
                allUrls[loweredUrl] = urlItem;
            }else
            {
                if (!urlItem.Dacl.MatchesUser(loweredUrl))
                    urlItem.Dacl.SetUser(user);
                else
                    urlItem.needUpdate = false;
            }

            return urlItem;
        }

        internal string Url { get; set; }

        internal Acl Dacl { get; set; }

        internal string Key
        {
            get { return this.Url.ToLowerInvariant(); }
        }

        internal bool NeedUpdate
        {
            get { return this.needUpdate; }
        }

        public override string ToString()
        {
            return this.Url;
        }

        internal void ReigsterUrlItem()
        {
            this.ApplyConfig(ConfigItemAction.Create);
        }

        internal void UpdateUlrItem()
        {
            this.ApplyConfig(ConfigItemAction.Update);
        }

        internal void UnregisterUrlItem()
        {
            this.ApplyConfig(ConfigItemAction.Delete);
        }

        private void ApplyConfig(ConfigItemAction action)
        {
            IntPtr pStruct = IntPtr.Zero;

            HttpApi.HTTP_SERVICE_CONFIG_URLACL_SET setStruct = new HttpApi.HTTP_SERVICE_CONFIG_URLACL_SET();

            setStruct.KeyDesc.pUrlPrefix = this.Url;

            setStruct.ParamDesc.pStringSecurityDescriptor = this.Dacl.ToSddl();

            try
            {
                pStruct = Marshal.AllocHGlobal(Marshal.SizeOf(setStruct));

                Marshal.StructureToPtr(setStruct, pStruct, false);

                //if(this.presentInHttpCfg && 
                //    (action == ConfigItemAction.Delete || action == ConfigItemAction.Update))
                if (this.presentInHttpCfg)
                {
                    HttpApi.Error error = HttpApi.HttpDeleteServiceConfiguration(
                        IntPtr.Zero,
                        HttpApi.HTTP_SERVICE_CONFIG_ID.HttpServiceConfigUrlAclInfo,
                        pStruct,
                        Marshal.SizeOf(setStruct),
                        IntPtr.Zero);
                        
                    ErrorCheck.VerifySuccess(error, "HttpDeleteServiceConfiguration (URLACL) failed.");
                }

                if (action == ConfigItemAction.Create|| action == ConfigItemAction.Update)
                {
                    HttpApi.Error error = HttpApi.HttpSetServiceConfiguration(
                        IntPtr.Zero,
                        HttpApi.HTTP_SERVICE_CONFIG_ID.HttpServiceConfigUrlAclInfo,
                        pStruct,
                        Marshal.SizeOf(setStruct),
                        IntPtr.Zero);

                    if (error != HttpApi.Error.ERROR_ALREADY_EXISTS)
                        ErrorCheck.VerifySuccess(error, "HttpSetServiceConfiguration (URLACL) failed.");
                }
            }
            finally
            {
                if(pStruct != IntPtr.Zero)
                {
                    Marshal.DestroyStructure(pStruct, typeof(HttpApi.HTTP_SERVICE_CONFIG_URLACL_SET));
                    Marshal.FreeHGlobal(pStruct);
                }
            }
        }

        internal static Dictionary<string, UrlAclConfigItem> QueryConfig()
        {
            Dictionary<string, UrlAclConfigItem> items = new Dictionary<string, UrlAclConfigItem>();

            HttpApi.HTTP_SERVICE_CONFIG_URLACL_QUERY query = new HttpApi.HTTP_SERVICE_CONFIG_URLACL_QUERY();

            query.QueryDesc = HttpApi.HTTP_SERVICE_CONFIG_QUERY_TYPE.HttpServiceConfigQueryNext;
            
            HttpApi.Error error = HttpApi.Error.NO_ERROR;

            IntPtr pInput = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(HttpApi.HTTP_SERVICE_CONFIG_URLACL_QUERY)));

            try
            {
                for(query.dwToken = 0; ; query.dwToken++)
                {
                    Marshal.StructureToPtr(query, pInput, false);

                    int requiredBufferLength = 0;
                    
                    error = QueryServiceConfig(pInput, IntPtr.Zero, 0, out requiredBufferLength);

                    if(error == HttpApi.Error.ERROR_NO_MORE_ITEMS)
                        break;
                    else if(error != HttpApi.Error.ERROR_INSUFFICIENT_BUFFER)
                        ErrorCheck.VerifySuccess(error, "HttpQueryServiceConfiguration (URLACL) failed.");

                    IntPtr pOutput = Marshal.AllocHGlobal(requiredBufferLength);

                    try
                    {
                        HttpApi.ZeroMemory(pOutput, requiredBufferLength);

                        error = QueryServiceConfig(pInput, pOutput, requiredBufferLength, out requiredBufferLength);

                        ErrorCheck.VerifySuccess(error, "HttpQueryServiceConfiguration (URLACL) failed.");
                        
                        UrlAclConfigItem item = Deserialize(pOutput);

                        items.Add(item.Key, item);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pOutput);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pInput);
            }            

            return items;
        }

        private static UrlAclConfigItem Deserialize(IntPtr pUrlAclConfigSetStruct)
        {
            UrlAclConfigItem item = new UrlAclConfigItem();

            HttpApi.HTTP_SERVICE_CONFIG_URLACL_SET aclStruct =
                (HttpApi.HTTP_SERVICE_CONFIG_URLACL_SET)Marshal.PtrToStructure(pUrlAclConfigSetStruct, typeof(HttpApi.HTTP_SERVICE_CONFIG_URLACL_SET));

            item.Url = aclStruct.KeyDesc.pUrlPrefix;

            item.Dacl = Acl.FromSddl(aclStruct.ParamDesc.pStringSecurityDescriptor);

            item.presentInHttpCfg = true;

            return item;
        }

        private static HttpApi.Error QueryServiceConfig(IntPtr pInput, IntPtr pOutput, int outputLength, out int requiredBufferLength)
        {
            return HttpApi.HttpQueryServiceConfiguration(
                IntPtr.Zero,
                HttpApi.HTTP_SERVICE_CONFIG_ID.HttpServiceConfigUrlAclInfo,
                pInput,
                Marshal.SizeOf(typeof(HttpApi.HTTP_SERVICE_CONFIG_URLACL_QUERY)),
                pOutput,
                outputLength,
                out requiredBufferLength,
                IntPtr.Zero);
        }
    }
}
