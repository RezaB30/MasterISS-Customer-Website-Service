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
    public partial class CustomerServiceSubscriberListResponse : BaseResponse<SubscriptionKeyValue[], SHA1>
    {
        public CustomerServiceSubscriberListResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public SubscriptionKeyValue[] SubscriptionList { get { return Data; } set { Data = value; } }
    }
    [DataContract]
    public class SubscriptionKeyValue
    {
        [DataMember]
        public long SubscriptionId { get; set; }
        [DataMember]
        public int State { get; set; }
        [DataMember]
        public string StateName { get; set; }
    }
}