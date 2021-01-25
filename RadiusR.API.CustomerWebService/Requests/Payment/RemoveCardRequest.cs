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
    public class RemoveCardRequest : BaseSubscriptionRequest
    {
        [DataMember]
        public string CardToken { get; set; }
        [DataMember]
        public string SMSCode { get; set; }
        [DataMember]
        public HttpContextParameters HttpContextParameters { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceRemoveCardRequest : BaseRequest<RemoveCardRequest, SHA1>
    {
        [DataMember]
        public RemoveCardRequest RemoveCardParameters { get { return Data; } set { Data = value; } }
    }
}