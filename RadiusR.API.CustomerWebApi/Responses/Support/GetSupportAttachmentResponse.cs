using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.Support
{
    [DataContract]
    public class GetSupportAttachmentResponse
    {
        [DataMember]
        public byte[] FileContent { get; set; }
        [DataMember]
        public string CreationDate { get; set; }
        [DataMember]
        public string FileExtention { get; set; }
        [DataMember]
        public string FileName { get; set; }
        [DataMember]
        public string MD5 { get; set; }
        [DataMember]
        public string MIMEType { get; set; }
        [DataMember]
        public string ServerSideName { get; set; }
        [DataMember]
        public long StageId { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceGetSupportAttachmentResponse : BaseResponse<GetSupportAttachmentResponse, SHA1>
    {
        public CustomerServiceGetSupportAttachmentResponse(string passwordhash, BaseRequest<SHA1> baseRequest) : base(passwordhash, baseRequest) { }
        [DataMember]
        public GetSupportAttachmentResponse GetSupportAttachment { get { return Data; } set { Data = value; } }
    }
}