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
    public class AgentSubscriptionsRequest : RequestBase
    {
        [DataMember]
        public PaginationRequest Pagination { get; set; }
        [DataMember]
        public SearchFilter SearchFilter { get; set; }
    }
    [DataContract]
    public partial class AgentServiceSubscriptionsRequest : BaseRequest<AgentSubscriptionsRequest, SHA256>
    {
        [DataMember]
        public AgentSubscriptionsRequest SubscriptionsRequestParameters { get { return Data; } set { Data = value; } }
    }
}