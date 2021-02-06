using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.PartnerRequests
{
    [DataContract]
    public class AuthenticationRequest : RequestBase
    {
        [DataMember]
        public string PartnerUsername { get; set; }
        [DataMember]
        public string PartnerPasswordHash { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceAuthenticationRequest : BaseRequest<AuthenticationRequest, SHA256>
    {
        [DataMember]
        public AuthenticationRequest AuthenticationParameters { get { return Data; } set { Data = value; } }
    }
}