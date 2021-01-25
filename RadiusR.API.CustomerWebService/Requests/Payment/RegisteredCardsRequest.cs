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
    public class RegisteredCardsRequest : BaseSubscriptionRequest
    {
        [DataMember]
        public HttpContextParameters HttpContextParameters { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceRegisteredCardsRequest : BaseRequest<RegisteredCardsRequest, SHA1>
    {
        [DataMember]
        public RegisteredCardsRequest RegisteredCardsParameters { get { return Data; } set { Data = value; } }
    }
}