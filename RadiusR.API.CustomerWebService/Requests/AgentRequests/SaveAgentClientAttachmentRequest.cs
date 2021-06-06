using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.AgentRequests
{
    [DataContract]
    public class SaveAgentClientAttachmentRequest : RequestBase
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
    public partial class AgentServiceSaveClientAttachmentRequest : BaseRequest<SaveAgentClientAttachmentRequest, SHA256>
    {
        [DataMember]
        public SaveAgentClientAttachmentRequest SaveClientAttachmentParameters { get { return Data; } set { Data = value; } }
    }
}