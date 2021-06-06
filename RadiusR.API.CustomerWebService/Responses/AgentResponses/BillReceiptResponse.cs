using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.AgentResponses
{
    [DataContract]
    public class BillReceiptResponse
    {
        [DataMember]
        public string FileName { get; set; }
        [DataMember]
        public byte[] FileContent { get; set; }
        [DataMember]
        public string MIMEType { get; set; }
    }
    [DataContract]
    public partial class AgentServiceBillReceiptResponse : BaseResponse<BillReceiptResponse, SHA256>
    {
        public AgentServiceBillReceiptResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public BillReceiptResponse BillReceiptResult { get { return Data; } set { Data = value; } }
    }
}