using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests
{
    [DataContract]
    public class AuthenticationWithPasswordRequest
    {
        [DataMember]
        public string CustomerCode { get; set; }
        [DataMember]
        public string Password { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceAuthenticationWithPasswordRequest : BaseRequest<AuthenticationWithPasswordRequest, SHA1>
    {
        [DataMember]
        public AuthenticationWithPasswordRequest AuthenticationWithPasswordParameters { get { return Data; } set { Data = value; } }
    }
}