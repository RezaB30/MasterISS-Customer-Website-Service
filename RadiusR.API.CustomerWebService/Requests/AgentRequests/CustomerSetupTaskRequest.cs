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
    public class CustomerSetupTaskRequest : RequestBase
    {
        [DataMember]
        public long SubscriptionId { get; set; }
    }
    [DataContract]
    public partial class AgentServiceCustomerSetupTaskRequest : BaseRequest<CustomerSetupTaskRequest, SHA256>
    {
        [DataMember]
        public CustomerSetupTaskRequest CustomerTaskParameters { get { return Data; } set { Data = value; } }
    }
}