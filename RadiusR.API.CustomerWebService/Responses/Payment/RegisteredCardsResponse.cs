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
    public class RegisteredCardsResponse
    {
        [DataMember]
        public string MaskedCardNo { get; set; }
        [DataMember]
        public string Token { get; set; }
        [DataMember]
        public bool HasAutoPayments { get; set; }
        //[DataMember]
        //public long SubscriptionId { get; set; }
        //[DataMember]
        //public string SubscriberNo { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceRegisteredCardsResponse : BaseResponse<IEnumerable<RegisteredCardsResponse>, SHA1>
    {
        public CustomerServiceRegisteredCardsResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public IEnumerable<RegisteredCardsResponse> RegisteredCardList { get { return Data; } set { Data = value; } }
    }
}