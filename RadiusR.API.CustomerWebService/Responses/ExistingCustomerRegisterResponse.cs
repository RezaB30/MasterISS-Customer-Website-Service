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
    public partial class CustomerServiceExistingCustomerRegisterResponse : BaseResponse<Dictionary<string, string>, SHA1>
    {
        public CustomerServiceExistingCustomerRegisterResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public Dictionary<string, string> KeyValuePairs { get; set; }
    }
}