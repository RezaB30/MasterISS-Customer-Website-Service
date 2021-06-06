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
    public partial class AgentServiceServiceOperatorsResponse : BaseResponse<NameValuePair[], SHA256>
    {
        public AgentServiceServiceOperatorsResponse(string passwordHash, BaseRequest<SHA256> request) : base(passwordHash, request) { }
        [DataMember]
        public NameValuePair[] ServiceOperators { get { return Data; } set { Data = value; } }
    }
}