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
    public class SubscriptionBasicInformationResponse
    {
        [DataMember]
        public string ValidDisplayName { get; set; }
        [DataMember]
        public long ID { get; set; }
        [DataMember]
        public string SubscriberNo { get; set; }
        [DataMember]
        public IEnumerable<string> RelatedCustomers { get; set; }
        [DataMember]
        public bool HasBilling { get; set; }
        [DataMember]
        public long CustomerID { get; set; }
        [DataMember]
        public bool IsCancelled { get; set; }
        [DataMember]
        public Service SubscriptionService { get; set; }
        [DataContract]
        public class Service
        {
            [DataMember]
            public decimal? Price { get; set; }
        }
    }
    [DataContract]
    public partial class CustomerServiceSubscriptionBasicInformationResponse : BaseResponse<SubscriptionBasicInformationResponse, SHA1>
    {
        public CustomerServiceSubscriptionBasicInformationResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public SubscriptionBasicInformationResponse SubscriptionBasicInformationResponse { get { return Data; } set { Data = value; } }
    }
}