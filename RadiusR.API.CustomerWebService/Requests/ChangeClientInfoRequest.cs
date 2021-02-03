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
    public class ChangeClientInfoRequest
    {
        [DataMember]
        public long? SubscriptionId { get; set; }
        [DataMember]
        public string ContactPhoneNo { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceChangeClientInfoRequest : BaseRequest<ChangeClientInfoRequest, SHA1>
    {
        [DataMember]
        public ChangeClientInfoRequest ChangeClientInfoRequest { get { return Data; } set { Data = value; } }
    }
}