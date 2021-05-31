using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.AgentRequests
{
    [DataContract]
    public class SubscriptionCredentialSMSRequest : RequestBase
    {
        [DataMember]
        public string SubscriberNo { get; set; }
    }
    [DataContract]
    public partial class AgentServiceCredentialSMSRequest : BaseRequest<SubscriptionCredentialSMSRequest, SHA256>
    {
        [DataMember]
        public SubscriptionCredentialSMSRequest CredentialSMSParameter { get { return Data; } set { Data = value; } }
    }
}