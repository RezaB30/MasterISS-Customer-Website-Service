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
    public class PartnerSubscriptionStateRequest : RequestBase
    {
        [DataMember]
        public string SubscriberNo { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceSubscriptionStateRequest : BaseRequest<PartnerSubscriptionStateRequest, SHA256>
    {
        [DataMember]
        public PartnerSubscriptionStateRequest SubscriptionStateParameters { get { return Data; } set { Data = value; } }
    }
}