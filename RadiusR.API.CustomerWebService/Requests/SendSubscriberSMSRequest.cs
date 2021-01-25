using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests
{
    [DataContract]
    public class SendSubscriberSMSRequest : BaseSubscriptionRequest
    {
        [DataMember]
        public decimal? PayableAmount { get; set; }
        [DataMember]
        public IEnumerable<long> BillIds { get; set; }
        [DataMember]
        public short? SubscriptionPaidType { get; set; } // Enum
    }
    [DataContract]
    public partial class CustomerServiceSendSubscriberSMSRequest : BaseRequest<SendSubscriberSMSRequest, SHA1>
    {
        [DataMember]
        public SendSubscriberSMSRequest SendSubscriberSMS { get { return Data; } set { Data = value; } }
    }
}