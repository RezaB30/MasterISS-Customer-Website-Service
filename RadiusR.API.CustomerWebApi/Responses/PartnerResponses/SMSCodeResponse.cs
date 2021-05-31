using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.PartnerResponses
{
    [DataContract]
    public class SMSCodeResponse
    {
        [DataMember]
        public string Code { get; set; }
    }
    [DataContract]
    public partial class PartnerServiceSMSCodeResponse : BaseResponse<SMSCodeResponse, SHA256>
    {
        public PartnerServiceSMSCodeResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public SMSCodeResponse SMSCodeResponse { get { return Data; } set { Data = value; } }
    }
}