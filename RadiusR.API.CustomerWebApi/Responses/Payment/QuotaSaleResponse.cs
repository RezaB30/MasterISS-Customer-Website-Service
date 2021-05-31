using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.Payment
{
    [DataContract]
    public partial class CustomerServiceQuotaSaleResponse : BaseResponse<bool?, SHA1>
    {
        public CustomerServiceQuotaSaleResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public bool? IsQuotaSale { get; set; }
    }
}