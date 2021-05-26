using NLog;
using RezaB.API.WebService.NLogExtentions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Caching;
using System.Web;

namespace RadiusR.API.CustomerWebService
{
    public class ServiceSettings
    {
        WebServiceLogger Errorslogger = new WebServiceLogger("Errors");
        public string GetUserPassword(string username)
        {
            if (username != RadiusR.DB.CustomerWebsiteSettings.WebsiteServicesUsername)
            {
                Errorslogger.LogException(username, new Exception("username is not found"));
                // log wrong username
                return "";
            }
            var password = RadiusR.DB.CustomerWebsiteSettings.WebsiteServicesPassword;

            return password;
        }
        public string GetPartnerUserPassword(string username)
        {
            if (username != RadiusR.DB.CustomerWebsiteSettings.WebsiteServicesUsername)
            {
                Errorslogger.LogException(username, new Exception("username is not found"));
                // log wrong username
                return "";
            }
            var password = RadiusR.DB.CustomerWebsiteSettings.WebsiteServicesPassword;

            return password;
        }
        public string GetAgentUserPassword(string username)
        {
            if (username != RadiusR.DB.CustomerWebsiteSettings.WebsiteServicesUsername)
            {
                Errorslogger.LogException(username, new Exception("username is not found"));
                // log wrong username
                return "";
            }
            var password = RadiusR.DB.CustomerWebsiteSettings.WebsiteServicesPassword;

            return password;
        }
        //public TimeSpan Duration()
        //{
        //    //add CacheDuration
        //    return Properties.Settings.Default.CacheDuration;
        //}
    }
    //public static class CacheManager
    //{
    //    private static MemoryCache _cache = MemoryCache.Default;
    //    private const string _namePrefix = "CacheKeys_";

    //    public static string GenerateKey(string username, string subscriptionId, string cacheCode, CacheTypes cacheType, TimeSpan CacheDuration)
    //    {
    //        var key = GetKey(username, subscriptionId, cacheType);
    //        if (key != null)
    //        {
    //            _cache.Remove(_namePrefix + username + subscriptionId + cacheType.ToString());
    //        }
    //        key = cacheCode; //Guid.NewGuid().ToString();
    //        _cache.Set(_namePrefix + username + subscriptionId + cacheType.ToString(), key, GetPolicy(CacheDuration));
    //        return key;
    //    }

    //    public static string GetKey(string username, string subscriptionId, CacheTypes cacheType)
    //    {
    //        var key = _cache.Get(_namePrefix + username + subscriptionId + cacheType.ToString()) as string;
    //        return key;
    //    }

    //    private static CacheItemPolicy GetPolicy(TimeSpan _cacheDuration)
    //    {
    //        return new CacheItemPolicy()
    //        {
    //            AbsoluteExpiration = DateTimeOffset.Now.Add(_cacheDuration),
    //        };
    //    }
    //}
    //public enum CacheTypes
    //{
    //    AddCardSMSValidation = 1,
    //    RemoveCardSMSValidation = 2
    //}
}