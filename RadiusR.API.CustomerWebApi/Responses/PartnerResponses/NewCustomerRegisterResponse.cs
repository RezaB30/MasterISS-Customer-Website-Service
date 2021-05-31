using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.PartnerResponses
{
    [DataContract]
    public partial class PartnerServiceNewCustomerRegisterResponse : BaseResponse<Dictionary<string, string>, SHA256>
    {
        public PartnerServiceNewCustomerRegisterResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public Dictionary<string, string> NewCustomerRegisterResponse
        {
            get
            {
                return Data;
            }
            set
            {
                Data = value;
            }
        }
    }
}