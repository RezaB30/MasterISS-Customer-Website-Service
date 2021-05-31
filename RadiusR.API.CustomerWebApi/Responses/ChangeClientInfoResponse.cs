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
    public class ChangeClientInfoResponse
    {
        [DataMember]
        public string SMSCode { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceChangeClientInfoResponse : BaseResponse<ChangeClientInfoResponse, SHA1>
    {
        public CustomerServiceChangeClientInfoResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public ChangeClientInfoResponse ChangeClientInfoResponse { get { return Data; } set { Data = value; } }
    }
}