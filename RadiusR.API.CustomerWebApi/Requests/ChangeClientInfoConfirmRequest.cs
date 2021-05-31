using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests
{
    [DataContract]
    public class ChangeClientInfoConfirmRequest
    {
        [DataMember]
        public long? SubscriptionId { get; set; }
        [DataMember]
        public string ContactPhoneNo { get; set; }
        [DataMember]
        public string Email { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceChangeClientInfoConfirmRequest : BaseRequest<ChangeClientInfoConfirmRequest, SHA1>
    {
        [DataMember]
        public ChangeClientInfoConfirmRequest ChangeClientInfoConfirmRequest { get { return Data; } set { Data = value; } }
    }
}