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
    public class GetSupportAttachmentListRequest
    {
        [DataMember]
        public long? RequestId { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceGetSupportAttachmentListRequest : BaseRequest<GetSupportAttachmentListRequest, SHA1>
    {
        [DataMember]
        public GetSupportAttachmentListRequest GetSupportAttachmentsParameters { get { return Data; } set { Data = value; } }
    }
}