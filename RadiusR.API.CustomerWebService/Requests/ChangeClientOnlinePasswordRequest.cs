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
    public class ChangeClientOnlinePasswordRequest
    {
        [DataMember]
        public string OnlinePassword { get; set; }
        [DataMember]
        public long? SubscriptionId { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceChangeClientOnlinePasswordRequest : BaseRequest<ChangeClientOnlinePasswordRequest, SHA1>
    {
        [DataMember]
        public ChangeClientOnlinePasswordRequest ChangeClientOnlinePasswordParameters { get { return Data; } set { Data = value; } }
    }
}