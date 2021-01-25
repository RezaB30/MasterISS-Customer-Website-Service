using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.Payment
{
    [DataContract]
    public class PaymentSystemLogRequest : BaseSubscriptionRequest
    {
        [DataMember]
        public long[] BillIds { get; set; }
        [DataMember]
        public int? UserId { get; set; }
        [DataMember]
        public string SubscriberNo { get; set; }
        [DataMember]
        public int? PaymentType { get; set; }
    }
    [DataContract]
    public partial class CustomerServicePaymentSystemLogRequest : BaseRequest<PaymentSystemLogRequest, SHA1>
    {
        [DataMember]
        public PaymentSystemLogRequest PaymentSystemLogParameters { get { return Data; } set { Data = value; } }
    }
}