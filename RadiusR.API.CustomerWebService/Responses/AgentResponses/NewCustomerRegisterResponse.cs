using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.AgentResponses
{
    [DataContract]
    public partial class AgentServiceNewCustomerRegisterResponse : BaseResponse<Dictionary<string, string>, SHA256>
    {
        public AgentServiceNewCustomerRegisterResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
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