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
    public class GetSupportAttachmentListResponse
    {
        [DataMember]
        public string FileExtention { get; set; }
        [DataMember]
        public string MIMEType { get; set; }
        [DataMember]
        public string ServerSideName { get; set; }
        [DataMember]
        public string FileName { get; set; }
        [DataMember]
        public string MD5 { get; set; }
        [DataMember]
        public string Datetime { get; set; }
        [DataMember]
        public long StageId { get; set; }
    }
    [DataContract]
    public partial class CustomerServicGetSupportAttachmentListResponse : BaseResponse<GetSupportAttachmentListResponse[], SHA1>
    {
        public CustomerServicGetSupportAttachmentListResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public GetSupportAttachmentListResponse[] GetSupportAttachmentList { get { return Data; } set { Data = value; } }
    }
}