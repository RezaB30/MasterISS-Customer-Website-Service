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
    public class MobilexpressPayBillRequest : BaseSubscriptionRequest
    {
        [DataMember]
        public decimal? PayableAmount { get; set; }
        [DataMember]
        public string Token { get; set; }
        [DataMember]
        public HttpContextParameters HttpContextParameters { get; set; }
        [DataMember]
        public long? BillId { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceMobilexpressPayBillRequest : BaseRequest<MobilexpressPayBillRequest, SHA1>
    {
        [DataMember]
        public MobilexpressPayBillRequest MobilexpressPayBillParameters { get { return Data; } set { Data = value; } }
    }
}