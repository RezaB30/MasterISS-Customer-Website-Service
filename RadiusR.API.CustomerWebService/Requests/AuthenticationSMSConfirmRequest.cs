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
    public class AuthenticationSMSConfirmRequest
    {
        [DataMember]
        public string CustomerCode { get; set; }
        [DataMember]
        public string SMSPassword { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceAuthenticationSMSConfirmRequest : BaseRequest<AuthenticationSMSConfirmRequest, SHA1>
    {
        [DataMember]
        public AuthenticationSMSConfirmRequest AuthenticationSMSConfirmParameters { get { return Data; } set { Data = value; } }
    }
}