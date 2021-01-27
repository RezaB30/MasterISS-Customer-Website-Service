using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

/// <summary>
/// Summary description for EArchivePDFResponse
/// </summary>
namespace RadiusR.API.CustomerWebService.Responses.Payment
{
    [DataContract]
    public class EArchivePDFResponse
    {
        [DataMember]
        public byte[] FileContent { get; set; }
        [DataMember]
        public string ContentType { get; set; }
        [DataMember]
        public string FileDownloadName { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceEArchivePDFResponse : BaseResponse<EArchivePDFResponse, SHA1>
    {
        public CustomerServiceEArchivePDFResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public EArchivePDFResponse EArchivePDFResponse
        {
            get { return Data; }
            set { Data = value; }
        }
    }
    [DataContract]
    public partial class CustomerServiceEArchivePDFMailResponse : BaseResponse<bool?, SHA1>
    {
        public CustomerServiceEArchivePDFMailResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public bool? EArchivePDFMailResponse
        {
            get { return Data; }
            set { Data = value; }
        }
    }
}
