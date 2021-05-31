using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses
{
    [DataContract]
    public class GetClientAttachmentResponse : GetCustomerFilesResponse
    {
        [DataMember]
        public string MD5 { get; set; }
        [DataMember]
        public byte[] Content { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceGetClientAttachmentResponse : BaseResponse<GetClientAttachmentResponse, SHA1>
    {
        public CustomerServiceGetClientAttachmentResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public GetClientAttachmentResponse GetClientAttachment { get { return Data; } set { Data = value; } }
    }
}