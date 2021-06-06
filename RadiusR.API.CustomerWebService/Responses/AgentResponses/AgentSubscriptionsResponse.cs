using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.AgentResponses
{
    [DataContract]
    public class AgentSubscriptionsResponse : PaginationResponse
    {
        [DataMember]
        public AgentSubscriptions[] AgentSubscriptionList { get; set; }
        [DataContract]
        public class AgentSubscriptions
        {
            [DataMember]
            public long ID { get; set; }
            [DataMember]
            public string DisplayName { get; set; }
            [DataMember]
            public string SubscriberNo { get; set; }
            [DataMember]
            public string MembershipDate { get; set; }
            [DataMember]
            public string ExpirationDate { get; set; }
            [DataMember]
            public NameValuePair CustomerState { get; set; }
        }

    }
    [DataContract]
    public partial class AgentServiceSubscriptionsResponse : BaseResponse<AgentSubscriptionsResponse, SHA256>
    {
        public AgentServiceSubscriptionsResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public AgentSubscriptionsResponse AgentSubscriptionList { get { return Data; } set { Data = value; } }
    }
    //[DataContract]
    //public partial class PartnerServiceSubscriptionStateResponse : BaseResponse<AgentSubscriptionsResponse, SHA256>
    //{
    //    public PartnerServiceSubscriptionStateResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
    //    [DataMember]
    //    public AgentSubscriptionsResponse AgentSubscriptionState { get { return Data; } set { Data = value; } }
    //}
}