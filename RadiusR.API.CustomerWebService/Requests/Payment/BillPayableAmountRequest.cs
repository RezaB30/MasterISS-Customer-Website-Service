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
    public class BillPayableAmountRequest : BaseSubscriptionRequest
    {
        [DataMember]
        public long? BillId { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceBillPayableAmountRequest : BaseRequest<BillPayableAmountRequest, SHA1>
    {
        [DataMember]
        public BillPayableAmountRequest BillPayableAmountParameters { get { return Data; } set { Data = value; } }
    }
}