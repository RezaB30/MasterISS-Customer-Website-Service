using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Requests.Support
{
    [DataContract]
    public class SaveSupportAttachmentRequest
    {
        [DataMember]
        public long? SupportRequestId { get; set; }
        [DataMember]
        public byte[] FileContent { get; set; }
        [DataMember]
        public long? StageId { get; set; }
        [DataMember]
        public string FileName { get; set; }
        [DataMember]
        public string FileExtention { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceSaveSupportAttachmentRequest : BaseRequest<SaveSupportAttachmentRequest, SHA1>
    {
        [DataMember]
        public SaveSupportAttachmentRequest SaveSupportAttachmentParameters { get; set; }
    }
}