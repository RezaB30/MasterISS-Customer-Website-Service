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
    public partial class AgentServiceCredentialSMSResponse : BaseResponse<bool, SHA256>
    {
        public AgentServiceCredentialSMSResponse(string passwordHash,BaseRequest<SHA256> request) : base(passwordHash, request) { }
        [DataMember]
        public bool CredentialSMSResponse { get { return Data; } set { Data = value; } }
    }
}