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
    public class AgentClientFormsRequest : RequestBase
    {
        [DataMember]
        public long? SubscriptionId { get; set; }
        [DataMember]
        public int? FormType { get; set; }
    }
    [DataContract]
    public partial class AgentServiceClientFormsRequest : BaseRequest<AgentClientFormsRequest, SHA256>
    {
        [DataMember]
        public AgentClientFormsRequest ClientFormsParameters { get { return Data; } set { Data = value; } }
    }
}