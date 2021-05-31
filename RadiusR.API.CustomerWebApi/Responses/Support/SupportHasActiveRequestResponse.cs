using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.Support
{
    [DataContract]
    public partial class CustomerServiceHasActiveRequestResponse : BaseResponse<bool?, SHA1>
    {
        public CustomerServiceHasActiveRequestResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public bool? HasActiveRequest { get { return Data; } set { Data = value; } }
    }
}