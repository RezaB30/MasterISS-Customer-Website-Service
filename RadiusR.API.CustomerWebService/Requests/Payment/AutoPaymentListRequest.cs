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
    public class AutoPaymentListRequest : BaseSubscriptionRequest
    {
        [DataMember]
        public IEnumerable<Responses.Payment.RegisteredCardsResponse> CardList { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceAutoPaymentListRequest : BaseRequest<AutoPaymentListRequest, SHA1>
    {
        [DataMember]
        public AutoPaymentListRequest AutoPaymentListParameters { get { return Data; } set { Data = value; } }
    }
}