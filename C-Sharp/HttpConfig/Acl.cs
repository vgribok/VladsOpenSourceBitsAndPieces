/*
	This code is released under the terms of MIT license.
*/
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace HttpConfig
{
    internal enum UrlPermission
    {
        All,
        Registration,
        Delegation,
        Other,
    }

    internal class Acl : ICloneable
    {
        private List<Ace> _acl = new List<Ace>();

        internal Acl() { }

        internal Acl(Ace initialAce)
        {
            this._acl.Add(initialAce);
        }

        internal Acl(string user)
            : this(new Ace(user))
        {
        }

        internal List<Ace> Aces
        {
            get { return _acl; }
        }

        object ICloneable.Clone()
        {
            Acl newAcl = new Acl();

            foreach(Ace entry in _acl)
                newAcl._acl.Add(new Ace(entry.User, entry.AccountNameMapped, entry.Permission, entry.OtherPerm));

            return newAcl;
        }

        internal string FriendlyString
        {
            get
            {
                StringBuilder friendlyString = new StringBuilder();

                foreach(Ace entry in _acl)
                {
                    friendlyString.Append("(");
                    friendlyString.Append(entry.User);
                    friendlyString.Append(";");
                    friendlyString.Append(entry.Permission.ToString());
                    friendlyString.Append(")");
                }

                return friendlyString.ToString();
            }
        }

        internal static Acl FromSddl(string sddl)
        {
            Acl newAcl = new Acl();

            string[] aceStrings = sddl.Split(new char[] { '(', ')' });

            // it's split on ( and ), so we have blanks every other item
            for(int i = 1; i < aceStrings.Length; i++)
            {
                if((i % 2) > 0)
                    newAcl._acl.Add(Ace.FromSddl(aceStrings[i]));
            }

            return newAcl;
        }

        internal string ToSddl()
        {
            StringBuilder sddl = new StringBuilder();

            sddl.Append("D:");

            foreach(Ace ace in _acl)
                ace.AddSddl(sddl);

            return sddl.ToString();
        }

        internal bool MatchesUser(string userName)
        {
            if (this._acl.Count != 1)
                return false;

            return this._acl[0].User.ToLowerInvariant() == userName.ToLowerInvariant();
        }

        internal void SetUser(string user)
        {
            this._acl.Clear();
            this.Aces.Add(new Ace(user));
        }
    }

    internal class Ace
    {
        private string _user;
        private UrlPermission _permission;
        private string _otherPerm;
        private bool _accountNameMapped;

        private Ace() { }

        internal Ace(string user) 
            : this(user, true, UrlPermission.Registration, null)
        {
        }

        internal Ace(string user, bool accountNameMapped, UrlPermission permission, string otherPerm)
        {
            _user              = user;
            _accountNameMapped = accountNameMapped;
            _permission        = permission;
            _otherPerm         = otherPerm;
        }

        internal string User
        {
            get { return _user; }
            set { _user = value; }
        }

        internal bool AccountNameMapped
        {
            get { return _accountNameMapped; }
            set { _accountNameMapped = value; }
        }

        internal UrlPermission Permission
        {
            get { return _permission; }
            set { _permission = value; }
        }

        internal string OtherPerm
        {
            get { return _otherPerm; }
            set { _otherPerm = value; }
        }

        internal void AddSddl(StringBuilder sddl)
        {
            sddl.Append("(A;;");

            switch(_permission)
            {
                case UrlPermission.All:
                    sddl.Append("GA");
                    break;

                case UrlPermission.Registration:
                    sddl.Append("GX");
                    break;

                case UrlPermission.Delegation:
                    sddl.Append("GW");
                    break;

                case UrlPermission.Other:
                    sddl.Append(_otherPerm);
                    break;
            }

            sddl.Append(";;;");

            sddl.Append(_accountNameMapped ? EncodeSid() : _user);

            sddl.Append(")");
        }

        internal static Ace FromSddl(string sddl)
        {
            string[] tokens = sddl.Split(';');

            if(tokens.Length != 6)
                throw new ArgumentException("Invalid SDDL string.  Too many or too few tokens.", "sddl");

            string permString = tokens[2];

            string stringSid = tokens[5];

            Ace ace = new Ace();

            switch(permString)
            {
                case "GA":
                    ace._permission = UrlPermission.All;
                    break;

                case "GX":
                    ace._permission = UrlPermission.Registration;
                    break;

                case "GW":
                    ace._permission = UrlPermission.Delegation;
                    break;

                default:
                    ace._permission = UrlPermission.Other;
                    ace._otherPerm = permString;
                    break;
            }

            ace._accountNameMapped = DecodeSid(stringSid, out ace._user);

            return ace;
        }

        private static bool DecodeSid(string stringSid, out string accountName)
        {
            IntPtr pSid     = IntPtr.Zero;
            IntPtr pAccount = IntPtr.Zero;
            IntPtr pDomain  = IntPtr.Zero;

            try
            {
                accountName = stringSid;

                if(!SecurityApi.ConvertStringSidToSid(stringSid, out pSid))
                    throw new Exception("ConvertStringSidToSid failed.  Error = " + Marshal.GetLastWin32Error().ToString());

                int accountLength = 0;
                int domainLength  = 0;

                SecurityApi.SidNameUse use;

                int error = (int)SecurityApi.Error.SUCCESS;

                Thread accountLookupThread = new Thread(new ThreadStart(delegate()
                    {
                        if (!SecurityApi.LookupAccountSid(null, pSid, pAccount, ref accountLength, pDomain, ref domainLength, out use))
                        {
                            error = Marshal.GetLastWin32Error();
                        }
                    }
                ));

                const int secondsToWaitForDnsLookup = 10;

                accountLookupThread.Start();
                if (!accountLookupThread.Join(secondsToWaitForDnsLookup * 1000))
                {
                    accountLookupThread.Abort();
                    accountLookupThread = null;

                    string errorMsg = string.Format("Timed out while trying to lookup SID of \"{0}\" account. There might be a problem with the DNS.", accountName);
                    Trace.TraceWarning(errorMsg);
                    //throw new Exception(errorMsg);
                    return false;
                }

                if (error != (int)SecurityApi.Error.SUCCESS)
                {
                    if(error != (int)SecurityApi.Error.ERROR_INSUFFICIENT_BUFFER)
                    {
                        if((error == (int)SecurityApi.Error.ERROR_NONE_MAPPED) || (error == (int)SecurityApi.Error.ERROR_TRUSTED_RELATIONSHIP_FAILURE))
                            return false;
                        else
                            throw new Exception("LookupAccountSid failed.  Error = " + Marshal.GetLastWin32Error().ToString());
                    }
                }

                error = (int)SecurityApi.Error.SUCCESS;

                pAccount = Marshal.AllocHGlobal(accountLength * 2); // 2-byte unicode...we're using the "W" variety of the funcion

                pDomain = Marshal.AllocHGlobal(domainLength * 2); // 2-byte unicode...we're using the "W" variety of the funcion

                accountLookupThread = new Thread(new ThreadStart(delegate()
                    {
                        if(!SecurityApi.LookupAccountSid(null, pSid, pAccount, ref accountLength, pDomain, ref domainLength, out use))
                        {
                            error = Marshal.GetLastWin32Error();
                        }
                    }
                ));
                accountLookupThread.Start();
                if (!accountLookupThread.Join(secondsToWaitForDnsLookup * 1000))
                {
                    accountLookupThread.Abort();
                    accountLookupThread = null;

                    string errorMsg = string.Format("Timed out while trying to lookup SID of \"{0}\" account. There might be a problem with the DNS.", accountName);
                    Trace.TraceWarning(errorMsg);
                    //throw new Exception(errorMsg);
                    return false;
                }

                if (error != (int)SecurityApi.Error.SUCCESS)
                {
                    if((error == (int)SecurityApi.Error.ERROR_NONE_MAPPED) || (error == (int)SecurityApi.Error.ERROR_TRUSTED_RELATIONSHIP_FAILURE))
                        return false;
                    else
                        throw new Exception("LookupAccountSid failed.  Error = " + error.ToString());
                }

                accountName = Marshal.PtrToStringUni(pDomain) + "\\" + Marshal.PtrToStringUni(pAccount);

                return true;
            }
            finally
            {
                if(pSid != IntPtr.Zero)
                    SecurityApi.LocalFree(pSid);

                if(pAccount != IntPtr.Zero)
                    Marshal.FreeHGlobal(pAccount);

                if(pDomain != IntPtr.Zero)
                    Marshal.FreeHGlobal(pDomain);
            }
        }

        private string EncodeSid()
        {
            IntPtr pSid       = IntPtr.Zero;
            IntPtr pStringSid = IntPtr.Zero;
            IntPtr pDomain    = IntPtr.Zero;

            try
            {
                int sidLength = 0;
                int domainLength = 0;

                SecurityApi.SidNameUse use;

                if (!SecurityApi.LookupAccountName(null, _user, pSid, ref sidLength, pDomain, ref domainLength, out use))
                {
                    int error = Marshal.GetLastWin32Error();

                    if (error != (int)SecurityApi.Error.ERROR_INSUFFICIENT_BUFFER)
                        throw new Exception(string.Format("LookupAccountName buffer length detection failed for user \"{0}\". Win32 Error: {1}.", this._user, Marshal.GetLastWin32Error()));
                }

                pSid = Marshal.AllocHGlobal(sidLength);

                pDomain = Marshal.AllocHGlobal(domainLength * 2); // 2-byte unicode...we're using the "W" variety of the funcion

                if (!SecurityApi.LookupAccountName(null, _user, pSid, ref sidLength, pDomain, ref domainLength, out use))
                    throw new Exception(string.Format("LookupAccountName failed for user \"{0}\". Win32 Error: {1}.", this._user, Marshal.GetLastWin32Error()));

                if (!SecurityApi.ConvertSidToStringSid(pSid, out pStringSid))
                    throw new Exception(string.Format("ConvertSidToStringSid failed for user \"{0}\". Win32 Error: {1}.", this._user, Marshal.GetLastWin32Error()));

                return Marshal.PtrToStringUni(pStringSid);
            }
            catch (Exception ex)
            {
                string errorMsg = string.Format("Failed to encode SID for account \"{0}\". See inner exception for more details.", this._user);
                throw new Exception(errorMsg, ex);
            }
            finally
            {
                if(pSid != IntPtr.Zero)
                    SecurityApi.LocalFree(pSid);

                if(pStringSid != IntPtr.Zero)
                    SecurityApi.LocalFree(pStringSid);

                if(pDomain != IntPtr.Zero)
                    Marshal.FreeHGlobal(pDomain);
            }
        }
    }
}
