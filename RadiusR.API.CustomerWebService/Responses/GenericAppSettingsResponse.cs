using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses
{
    [DataContract]
    public class GenericAppSettingsResponse
    {
        [DataMember]
        public bool HasAnyTelekomDomains { get; set; }
        [DataMember]
        public bool MobilExpressIsActive { get; set; }
        [DataMember]
        public long FileMaxCount { get; set; }
        [DataMember]
        public long FileMaxSize { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceGenericAppSettingsResponse : BaseResponse<GenericAppSettingsResponse, SHA1>
    {
        public CustomerServiceGenericAppSettingsResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public GenericAppSettingsResponse GenericAppSettings { get { return Data; } set { Data = value; } }
    }
}