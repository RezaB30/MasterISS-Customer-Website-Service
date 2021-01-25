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
    public class GetClientPDFFormResponse
    {
        [DataMember]
        public byte[] FileContent { get; set; }
        [DataMember]
        public string MIMEType { get; set; }
        [DataMember]
        public string FileName { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceGetClientPDFFormResponse : BaseResponse<GetClientPDFFormResponse, SHA1>
    {
        public CustomerServiceGetClientPDFFormResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public GetClientPDFFormResponse GetClientPDFFormResult { get { return Data; } set { Data = value; } }
    }
}