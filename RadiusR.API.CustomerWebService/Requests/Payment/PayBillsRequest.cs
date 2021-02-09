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
    public class PayBillsRequest : BaseSubscriptionRequest
    {
        [DataMember]
        public long[] BillIds { get; set; }
        [DataMember]
        public short? SubscriptionPaidType { get; set; } // Enum
        [DataMember]
        public int? PaymentType { get; set; }
        [DataMember]
        public int? AccountantType { get; set; }
    }
    [DataContract]
    public partial class CustomerServicePayBillsRequest : BaseRequest<PayBillsRequest, SHA1>
    {
        [DataMember]
        public PayBillsRequest PayBillsParameters
        {
            get { return Data; }
            set { Data = value; }
        }

    }
}