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
    public class ServiceOperatorsRequest : RequestBase
    {
        [DataMember]
        public long SubscriptionId { get; set; }
    }
    [DataContract]
    public partial class AgentServiceServiceOperatorsRequest : BaseRequest<ServiceOperatorsRequest, SHA256>
    {
        [DataMember]
        public ServiceOperatorsRequest ServiceOperatorsParameters { get { return Data; } set { Data = value; } }
    }
}