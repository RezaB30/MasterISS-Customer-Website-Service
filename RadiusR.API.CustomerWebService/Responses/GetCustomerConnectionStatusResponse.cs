using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

/// <summary>
/// Summary description for GetCustomerStatusResponse
/// </summary>
namespace RadiusR.API.CustomerWebService.Responses
{
    [DataContract]
    public class GetCustomerConnectionStatusResponse
    {
        [DataMember]
        public string XDSLNo { get; set; }
        [DataMember]
        public short XDSLTypeValue { get; set; }
        [DataMember]
        public string XDSLTypeText { get; set; }
        [DataMember]
        public string CurrentDownload { get; set; }
        [DataMember]
        public string CurrentUpload { get; set; }
        [DataMember]
        public short ConnectionStatusValue { get; set; }
        [DataMember]
        public string ConnectionStatusText { get; set; }
        //public string IPAddress => string.Empty;
        [DataMember]
        public string DownloadMargin { get; set; }
        [DataMember]
        public string UploadMargin { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceConnectionStatusResponse : BaseResponse<GetCustomerConnectionStatusResponse, SHA1>
    {
        public CustomerServiceConnectionStatusResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public GetCustomerConnectionStatusResponse GetCustomerConnectionStatusResponse
        {
            get { return Data; }
            set { Data = value; }
        }
    }
}
