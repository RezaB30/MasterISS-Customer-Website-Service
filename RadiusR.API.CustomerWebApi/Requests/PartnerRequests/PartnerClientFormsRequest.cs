using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.PartnerRequests
{
    [DataContract]
    public class PartnerClientFormsRequest : RequestBase
    {
        [DataMember]
        public long? SubscriptionId { get; set; }
        [DataMember]
        public int? FormType { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceClientFormsRequest : BaseRequest<PartnerClientFormsRequest, SHA256>
    {
        [DataMember]
        public PartnerClientFormsRequest ClientFormsParameters { get { return Data; } set { Data = value; } }
    }
}