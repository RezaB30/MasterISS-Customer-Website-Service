using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.Payment
{
    [DataContract]
    public class AutoPaymentListResponse
    {
        [DataMember]
        public long SubscriberID { get; set; }
        [DataMember]
        public string SubscriberNo { get; set; }
        [DataMember]
        public Card Cards { get; set; }
        [DataContract]
        public class Card
        {
            [DataMember]
            public string MaskedCardNo { get; set; }
            [DataMember]
            public string Token { get; set; }
        }
    }
    [DataContract]
    public partial class CustomerServiceAutoPaymentListResponse : BaseResponse<IEnumerable<AutoPaymentListResponse>, SHA1>
    {
        public CustomerServiceAutoPaymentListResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public IEnumerable<AutoPaymentListResponse> AutoPaymentListResult { get { return Data; } set { Data = value; } }
    }
}