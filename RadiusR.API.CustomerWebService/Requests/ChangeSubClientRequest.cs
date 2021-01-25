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
    public class ChangeSubClientRequest
    {
        [DataMember]
        public long? CurrentSubscriptionID { get; set; }
        [DataMember]
        public long? TargetSubscriptionID { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceChangeSubClientRequest : BaseRequest<ChangeSubClientRequest, SHA1>
    {
        [DataMember]
        public ChangeSubClientRequest ChangeSubClientRequest { get { return Data; } set { Data = value; } }
    }
}