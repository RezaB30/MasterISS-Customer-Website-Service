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
    public class SavePartnerClientAttachmentRequest
    {
        [DataMember]
        public byte[] FileContent { get; set; }
        [DataMember]
        public string FileExtention { get; set; }
        [DataMember]
        public int? AttachmentType { get; set; }
        [DataMember]
        public long? SubscriptionId { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceSaveClientAttachmentRequest : BaseRequest<SavePartnerClientAttachmentRequest, SHA256>
    {
        [DataMember]
        public SavePartnerClientAttachmentRequest SaveClientAttachmentParameters { get { return Data; } set { Data = value; } }
    }
}