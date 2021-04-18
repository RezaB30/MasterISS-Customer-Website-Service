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
    public class GetCustomerFilesResponse
    {
        [DataMember]
        public string FileExtention { get; set; }
        [DataMember]
        public string MIMEType { get; set; }
        [DataMember]
        public string ServerSideName { get; set; }
        [DataMember]
        public FileInfo FileInfo { get; set; }
    }
    [DataContract]
    public class FileInfo
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public int Type { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceGetCustomerFileResponse : BaseResponse<GetCustomerFilesResponse[], SHA1>
    {
        public CustomerServiceGetCustomerFileResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public GetCustomerFilesResponse[] CustomerFiles { get { return Data; } set { Data = value; } }
    }
}