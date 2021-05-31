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
    public partial class CustomerServiceDomainCachesResponse : BaseResponse<bool, SHA1>
    {
        public CustomerServiceDomainCachesResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public bool HasAnyTelekomDomains { get { return Data; } set { Data = value; } }
    }
}