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
    public class ActivateAutomaticPaymentRequest : BaseSubscriptionRequest
    {
        [DataMember]
        public string CardToken { get; set; }
        [DataMember]
        public short? PaymentType { get; set; }
        [DataMember]
        public HttpContextParameters HttpContextParameters { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceActivateAutomaticPaymentRequest : BaseRequest<ActivateAutomaticPaymentRequest, SHA1>
    {
        [DataMember]
        public ActivateAutomaticPaymentRequest ActivateAutomaticPaymentParameters { get { return Data; } set { Data = value; } }
    }
}