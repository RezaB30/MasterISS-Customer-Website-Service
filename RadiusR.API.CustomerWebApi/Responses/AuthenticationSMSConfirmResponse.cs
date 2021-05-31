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
    public class AuthenticationSMSConfirmResponse
    {
        [DataMember]
        public string ValidDisplayName { get; set; }
        [DataMember]
        public long ID { get; set; }
        [DataMember]
        public string SubscriberNo { get; set; }
        [DataMember]
        public IEnumerable<string> RelatedCustomers { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceAuthenticationSMSConfirmResponse : BaseResponse<AuthenticationSMSConfirmResponse, SHA1>
    {
        public CustomerServiceAuthenticationSMSConfirmResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public AuthenticationSMSConfirmResponse AuthenticationSMSConfirmResponse { get { return Data; } set { Data = value; } }
    }
}