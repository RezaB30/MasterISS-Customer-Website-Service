using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.PartnerResponses
{
    [DataContract]
    public class PartnerSubscriptionsResponse
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
        public NameValuePair CustomerState { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceSubscriptionsResponse : BaseResponse<PartnerSubscriptionsResponse[], SHA256>
    {
        public PartnerServiceSubscriptionsResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public PartnerSubscriptionsResponse[] PartnerSubscriptionList { get { return Data; } set { Data = value; } }
    }
}