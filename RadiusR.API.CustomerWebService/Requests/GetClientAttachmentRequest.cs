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
    public class GetClientAttachmentRequest
    {
        [DataMember]
        public long? SubscriptionId { get; set; }
        [DataMember]
        public string FileName { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceGetClientAttachmentRequest : BaseRequest<GetClientAttachmentRequest, SHA1>
    {
        [DataMember]
        public GetClientAttachmentRequest GetClientAttachment { get { return Data; } set { Data = value; } }
    }
}