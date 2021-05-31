using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

/// <summary>
/// Summary description for SupportRegisterResponse
/// </summary>
namespace RadiusR.API.CustomerWebService.Responses.Support
{
    [DataContract]
    public class SupportRegisterResponse
    {
        [DataMember]
        public bool SupportRegisterResult { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceSupportRegisterResponse : BaseResponse<SupportRegisterResponse, SHA1>
    {
        public CustomerServiceSupportRegisterResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public SupportRegisterResponse SupportRegisterResponse
        {
            get { return Data; }
            set { Data = value; }
        }
    }
}
