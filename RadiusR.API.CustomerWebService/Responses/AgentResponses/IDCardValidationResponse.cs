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
    public class AgentServiceIDCardValidationResponse : BaseResponse<bool, SHA256>
    {
        public AgentServiceIDCardValidationResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public bool IDCardValidationResponse { get { return Data; } set { Data = value; } }
    }
}