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
    public class GetSupportAttachmentRequest
    {
        [DataMember]
        public long? SupportRequestId { get; set; }
        [DataMember]
        public string FileName { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceGetSupportAttachmentRequest : BaseRequest<GetSupportAttachmentRequest, SHA1>
    {
        [DataMember]
        public GetSupportAttachmentRequest GetSupportAttachmentParameters { get { return Data; } set { Data = value; } }
    }
}