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
    public class RemoveCardSMSCheckRequest : BaseSubscriptionRequest
    {
        [DataMember]
        public string CardToken { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceRemoveCardSMSCheckRequest : BaseRequest<RemoveCardSMSCheckRequest, SHA1>
    {
        [DataMember]
        public RemoveCardSMSCheckRequest RemoveCardSMSCheckParameters { get { return Data; } set { Data = value; } }
    }
}