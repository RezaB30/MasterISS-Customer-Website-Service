using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

/// <summary>
/// Summary description for CustomerServiceResponses
/// </summary>

namespace RadiusR.API.CustomerWebService.Responses
{
    [DataContract]
    public partial class CustomerServiceSendSupportMessageResponse : BaseResponse<bool, SHA1>
    {
        public CustomerServiceSendSupportMessageResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public bool SendSupportMessageResponse
        {
            get { return Data; }
            set { Data = value; }
        }
    }
    [DataContract]
    public partial class CustomerServiceCanHaveQuotaSaleResponse : BaseResponse<bool?, SHA1>
    {
        public CustomerServiceCanHaveQuotaSaleResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public bool? CanHaveQuotaSale
        {
            get { return Data; }
            set { Data = value; }
        }
    }
    [DataContract]
    public partial class CustomerServiceVPOSErrorParameterNameResponse : BaseResponse<string, SHA1>
    {
        public CustomerServiceVPOSErrorParameterNameResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public string VPOSErrorParameterName
        {
            get { return Data; }
            set { Data = value; }
        }
    }
}
