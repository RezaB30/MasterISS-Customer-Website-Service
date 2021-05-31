using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.Payment
{
    [DataContract]
    public class VPOSFormResponse
    {
        [DataMember]
        public string HtmlForm { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceVPOSFormResponse : BaseResponse<VPOSFormResponse, SHA1>
    {
        public CustomerServiceVPOSFormResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public VPOSFormResponse VPOSFormResponse
        {
            get { return Data; }
            set { Data = value; }
        }
    }
}