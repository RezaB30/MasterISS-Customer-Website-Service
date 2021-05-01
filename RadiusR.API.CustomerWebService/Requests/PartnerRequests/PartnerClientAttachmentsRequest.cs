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
    public class PartnerClientAttachmentsRequest : RequestBase
    {
        [DataMember]
        public long? SubscriptionId { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceClientAttachmentsRequest : BaseRequest<PartnerClientAttachmentsRequest, SHA256>
    {
        [DataMember]
        public PartnerClientAttachmentsRequest ClientAttachmentsParameters { get { return Data; } set { Data = value; } }
    }
}