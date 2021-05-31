using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.AgentRequests
{
    [DataContract]
    public class PaymentRequest : RequestBase
    {
        [DataMember]
        public long[] BillIDs { get; set; }
    }
    [DataContract]
    public partial class AgentServicePaymentRequest : BaseRequest<PaymentRequest, SHA256>
    {
        [DataMember]
        public PaymentRequest PaymentRequest { get { return Data; } set { Data = value; } }
    }
}