using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.AgentResponses
{
    [DataContract]
    public class AuthenticationResponse
    {
        [DataMember]
        public string PhoneNo { get; set; }
        [DataMember]
        public bool IsAuthenticated { get; set; }

        [DataMember]
        public int AgentId { get; set; }

        [DataMember]
        public string DisplayName { get; set; }

        [DataMember]
        public string SetupServiceUser { get; set; }

        [DataMember]
        public string SetupServiceHash { get; set; }
    }
    [DataContract]
    public partial class AgentServiceAuthenticationResponse : BaseResponse<AuthenticationResponse, SHA256>
    {
        public AgentServiceAuthenticationResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public AuthenticationResponse AuthenticationResponse { get { return Data; } set { Data = value; } }
    }
}